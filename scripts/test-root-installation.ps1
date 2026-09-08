param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$workRoot = Join-Path $projectRoot 'work'
$testRoot = Join-Path $workRoot ('installation-tests-' + [guid]::NewGuid().ToString('N'))
$installer = Join-Path $PSScriptRoot 'install-root-executable.ps1'
$failures = [Collections.Generic.List[string]]::new()
$passed = 0

function Assert-Value($Expected, $Actual, [string]$Message) {
    if ($Expected -ne $Actual) { throw $Message }
}

function New-TestFiles([string]$Name) {
    $root = Join-Path $testRoot $Name
    $source = Join-Path $root 'source'
    $target = Join-Path $root 'target'
    New-Item -ItemType Directory -Path $source, $target -Force | Out-Null
    foreach ($name in @('ZoneManager.exe', 'ZoneManager.Helper.exe')) {
        [IO.File]::WriteAllText((Join-Path $source $name), "new-$name")
        [IO.File]::WriteAllText((Join-Path $target $name), "old-$name")
    }
    return @{ Source = $source; Target = $target }
}

function Invoke-Install($Files) {
    & $installer -PublishedExecutablePath (Join-Path $Files.Source 'ZoneManager.exe') `
        -RootExecutablePath (Join-Path $Files.Target 'ZoneManager.exe') -ShutdownTimeoutSeconds 1 | Out-Null
}

function Test-Case([string]$Name, [scriptblock]$Action) {
    try { & $Action; $script:passed++; Write-Output "PASS $Name" }
    catch { $script:failures.Add("${Name}: $($_.Exception.Message)"); Write-Output "FAIL $Name" }
}

try {
    Test-Case 'Beide Dateien werden ersetzt' {
        $files = New-TestFiles 'success'
        Invoke-Install $files
        foreach ($name in @('ZoneManager.exe', 'ZoneManager.Helper.exe')) {
            Assert-Value "new-$name" ([IO.File]::ReadAllText((Join-Path $files.Target $name))) 'Datei nicht ersetzt.'
        }
    }

    Test-Case 'Gesperrter Helfer setzt die bereits getauschte EXE zurueck' {
        $files = New-TestFiles 'locked-helper'
        $locked = [IO.File]::Open((Join-Path $files.Target 'ZoneManager.Helper.exe'), 'Open', 'Read', 'Read')
        $rejected = $false
        try {
            try { Invoke-Install $files } catch { $rejected = $true }
        }
        finally { $locked.Dispose() }
        foreach ($name in @('ZoneManager.exe', 'ZoneManager.Helper.exe')) {
            Assert-Value "old-$name" ([IO.File]::ReadAllText((Join-Path $files.Target $name))) 'Ein gemischter Versionsstand blieb liegen.'
        }
        Assert-Value $true $rejected 'Der Fehler wurde nicht weitergegeben.'
    }

    Test-Case 'Fehlender Quellhelfer verhindert den Austausch' {
        $files = New-TestFiles 'missing-helper'
        [IO.File]::Delete((Join-Path $files.Source 'ZoneManager.Helper.exe'))
        $rejected = $false
        try { Invoke-Install $files } catch { $rejected = $true }
        Assert-Value 'old-ZoneManager.exe' ([IO.File]::ReadAllText((Join-Path $files.Target 'ZoneManager.exe'))) 'EXE trotz fehlendem Helfer ersetzt.'
        Assert-Value $true $rejected 'Fehlender Helfer wurde nicht abgelehnt.'
    }

    Test-Case 'Gleichzeitiger Austausch wird abgelehnt' {
        $files = New-TestFiles 'concurrent'
        $locked = [IO.File]::Open((Join-Path $files.Target '.zonemanager-install.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
        $rejected = $false
        try { try { Invoke-Install $files } catch { $rejected = $true } }
        finally { $locked.Dispose() }
        Assert-Value $true $rejected 'Die Installationssperre wurde nicht beachtet.'
        Assert-Value 'old-ZoneManager.exe' ([IO.File]::ReadAllText((Join-Path $files.Target 'ZoneManager.exe'))) 'EXE trotz Sperre ersetzt.'
    }

    Test-Case 'Gesperrte Hauptdatei laesst beide alten Dateien bestehen' {
        $files = New-TestFiles 'locked-exe'
        $locked = [IO.File]::Open((Join-Path $files.Target 'ZoneManager.exe'), 'Open', 'Read', 'Read')
        $rejected = $false
        try { try { Invoke-Install $files } catch { $rejected = $true } }
        finally { $locked.Dispose() }
        Assert-Value $true $rejected 'Die Dateisperre wurde nicht weitergegeben.'
        foreach ($name in @('ZoneManager.exe', 'ZoneManager.Helper.exe')) {
            Assert-Value "old-$name" ([IO.File]::ReadAllText((Join-Path $files.Target $name))) 'Original wurde veraendert.'
        }
    }

    Test-Case 'Nicht beendete Instanz verhindert den Austausch' {
        $files = New-TestFiles 'running'
        $fixture = Join-Path $testRoot 'fixture.exe'
        $builder = Join-Path $testRoot 'build-fixture.ps1'
        # Windows PowerShell baut eine kleine lokale .NET-Framework-EXE, die --exit absichtlich ignoriert.
        @'
param([string]$Destination)
$ErrorActionPreference = 'Stop'
Add-Type -OutputType WindowsApplication -OutputAssembly $Destination -TypeDefinition @"
public static class Fixture {
    public static void Main(string[] args) {
        if (args.Length > 0 && args[0] == "--exit") return;
        System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite);
    }
}
"@
'@ | Set-Content -LiteralPath $builder -Encoding UTF8
        & powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $builder -Destination $fixture
        if ($LASTEXITCODE -ne 0) { throw 'Testprogramm konnte nicht erstellt werden.' }
        $target = Join-Path $files.Target 'ZoneManager.exe'
        Copy-Item -LiteralPath $fixture -Destination $target -Force
        $before = (Get-FileHash -LiteralPath $target).Hash
        $process = Start-Process -FilePath $target -WindowStyle Hidden -PassThru
        try {
            $failure = ''
            try { Invoke-Install $files } catch { $failure = $_.Exception.Message }
            if ($failure -notlike '*hat sich nicht beendet*') { throw "Unerwarteter Abbruch: $failure" }
            Assert-Value $before (Get-FileHash -LiteralPath $target).Hash 'Laufende EXE wurde ersetzt.'
            Assert-Value 'old-ZoneManager.Helper.exe' ([IO.File]::ReadAllText((Join-Path $files.Target 'ZoneManager.Helper.exe'))) 'Helfer wurde ersetzt.'
            Assert-Value $false $process.HasExited 'Testinstanz wurde unerwartet beendet.'
        }
        finally {
            # Ausschliesslich den selbst gestarteten Testprozess beenden.
            if (-not $process.HasExited) { $process.Kill(); $process.WaitForExit() }
            $process.Dispose()
        }
    }

    if ($failures.Count) { throw ($failures -join [Environment]::NewLine) }
    Write-Output "INSTALLATION_TESTS_PASSED=$passed"
}
finally {
    $resolved = [IO.Path]::GetFullPath($testRoot)
    if ($resolved.StartsWith($workRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -and
        (Test-Path -LiteralPath $resolved)) {
        Remove-Item -LiteralPath $resolved -Recurse -Force -Confirm:$false
    }
}
