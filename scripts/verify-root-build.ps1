param(
    [long]$MaximumExecutableBytes = 100000000
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $scriptDirectory '..'))
$projectPath = Join-Path $projectRoot 'src\SnapZones.App\SnapZones.App.csproj'
$workRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'work'))
$testDirectory = [System.IO.Path]::GetFullPath((Join-Path $workRoot 'root-build-verification'))
$testExecutable = Join-Path $testDirectory 'ZoneManager.exe'
$diagnosticPath = Join-Path $testDirectory 'diagnostics.json'

if (-not $testDirectory.StartsWith($workRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw 'Der Testordner liegt ausserhalb des Arbeitsordners.'
}

if (Test-Path -LiteralPath $testDirectory) {
    Remove-Item -LiteralPath $testDirectory -Recurse -Force
}

New-Item -ItemType Directory -Path $testDirectory | Out-Null

try {
    dotnet build $projectPath -c Release --no-restore -p:RootExecutablePath=$testExecutable
    if ($LASTEXITCODE -ne 0) { throw 'Der Build für die Root-Artefaktprüfung ist fehlgeschlagen.' }

    if (-not (Test-Path -LiteralPath $testExecutable -PathType Leaf)) {
        throw 'Der normale Build hat keine ZoneManager.exe am vorgegebenen Root-Pfad erzeugt.'
    }

    & $testExecutable --diagnostics | Out-File -LiteralPath $diagnosticPath -Encoding utf8
    if ($LASTEXITCODE -ne 0) { throw 'Die vom normalen Build erzeugte Root-EXE ist nicht selbständig ausführbar.' }

    $diagnostic = Get-Content -Raw -LiteralPath $diagnosticPath | ConvertFrom-Json
    if ($diagnostic.application -ne "Sascha’s Zone Manager") {
        throw 'Die Root-EXE meldet einen unerwarteten Programmnamen.'
    }

    if ($diagnostic.startupConfigurationReady -ne $true) {
        throw 'Die Root-EXE kann keine leere Startkonfiguration initialisieren.'
    }

    if ([int]$diagnostic.startupLayoutCount -ne @($diagnostic.monitors).Count) {
        throw 'Die Root-EXE hat nicht für jeden erkannten Monitor ein Startlayout erzeugt.'
    }

    $bytes = (Get-Item -LiteralPath $testExecutable).Length
    if ($bytes -gt $MaximumExecutableBytes) {
        throw "Die Root-EXE ist mit $bytes Bytes grösser als das erlaubte Maximum von $MaximumExecutableBytes Bytes."
    }

    $hash = (Get-FileHash -LiteralPath $testExecutable -Algorithm SHA256).Hash
    Write-Output "ROOT_BUILD_OK path=$testExecutable bytes=$bytes maximumBytes=$MaximumExecutableBytes sha256=$hash"
}
finally {
    if (Test-Path -LiteralPath $testDirectory) {
        Remove-Item -LiteralPath $testDirectory -Recurse -Force
    }
}
