param(
    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $scriptDirectory '..'))
$solutionPath = Join-Path $projectRoot 'SnapZones.sln'
$projectPath = Join-Path $projectRoot 'src\SnapZones.App\SnapZones.App.csproj'
$outputPath = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'outputs\Sascha-Window-Zones-prototype'))
$expectedOutputParent = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'outputs'))
$rootExecutablePath = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'SaschaWindowZones.exe'))
$testResultsPath = Join-Path $projectRoot 'outputs\verify-test-results'
$portableProbePrefix = [System.IO.Path]::GetFullPath((Join-Path ([System.IO.Path]::GetTempPath()) 'SnapZones.verify-portable'))
$expectedIconBaselines = @(
    'SnapZones.Tests.Theme.ThemeResourceTests.Brand_icon_uses_only_neutral_greys',
    'SnapZones.Tests.Theme.ThemeResourceTests.Brand_icon_uses_two_wide_lower_tiles_instead_of_a_monitor_stand'
)

function Get-FileState([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return 'missing'
    }

    return "present:$((Get-FileHash -Algorithm SHA256 -LiteralPath $Path).Hash)"
}

function Get-DirectoryState([string]$Path) {
    if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
        return 'missing'
    }

    return @(
        'D:.'
        Get-ChildItem -LiteralPath $Path -Directory -Recurse | ForEach-Object {
            $relativePath = $_.FullName.Substring($Path.Length).TrimStart([System.IO.Path]::DirectorySeparatorChar)
            "D:${relativePath}"
        }
        Get-ChildItem -LiteralPath $Path -File -Recurse | ForEach-Object {
            $relativePath = $_.FullName.Substring($Path.Length).TrimStart([System.IO.Path]::DirectorySeparatorChar)
            "F:${relativePath}:$((Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash)"
        } | Sort-Object
    ) -join "`n"
}

function Get-TrxCounterValue($Counter, [string]$Name) {
    $value = $Counter.GetAttribute($Name)
    if ([string]::IsNullOrEmpty($value)) {
        return 0
    }

    return [int]$value
}

function Read-TestRun([string]$TrxPath, [int]$ExitCode) {
    if (-not (Test-Path -LiteralPath $TrxPath -PathType Leaf)) {
        throw "Die TRX-Datei fehlt: $TrxPath"
    }

    [xml]$trx = Get-Content -LiteralPath $TrxPath -Raw
    $counter = $trx.SelectSingleNode("//*[local-name()='ResultSummary']/*[local-name()='Counters']")
    if ($null -eq $counter) {
        throw "Die TRX-Datei enthält keine Ergebniszähler: $TrxPath"
    }

    $results = @($trx.SelectNodes("//*[local-name()='UnitTestResult']") | ForEach-Object {
        [pscustomobject]@{ Name = $_.testName; Outcome = $_.outcome }
    })
    return [pscustomobject]@{
        ExitCode = $ExitCode
        TrxPath = $TrxPath
        Total = [int]$counter.total
        Passed = [int]$counter.passed
        Failed = [int]$counter.failed
        Error = [int]$counter.error
        Executed = Get-TrxCounterValue $counter 'executed'
        Timeout = Get-TrxCounterValue $counter 'timeout'
        Aborted = Get-TrxCounterValue $counter 'aborted'
        Inconclusive = Get-TrxCounterValue $counter 'inconclusive'
        PassedButRunAborted = Get-TrxCounterValue $counter 'passedButRunAborted'
        NotRunnable = Get-TrxCounterValue $counter 'notRunnable'
        NotExecuted = Get-TrxCounterValue $counter 'notExecuted'
        Disconnected = Get-TrxCounterValue $counter 'disconnected'
        Warning = Get-TrxCounterValue $counter 'warning'
        Completed = Get-TrxCounterValue $counter 'completed'
        InProgress = Get-TrxCounterValue $counter 'inProgress'
        Pending = Get-TrxCounterValue $counter 'pending'
        Results = $results
    }
}

function Assert-CleanTestRun($TestRun) {
    $nonSuccessCounters = @(
        'Failed', 'Error', 'Timeout', 'Aborted', 'Inconclusive', 'PassedButRunAborted',
        'NotRunnable', 'NotExecuted', 'Disconnected', 'Warning', 'Completed', 'InProgress', 'Pending'
    )
    if ($TestRun.ExitCode -ne 0 -or $TestRun.Total -ne $TestRun.Executed -or $TestRun.Total -ne $TestRun.Passed -or
        $TestRun.Results.Count -ne $TestRun.Total -or
        $null -ne ($TestRun.Results | Where-Object Outcome -ne 'Passed') -or
        $null -ne ($nonSuccessCounters | Where-Object { $TestRun.$_ -ne 0 })) {
        throw "Der Testlauf ist nicht vollständig grün. TRX: $($TestRun.TrxPath)"
    }
}

