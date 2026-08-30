$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $scriptDirectory '..'))
$solutionPath = Join-Path $projectRoot 'SnapZones.sln'
$projectPath = Join-Path $projectRoot 'src\SnapZones.App\SnapZones.App.csproj'
$outputPath = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'outputs\Sascha-Window-Zones-prototype'))
$expectedOutputParent = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'outputs'))
$rootExecutablePath = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'SaschaWindowZones.exe'))

if (-not $outputPath.StartsWith($expectedOutputParent + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Der Publish-Pfad liegt ausserhalb des Ausgabeordners.'
}

if ([System.IO.Path]::GetFileName($outputPath) -ne 'Sascha-Window-Zones-prototype') {
    throw 'Der Publish-Zielordner ist unerwartet.'
}

if (Test-Path -LiteralPath $outputPath) {
    Remove-Item -LiteralPath $outputPath -Recurse -Force
}

dotnet restore $solutionPath
if ($LASTEXITCODE -ne 0) { throw 'Die Paketwiederherstellung ist fehlgeschlagen.' }

dotnet restore $projectPath -r win-x64
if ($LASTEXITCODE -ne 0) { throw 'Die win-x64-Laufzeitwiederherstellung ist fehlgeschlagen.' }

& (Join-Path $scriptDirectory 'build-icon.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Das Programmicon konnte nicht erzeugt werden.' }

dotnet test $solutionPath -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Die Tests sind fehlgeschlagen.' }

dotnet build $solutionPath -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Der Release-Build ist fehlgeschlagen.' }

dotnet publish $projectPath -c Release -r win-x64 --self-contained true --no-restore -o $outputPath
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

if ((Get-FileHash -LiteralPath $publishedExecutablePath).Hash -ne (Get-FileHash -LiteralPath $rootExecutablePath).Hash) {
    throw 'Die EXE im Rootverzeichnis stimmt nicht mit dem Publish-Artefakt ueberein.'
}

& $rootExecutablePath --diagnostics | Out-File -LiteralPath $diagnosticPath -Encoding utf8
if ($LASTEXITCODE -ne 0) { throw 'Die Diagnose ist fehlgeschlagen.' }

$diagnostic = Get-Content -LiteralPath $diagnosticPath -Raw | ConvertFrom-Json
if ($diagnostic.application -ne 'Sascha Window Zones') { throw 'Die Diagnose meldet einen unerwarteten Programmnamen.' }
if ($diagnostic.hookRegistered -ne $false) { throw 'Die Diagnose hat unerwartet einen Hook registriert.' }
if ($diagnostic.settingsChanged -ne $false) { throw 'Die Diagnose hat unerwartet Einstellungen verändert.' }
if (@($diagnostic.monitors).Count -lt 1) { throw 'Die Diagnose hat keinen Monitor erkannt.' }

& (Join-Path $scriptDirectory 'verify-dpi-awareness.ps1') -ExecutablePath $rootExecutablePath
if ($LASTEXITCODE -ne 0) { throw 'Die DPI-Prüfung ist fehlgeschlagen.' }

$files = Get-ChildItem -LiteralPath $outputPath -File -Recurse
$bytes = ($files | Measure-Object -Property Length -Sum).Sum
Write-Output "VERIFY_OK tests=passed monitors=$(@($diagnostic.monitors).Count) files=$($files.Count) bytes=$bytes rootExe=$rootExecutablePath hookRegistered=false settingsChanged=false"
