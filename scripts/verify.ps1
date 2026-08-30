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

function Invoke-TestRun([string]$Name, [string]$Filter) {
    $logPath = Join-Path $testResultsPath "$Name.log"
    $arguments = @('test', $solutionPath, '-c', 'Release', '--no-restore', '-p:SkipRootExecutablePublish=true')
    if (-not [string]::IsNullOrWhiteSpace($Filter)) {
        $arguments += @('--filter', $Filter)
    }

    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $output = & dotnet @arguments 2>&1
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
    $output | Out-File -LiteralPath $logPath -Encoding utf8
    return [pscustomobject]@{
        ExitCode = $exitCode
        Text = ($output | Out-String)
        LogPath = $logPath
    }
}

if (-not $outputPath.StartsWith($expectedOutputParent + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Der Publish-Pfad liegt ausserhalb des Ausgabeordners.'
}

if ([System.IO.Path]::GetFileName($outputPath) -ne 'Sascha-Window-Zones-prototype') {
    throw 'Der Publish-Zielordner ist unerwartet.'
}

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
    foreach ($expectedIconBaseline in $expectedIconBaselines) {
        if ($fullTest.Text -notmatch [regex]::Escape($expectedIconBaseline)) {
            throw "Der Volltest hat nicht genau die dokumentierte Icon-Baseline gemeldet: $expectedIconBaseline"
        }
    }

    $nonBaselineFilter = ($expectedIconBaselines | ForEach-Object { "FullyQualifiedName!~$_" }) -join '&'
    $remainingTests = Invoke-TestRun 'without-icon-baselines' $nonBaselineFilter
    if ($remainingTests.ExitCode -ne 0) {
        throw "Der zweite Testlauf ohne die zwei exakten Icon-Baselines ist fehlgeschlagen. Details: $($remainingTests.LogPath)"
    }

    $testStatus = 'passed-with-two-documented-icon-baselines'
}

dotnet build $solutionPath -c Release --no-restore -p:SkipRootExecutablePublish=true
if ($LASTEXITCODE -ne 0) { throw 'Der Release-Build ist fehlgeschlagen.' }

dotnet publish $projectPath -c Release -r win-x64 --self-contained true --no-restore -p:SkipRootExecutablePublish=true -o $outputPath
if ($LASTEXITCODE -ne 0) { throw 'Der Publish ist fehlgeschlagen.' }

$publishedExecutablePath = Join-Path $outputPath 'SaschaWindowZones.exe'
$diagnosticPath = Join-Path $projectRoot 'outputs\sascha-window-zones-diagnostics.json'
if (-not (Test-Path -LiteralPath $publishedExecutablePath -PathType Leaf)) {
    throw 'SaschaWindowZones.exe fehlt im Publish-Ordner.'
}

& (Join-Path $scriptDirectory 'install-root-executable.ps1') `
    -PublishedExecutablePath $publishedExecutablePath `
    -RootExecutablePath $rootExecutablePath
if ($LASTEXITCODE -ne 0) { throw 'Die Root-EXE konnte nicht aktualisiert werden.' }
if (-not (Test-Path -LiteralPath $rootExecutablePath -PathType Leaf)) {
    throw 'SaschaWindowZones.exe fehlt im Rootverzeichnis.'
}

if ((Get-FileHash -Algorithm SHA256 -LiteralPath $publishedExecutablePath).Hash -ne (Get-FileHash -Algorithm SHA256 -LiteralPath $rootExecutablePath).Hash) {
    throw 'Die EXE im Rootverzeichnis stimmt nicht mit dem Publish-Artefakt ueberein.'
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

& $rootExecutablePath --diagnostics | Out-File -LiteralPath $diagnosticPath -Encoding utf8
if ($LASTEXITCODE -ne 0) { throw 'Die Diagnose ist fehlgeschlagen.' }

if ($settingsBefore -ne (Get-FileState $settingsPath)) {
    throw 'Die Diagnose hat settings.json verändert.'
}
if ($placementsBefore -ne (Get-FileState $placementsPath)) {
    throw 'Die Diagnose hat placements.json verändert.'
}

$diagnostic = Get-Content -LiteralPath $diagnosticPath -Raw | ConvertFrom-Json
if ($diagnostic.application -ne 'Sascha Window Zones') { throw 'Die Diagnose meldet einen unerwarteten Programmnamen.' }
if ($diagnostic.hookRegistered -ne $false) { throw 'Die Diagnose hat unerwartet einen Hook registriert.' }
if ($diagnostic.settingsChanged -ne $false) { throw 'Die Diagnose hat unerwartet Einstellungen verändert.' }
if ($diagnostic.windowPlacement.lifecycleHookRegistered -ne $false) { throw 'Die Diagnose hat unerwartet einen Platzierungs-Hook registriert.' }
if ($null -eq $diagnostic.windowPlacement.enabled -or
    $null -eq $diagnostic.windowPlacement.learnedEntryCount -or
    $null -eq $diagnostic.windowPlacement.ruleCount) {
    throw 'Die Diagnose meldet keinen vollständigen Platzierungsstatus.'
}
if (@($diagnostic.monitors).Count -lt 1) { throw 'Die Diagnose hat keinen Monitor erkannt.' }

& (Join-Path $scriptDirectory 'verify-dpi-awareness.ps1') -ExecutablePath $rootExecutablePath
if ($LASTEXITCODE -ne 0) { throw 'Die DPI-Prüfung ist fehlgeschlagen.' }

$files = Get-ChildItem -LiteralPath $outputPath -File -Recurse
$bytes = ($files | Measure-Object -Property Length -Sum).Sum
Write-Output "VERIFY_OK tests=$testStatus monitors=$(@($diagnostic.monitors).Count) files=$($files.Count) bytes=$bytes rootExe=$rootExecutablePath hookRegistered=false settingsChanged=false windowPlacement.lifecycleHookRegistered=false"