function Invoke-TestRun([string]$Name, [string]$Filter) {
    $trxPath = Join-Path $testResultsPath "$Name.trx"
    $arguments = @(
        'test', $solutionPath, '-c', 'Release', '--no-restore', '-p:SkipRootExecutablePublish=true',
        '--logger', "trx;LogFileName=$Name.trx", '--results-directory', $testResultsPath)
    if (-not [string]::IsNullOrWhiteSpace($Filter)) {
        $arguments += @('--filter', $Filter)
    }

    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        & dotnet @arguments | Out-Host
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    return Read-TestRun $trxPath $exitCode
}

function Assert-ExpectedIconBaselines($TestRun) {
    $failedNames = @($TestRun.Results | Where-Object Outcome -eq 'Failed' | ForEach-Object Name | Sort-Object)
    $expectedNames = @($expectedIconBaselines | Sort-Object)
    if ($TestRun.Error -ne 0 -or $TestRun.Failed -ne $expectedNames.Count -or
        $failedNames.Count -ne $expectedNames.Count -or
        $TestRun.Results.Count -ne $TestRun.Total -or
        $null -ne ($TestRun.Results | Where-Object { $_.Outcome -notin @('Passed', 'Failed') }) -or
        $null -ne (Compare-Object -ReferenceObject $expectedNames -DifferenceObject $failedNames)) {
        throw "Der Volltest enthält nicht exakt die zwei dokumentierten Icon-Baselines. TRX: $($TestRun.TrxPath)"
    }
}

function Assert-DiagnosticSafety($Diagnostic) {
    if ($Diagnostic.hookRegistered -isnot [bool] -or $Diagnostic.hookRegistered -ne $false -or
        $Diagnostic.settingsChanged -isnot [bool] -or $Diagnostic.settingsChanged -ne $false -or
        $Diagnostic.windowPlacement.lifecycleHookRegistered -isnot [bool] -or
        $Diagnostic.windowPlacement.lifecycleHookRegistered -ne $false) {
        throw 'Die Diagnose meldet keinen schreibgeschützten Sicherheitsstatus.'
    }
}

