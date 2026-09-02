param(
    [switch]$SkipDpiCheck
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $scriptDirectory '..'))
$solutionPath = Join-Path $projectRoot 'ZoneManager.sln'
$projectPath = Join-Path $projectRoot 'src\SnapZones.App\SnapZones.App.csproj'
$outputPath = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'outputs\ZoneManager-prototype'))
$expectedOutputParent = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'outputs'))
$rootExecutablePath = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'ZoneManager.exe'))
$maximumExecutableBytes = 100000000

if (-not $outputPath.StartsWith($expectedOutputParent + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Der Publish-Pfad liegt ausserhalb des Ausgabeordners.'
}

if ([System.IO.Path]::GetFileName($outputPath) -ne 'ZoneManager-prototype') {
    throw 'Der Publish-Zielordner ist unerwartet.'
}

if (Test-Path -LiteralPath $outputPath) {
    Remove-Item -LiteralPath $outputPath -Recurse -Force
}

dotnet restore $solutionPath
if ($LASTEXITCODE -ne 0) { throw 'Die Paketwiederherstellung ist fehlgeschlagen.' }

dotnet restore $projectPath -r win-x64
if ($LASTEXITCODE -ne 0) { throw 'Die win-x64-Laufzeitwiederherstellung ist fehlgeschlagen.' }

# Die aufgerufenen Skripte melden Fehler ueber eine terminierende Ausnahme; $LASTEXITCODE bliebe hier
# auf dem Wert des zuletzt gestarteten nativen Befehls stehen und waere deshalb keine gueltige Pruefung.
& (Join-Path $scriptDirectory 'build-icon.ps1')

# Test und Release-Build brauchen die Root-EXE nicht. Ohne diesen Schalter loest jeder Build des
# App-Projekts einen vollstaendigen Self-contained-Publish aus; der Lauf wuerde die 72-MB-EXE
# viermal statt einmal erzeugen. Die Root-EXE entsteht unten aus dem Publish-Artefakt, und
# verify-root-build.ps1 prueft den impliziten Weg separat.
dotnet test $solutionPath -c Release --no-restore -p:SkipRootExecutablePublish=true
if ($LASTEXITCODE -ne 0) { throw 'Die Tests sind fehlgeschlagen.' }

dotnet build $solutionPath -c Release --no-restore -p:SkipRootExecutablePublish=true
if ($LASTEXITCODE -ne 0) { throw 'Der Release-Build ist fehlgeschlagen.' }

& (Join-Path $scriptDirectory 'verify-root-build.ps1') -MaximumExecutableBytes $maximumExecutableBytes

dotnet publish $projectPath -c Release -r win-x64 --self-contained true --no-restore -o $outputPath -p:SkipRootExecutablePublish=true
if ($LASTEXITCODE -ne 0) { throw 'Der Publish ist fehlgeschlagen.' }

$publishedExecutablePath = Join-Path $outputPath 'ZoneManager.exe'
$diagnosticPath = Join-Path $projectRoot 'outputs\zonemanager-diagnostics.json'
if (-not (Test-Path -LiteralPath $publishedExecutablePath -PathType Leaf)) {
    throw 'ZoneManager.exe fehlt im Publish-Ordner.'
}

$publishedExecutableBytes = (Get-Item -LiteralPath $publishedExecutablePath).Length
if ($publishedExecutableBytes -gt $maximumExecutableBytes) {
    throw "Die veröffentlichte EXE ist mit $publishedExecutableBytes Bytes grösser als das erlaubte Maximum von $maximumExecutableBytes Bytes."
}

& (Join-Path $scriptDirectory 'install-root-executable.ps1') `
    -PublishedExecutablePath $publishedExecutablePath `
    -RootExecutablePath $rootExecutablePath
if (-not (Test-Path -LiteralPath $rootExecutablePath -PathType Leaf)) {
    throw 'ZoneManager.exe fehlt im Rootverzeichnis.'
}

if ((Get-FileHash -LiteralPath $publishedExecutablePath).Hash -ne (Get-FileHash -LiteralPath $rootExecutablePath).Hash) {
    throw 'Die EXE im Rootverzeichnis stimmt nicht mit dem Publish-Artefakt ueberein.'
}

& $rootExecutablePath --diagnostics | Out-File -LiteralPath $diagnosticPath -Encoding utf8
if ($LASTEXITCODE -ne 0) { throw 'Die Diagnose ist fehlgeschlagen.' }

$diagnostic = Get-Content -LiteralPath $diagnosticPath -Raw | ConvertFrom-Json
if ($diagnostic.application -ne "Sascha’s Zone Manager") { throw 'Die Diagnose meldet einen unerwarteten Programmnamen.' }
if ($diagnostic.hookRegistered -ne $false) { throw 'Die Diagnose hat unerwartet einen Hook registriert.' }
if ($diagnostic.settingsChanged -ne $false) { throw 'Die Diagnose hat unerwartet Einstellungen verändert.' }
if (@($diagnostic.monitors).Count -lt 1) { throw 'Die Diagnose hat keinen Monitor erkannt.' }
if ($diagnostic.startupConfigurationReady -ne $true) { throw 'Die Diagnose konnte keine leere Startkonfiguration initialisieren.' }
if ([int]$diagnostic.startupLayoutCount -ne @($diagnostic.monitors).Count) { throw 'Die Diagnose hat nicht für jeden Monitor ein Startlayout erzeugt.' }

$dpiStatus = 'passed'
if ($SkipDpiCheck) {
    $dpiStatus = 'skipped'
}
else {
    & (Join-Path $scriptDirectory 'verify-dpi-awareness.ps1') -ExecutablePath $rootExecutablePath
}

# Der unsichtbare Fensterrand wird an bereits offenen Fenstern gemessen, nicht angenommen. Ueberschreitet
# er die Obergrenze aus WindowFrameCompensation, bricht die Messung ab: dann waeren sowohl der Ausgleich
# beim Einrasten als auch die Toleranz, mit der MainZoneFallback ein eingerastetes Fenster erkennt, falsch.
$frameOutput = & (Join-Path $scriptDirectory 'measure-window-frame.ps1')
$frameOutput | Write-Output
$frameLine = @($frameOutput | Where-Object { $_ -is [string] -and $_ -match '^FRAME_(OK|SKIPPED)' })
$frameStatus = if ($frameLine.Count -gt 0 -and $frameLine[-1] -match '^FRAME_OK.*largest=(\d+)') {
    "measured-$($Matches[1])px"
}
else {
    'skipped'
}

$files = Get-ChildItem -LiteralPath $outputPath -File -Recurse
$bytes = ($files | Measure-Object -Property Length -Sum).Sum
Write-Output "VERIFY_OK tests=passed rootBuild=passed dpi=$dpiStatus windowFrame=$frameStatus monitors=$(@($diagnostic.monitors).Count) startupLayouts=$($diagnostic.startupLayoutCount) files=$($files.Count) bytes=$bytes maximumExecutableBytes=$maximumExecutableBytes rootExe=$rootExecutablePath hookRegistered=false settingsChanged=false"
