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

<#
.SYNOPSIS
    Baut ein kleines Testprogramm, das eine laufende Instanz nachstellt.

.DESCRIPTION
    Windows PowerShell uebersetzt eine kleine .NET-Framework-EXE ohne Fenster. Sie wartet, bis sie
    beendet wird. Mit -Obedient hoert sie auf die Bitte «--exit»: der zweite Aufruf derselben Datei
    setzt ein benanntes Ereignis, worauf die wartende Instanz endet — genau der Weg, den
    install-root-executable.ps1 nimmt. Ohne den Schalter ignoriert sie die Bitte.
#>
function New-Fixture([string]$Name, [switch]$Obedient) {
    $destination = Join-Path $testRoot $Name
    $builder = Join-Path $testRoot "build-$Name.ps1"
    $body = if ($Obedient) {
        @'
        if (args.Length > 0 && args[0] == "--exit") {
            try { System.Threading.EventWaitHandle.OpenExisting(Name).Set(); } catch { }
            return;
        }
        using (var handle = new System.Threading.EventWaitHandle(false, System.Threading.EventResetMode.ManualReset, Name)) {
            handle.WaitOne();
        }
'@
    }
    else {
        @'
        if (args.Length > 0 && args[0] == "--exit") return;
        System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite);
'@
    }

    # Der Rumpf wird eingesetzt statt eingefuegt: eine Zeichenkette mit Ersetzung kommt ohne
    # verschachtelte Here-Strings aus, und die enden sonst an der falschen Zeile.
    $template = @'
param([string]$Destination)
$ErrorActionPreference = 'Stop'
Add-Type -OutputType WindowsApplication -OutputAssembly $Destination -TypeDefinition @"
public static class Fixture {
    private const string Name = "Local\\ZoneManagerFixture";
    public static void Main(string[] args) {
__RUMPF__
    }
}
"@
'@
    Set-Content -LiteralPath $builder -Value $template.Replace('__RUMPF__', $body) -Encoding UTF8
    & powershell.exe -NoProfile -NonInteractive -ExecutionPolicy Bypass -File $builder -Destination $destination
    if ($LASTEXITCODE -ne 0) { throw "Testprogramm konnte nicht erstellt werden: $Name" }
    return $destination
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
        $fixture = New-Fixture -Name 'fixture-stur.exe' -Obedient:$false
        $target = Join-Path $files.Target 'ZoneManager.exe'
        Copy-Item -LiteralPath $fixture -Destination $target -Force
        $before = (Get-FileHash -LiteralPath $target).Hash
        # Nicht ueber die Shell starten: bei einer EXE auf einem Netzlaufwerk legt sie den Dialog
        # «Datei oeffnen - Sicherheitswarnung» vor, und der Testlauf wartete still auf einen Klick.
        $startInfo = [Diagnostics.ProcessStartInfo]::new()
        $startInfo.FileName = $target
        $startInfo.UseShellExecute = $false
        $startInfo.CreateNoWindow = $true
        $process = [Diagnostics.Process]::Start($startInfo)
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

    <#
      Der Gegenfall zum vorigen: eine Instanz, die auf «--exit» hoert, wird beendet und die Dateien
      werden ersetzt. Der Fall prueft, dass die Bitte ueberhaupt ankommt. Am 09.09.2026 kam sie eine
      Weile nicht an — der Start reichte die Schalter ueber eine Eigenschaft weiter, die es unter
      Windows PowerShell nicht gibt, und der Fehler blieb eine blosse Warnung.
    #>
    Test-Case 'Eine Instanz, die auf die Bitte hoert, wird beendet' {
        $files = New-TestFiles 'obedient'
        $fixture = New-Fixture -Name 'fixture-hoerend.exe' -Obedient
        $target = Join-Path $files.Target 'ZoneManager.exe'
        Copy-Item -LiteralPath $fixture -Destination $target -Force
        $startInfo = [Diagnostics.ProcessStartInfo]::new()
        $startInfo.FileName = $target
        $startInfo.UseShellExecute = $false
        $startInfo.CreateNoWindow = $true
        $process = [Diagnostics.Process]::Start($startInfo)
        try {
            # Die Instanz muss ihr Ereignis angelegt haben, bevor die Bitte es setzen kann.
            $deadline = [DateTime]::UtcNow.AddSeconds(10)
            while ([DateTime]::UtcNow -lt $deadline) {
                try { [Threading.EventWaitHandle]::OpenExisting('Local\ZoneManagerFixture').Dispose(); break }
                catch { Start-Sleep -Milliseconds 100 }
            }

            Invoke-Install $files
            Assert-Value $true $process.WaitForExit(10000) 'Die Instanz hat sich nicht beendet.'
            foreach ($name in @('ZoneManager.exe', 'ZoneManager.Helper.exe')) {
                Assert-Value $true (Test-Path -LiteralPath (Join-Path $files.Target $name) -PathType Leaf) "Datei fehlt nach dem Austausch: $name"
            }

            Assert-Value 'new-ZoneManager.Helper.exe' ([IO.File]::ReadAllText((Join-Path $files.Target 'ZoneManager.Helper.exe'))) 'Der Helfer wurde nicht ersetzt.'
        }
        finally {
            if (-not $process.HasExited) { $process.Kill(); $process.WaitForExit() }
            $process.Dispose()
            # Nach einem Austausch startet das Skript die zuvor beendete Instanz neu — hier also das
            # Testprogramm. Beendet wird ausschliesslich, was aus dem Testverzeichnis laeuft.
            Get-CimInstance Win32_Process -Filter "Name='ZoneManager.exe'" |
                Where-Object { $_.ExecutablePath -eq $target } |
                ForEach-Object { Stop-Process -Id $_.ProcessId -Force -Confirm:$false -ErrorAction SilentlyContinue }
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