function Remove-PortableProbeDirectory([string]$Path) {
    $prefix = [System.IO.Path]::GetFullPath($portableProbePrefix).TrimEnd([System.IO.Path]::DirectorySeparatorChar)
    $target = [System.IO.Path]::GetFullPath($Path).TrimEnd([System.IO.Path]::DirectorySeparatorChar)
    if ($target -eq $prefix -or -not $target.StartsWith($prefix + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw 'Der portable Probeordner liegt ausserhalb des expliziten Temp-Präfixes.'
    }

    if (Test-Path -LiteralPath $target -PathType Container) {
        Remove-Item -LiteralPath $target -Recurse -Force
    }
}

function Invoke-PortableDiagnosticsProbe([string]$ExecutablePath) {
    $probeDirectory = Join-Path $portableProbePrefix "probe-$PID-$([Guid]::NewGuid().ToString('N'))"
    $probeExecutable = Join-Path $probeDirectory 'SaschaWindowZones.exe'
    $probeDataDirectory = Join-Path $probeDirectory 'Data'
    $probeOutputPath = Join-Path $testResultsPath 'portable-diagnostics.json'
    try {
        New-Item -ItemType Directory -Path $probeDataDirectory -Force | Out-Null
        Copy-Item -LiteralPath $ExecutablePath -Destination $probeExecutable
        New-Item -ItemType File -Path (Join-Path $probeDirectory 'portable.flag') | Out-Null
        Set-Content -LiteralPath (Join-Path $probeDataDirectory 'settings.json') -Encoding utf8 -NoNewline -Value '{"schemaVersion":2,"settings":{"restoreWindowPlacementEnabled":false,"windowPlacementRules":[{},{}]}}'
        Set-Content -LiteralPath (Join-Path $probeDataDirectory 'placements.json') -Encoding utf8 -NoNewline -Value '{"schemaVersion":1,"entries":[{},{}]}'
        $before = Get-DirectoryState $probeDirectory

        $process = Start-Process -FilePath $probeExecutable -ArgumentList '--diagnostics' -PassThru -RedirectStandardOutput $probeOutputPath -WindowStyle Hidden
        if (-not $process.WaitForExit(10000)) {
            Stop-Process -Id $process.Id -ErrorAction SilentlyContinue
            throw 'Die portable Diagnose hat das Zeitlimit überschritten.'
        }
        $process.Refresh()
        $exitCode = [int]$process.ExitCode
        if ($exitCode -ne 0) {
            throw "Die portable Diagnose ist fehlgeschlagen: ExitCode $exitCode"
        }
        if ($before -ne (Get-DirectoryState $probeDirectory)) {
            throw 'Die portable Diagnose hat Dateien oder Verzeichnisse verändert.'
        }

        $diagnostic = Get-Content -LiteralPath $probeOutputPath -Raw | ConvertFrom-Json
        if ($diagnostic.windowPlacement.enabled -ne $false -or
            $diagnostic.windowPlacement.learnedEntryCount -ne 2 -or
            $diagnostic.windowPlacement.ruleCount -ne 2) {
            throw 'Die portable Diagnose meldet keinen erwarteten Platzierungsstatus.'
        }
        Assert-DiagnosticSafety $diagnostic
        return $diagnostic
    }
    finally {
        Remove-PortableProbeDirectory $probeDirectory
    }
}

function Write-SyntheticTrx([string]$Path, [hashtable]$Counters, [string]$Outcome) {
    $attributes = ($Counters.GetEnumerator() | Sort-Object Key | ForEach-Object { '{0}="{1}"' -f $_.Key, $_.Value }) -join ' '
    @"
<?xml version="1.0" encoding="utf-8"?>
<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010">
  <Results><UnitTestResult testName="Synthetic.Test" outcome="$Outcome" /></Results>
  <ResultSummary outcome="Completed"><Counters $attributes /></ResultSummary>
</TestRun>
"@ | Set-Content -LiteralPath $Path -Encoding utf8
}

function Assert-Throws([scriptblock]$Action, [string]$Name) {
    try {
        & $Action
    }
    catch {
        return
    }

    throw "Der Selbsttest hat den ungültigen Fall nicht abgewiesen: $Name"
}

function Invoke-VerificationSelfTest {
    $directory = Join-Path $portableProbePrefix "selftest-$PID-$([Guid]::NewGuid().ToString('N'))"
    try {
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
        $validCounters = @{ total = 1; executed = 1; passed = 1; failed = 0; error = 0; timeout = 0; aborted = 0; inconclusive = 0; passedButRunAborted = 0; notRunnable = 0; notExecuted = 0; disconnected = 0; warning = 0; completed = 0; inProgress = 0; pending = 0 }
        $validTrx = Join-Path $directory 'valid.trx'
        Write-SyntheticTrx $validTrx $validCounters 'Passed'
        Assert-CleanTestRun (Read-TestRun $validTrx 0)

        $skippedCounters = @{} + $validCounters
        $skippedCounters.passed = 0
        $skippedCounters.notExecuted = 1
        $skippedTrx = Join-Path $directory 'skipped.trx'
        Write-SyntheticTrx $skippedTrx $skippedCounters 'NotExecuted'
        Assert-Throws { Assert-CleanTestRun (Read-TestRun $skippedTrx 0) } 'notExecuted'

        $unsafeDiagnostic = [pscustomobject]@{
            hookRegistered = $false
            settingsChanged = $true
            windowPlacement = [pscustomobject]@{ lifecycleHookRegistered = $false }
        }
        Assert-Throws { Assert-DiagnosticSafety $unsafeDiagnostic } 'settingsChanged=true'
        Write-Output 'VERIFY_SELFTEST_OK'
    }
    finally {
        Remove-PortableProbeDirectory $directory
    }
}

if ($SelfTest) {
    Invoke-VerificationSelfTest
    exit 0
}

if (-not $outputPath.StartsWith($expectedOutputParent + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Der Publish-Pfad liegt ausserhalb des Ausgabeordners.'
}
if ([System.IO.Path]::GetFileName($outputPath) -ne 'Sascha-Window-Zones-prototype') {
    throw 'Der Publish-Zielordner ist unerwartet.'
}

if (Test-Path -LiteralPath (Join-Path $projectRoot 'portable.flag') -PathType Leaf) {
    $configurationDirectory = Join-Path $projectRoot 'Data'
}
else {
    $configurationDirectory = Join-Path ([Environment]::GetFolderPath([Environment+SpecialFolder]::ApplicationData)) 'SnapZones'
}
$settingsPath = Join-Path $configurationDirectory 'settings.json'
$placementsPath = Join-Path $configurationDirectory 'placements.json'
$settingsBefore = Get-FileState $settingsPath
$placementsBefore = Get-FileState $placementsPath

if (Test-Path -LiteralPath $outputPath) {
    Remove-Item -LiteralPath $outputPath -Recurse -Force
}
if (Test-Path -LiteralPath $testResultsPath) {
    Remove-Item -LiteralPath $testResultsPath -Recurse -Force
}
New-Item -ItemType Directory -Path $testResultsPath | Out-Null

dotnet restore $solutionPath
if ($LASTEXITCODE -ne 0) { throw 'Die Paketwiederherstellung ist fehlgeschlagen.' }
dotnet restore $projectPath -r win-x64
if ($LASTEXITCODE -ne 0) { throw 'Die win-x64-Laufzeitwiederherstellung ist fehlgeschlagen.' }
& (Join-Path $scriptDirectory 'build-icon.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Das Programmicon konnte nicht erzeugt werden.' }

$fullTest = Invoke-TestRun 'full-suite' ''
$testStatus = 'passed'
if ($fullTest.ExitCode -ne 0) {
    Assert-ExpectedIconBaselines $fullTest
    $nonBaselineFilter = ($expectedIconBaselines | ForEach-Object { "FullyQualifiedName!=$_" }) -join '&'
    $remainingTests = Invoke-TestRun 'without-icon-baselines' $nonBaselineFilter
    if ($remainingTests.ExitCode -ne 0 -or $remainingTests.Failed -ne 0 -or $remainingTests.Error -ne 0 -or
        $remainingTests.Total -ne ($fullTest.Total - $expectedIconBaselines.Count)) {
        throw "Der Gegenlauf ohne die zwei exakten Icon-Baselines ist nicht vollständig grün. TRX: $($remainingTests.TrxPath)"
    }

    $testStatus = 'passed-with-two-documented-icon-baselines'
}
elseif ($fullTest.Failed -ne 0 -or $fullTest.Error -ne 0) {
    throw "Der erfolgreiche Volltest meldet dennoch Fehler. TRX: $($fullTest.TrxPath)"
}

dotnet build $solutionPath -c Release --no-restore -p:SkipRootExecutablePublish=true
if ($LASTEXITCODE -ne 0) { throw 'Der Release-Build ist fehlgeschlagen.' }
dotnet publish $projectPath -c Release -r win-x64 --self-contained true --no-restore -p:SkipRootExecutablePublish=true -o $outputPath
if ($LASTEXITCODE -ne 0) { throw 'Der Publish ist fehlgeschlagen.' }

$publishedExecutablePath = Join-Path $outputPath 'SaschaWindowZones.exe'
if (-not (Test-Path -LiteralPath $publishedExecutablePath -PathType Leaf)) {
    throw 'SaschaWindowZones.exe fehlt im Publish-Ordner.'
}
& (Join-Path $scriptDirectory 'install-root-executable.ps1') -PublishedExecutablePath $publishedExecutablePath -RootExecutablePath $rootExecutablePath
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $rootExecutablePath -PathType Leaf)) {
    throw 'Die Root-EXE konnte nicht aktualisiert werden.'
}

$publishedHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $publishedExecutablePath).Hash
$rootHash = (Get-FileHash -Algorithm SHA256 -LiteralPath $rootExecutablePath).Hash
if ($publishedHash -ne $rootHash) {
    throw 'Die EXE im Rootverzeichnis stimmt nicht mit dem Publish-Artefakt überein.'
}

& (Join-Path $scriptDirectory 'verify-dpi-awareness.ps1') -ExecutablePath $rootExecutablePath
if ($LASTEXITCODE -ne 0) { throw 'Die DPI-Prüfung ist fehlgeschlagen.' }
$diagnostic = Invoke-PortableDiagnosticsProbe $rootExecutablePath

$settingsUnchanged = $settingsBefore -eq (Get-FileState $settingsPath)
$placementsUnchanged = $placementsBefore -eq (Get-FileState $placementsPath)
if (-not $settingsUnchanged -or -not $placementsUnchanged) {
    throw 'Ein ausführender Prüfschritt hat settings.json oder placements.json verändert.'
}

$files = Get-ChildItem -LiteralPath $outputPath -File -Recurse
$bytes = ($files | Measure-Object -Property Length -Sum).Sum
Write-Output "VERIFY_OK tests=$testStatus fullTotal=$($fullTest.Total) fullPassed=$($fullTest.Passed) fullFailed=$($fullTest.Failed) monitors=$(@($diagnostic.monitors).Count) files=$($files.Count) bytes=$bytes rootExeHashEqual=$($rootHash -eq $publishedHash) settingsUnchanged=$settingsUnchanged placementsUnchanged=$placementsUnchanged hookRegistered=$($diagnostic.hookRegistered) settingsChanged=$($diagnostic.settingsChanged) windowPlacement.lifecycleHookRegistered=$($diagnostic.windowPlacement.lifecycleHookRegistered)"
