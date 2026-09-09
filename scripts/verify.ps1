param(
    [switch]$SkipDpiCheck
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $scriptDirectory '..'))
$solutionPath = Join-Path $projectRoot 'ZoneManager.sln'
$projectPath = Join-Path $projectRoot 'src\SnapZones.App\SnapZones.App.csproj'
$helperProjectPath = Join-Path $projectRoot 'src\SnapZones.Helper\SnapZones.Helper.csproj'
# Der Publish-Ordner des App-Projekts; derselbe Pfad steht als RootPublishDirectory in der Projektdatei.
$publishDirectory = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'obj\root-publish\Release\win-x64'))
$rootExecutablePath = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'ZoneManager.exe'))
$rootHelperPath = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'ZoneManager.Helper.exe'))
$diagnosticPath = Join-Path $projectRoot 'outputs\zonemanager-diagnostics.json'
$maximumExecutableBytes = 100000000
$stepDurations = [ordered]@{}

<#
.SYNOPSIS
    Fuehrt einen Schritt aus und haelt fest, wie lange er gedauert hat.

.DESCRIPTION
    Der Lauf dauert Minuten, und ohne Messung ist nicht zu sehen, welcher Schritt sie verbraucht. Jede
    Zeile «STEP …» nennt die Dauer sofort, die Zeile «VERIFY_TIMING» am Ende die Rangliste.
#>
function Invoke-Step {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][scriptblock]$Action
    )

    $watch = [System.Diagnostics.Stopwatch]::StartNew()
    & $Action
    $watch.Stop()
    $script:stepDurations[$Name] = $watch.Elapsed.TotalSeconds
    Write-Output ('STEP {0} {1:n1}s' -f $Name, $watch.Elapsed.TotalSeconds)
}

Invoke-Step 'restore' {
    dotnet restore $solutionPath
    if ($LASTEXITCODE -ne 0) { throw 'Die Paketwiederherstellung ist fehlgeschlagen.' }

    dotnet restore $projectPath -r win-x64
    if ($LASTEXITCODE -ne 0) { throw 'Die win-x64-Laufzeitwiederherstellung ist fehlgeschlagen.' }

    dotnet restore $helperProjectPath -r win-x64
    if ($LASTEXITCODE -ne 0) { throw 'Die win-x64-Laufzeitwiederherstellung des Fensterhelfers ist fehlgeschlagen.' }
}

# Die aufgerufenen Skripte melden Fehler ueber eine terminierende Ausnahme; $LASTEXITCODE bliebe hier
# auf dem Wert des zuletzt gestarteten nativen Befehls stehen und waere deshalb keine gueltige Pruefung.
Invoke-Step 'icon' { & (Join-Path $scriptDirectory 'build-icon.ps1') }

# Erst bauen, dann testen: mit «--no-build» uebersetzt «dotnet test» die Projektmappe nicht ein zweites
# Mal. Beide Schritte brauchen die Root-EXE nicht; ohne den Schalter loeste jeder Build des App-Projekts
# einen vollstaendigen Self-contained-Publish aus.
Invoke-Step 'build' {
    dotnet build $solutionPath -c Release --no-restore -p:SkipRootExecutablePublish=true
    if ($LASTEXITCODE -ne 0) { throw 'Der Release-Build ist fehlgeschlagen.' }
}

Invoke-Step 'tests' {
    dotnet test $solutionPath -c Release --no-restore --no-build -p:SkipRootExecutablePublish=true
    if ($LASTEXITCODE -ne 0) { throw 'Die Tests sind fehlgeschlagen.' }
}

Invoke-Step 'installationstests' { & (Join-Path $scriptDirectory 'test-root-installation.ps1') }

# Das Installationspaket selbst entsteht erst beim Release, seine Versionsabbildung wird aber hier
# geprueft: sie kostet nichts und entscheidet darueber, ob Windows ein Update als solches erkennt.
Invoke-Step 'installer-version' { & (Join-Path $scriptDirectory 'test-installer-version.ps1') }

<#
.SYNOPSIS
    Baut die auslieferbaren Dateien und legt sie ins Rootverzeichnis.

.DESCRIPTION
    Ein gewoehnlicher Build des App-Projekts veroeffentlicht Programmdatei und Fensterhelfer als
    selbstaendige Einzeldateien und laesst sie von install-root-executable.ps1 ins Rootverzeichnis
    legen. Genau das passiert hier, mit denselben Dateien, die danach ins Release gehen.

    Bis zum 09.09.2026 lief dieser Publish dreimal je Prueflauf: einmal fuer eine Artefaktpruefung in
    einem Wegwerfverzeichnis, einmal ausdruecklich nach outputs\ und einmal beim Kopieren ins
    Rootverzeichnis. Jeder Durchgang erzeugte dieselben 70 MB und schob sie ueber das Netzlaufwerk;
    zusammen waren das gut anderthalb Minuten fuer ein Ergebnis, das schon vorlag.
#>
Invoke-Step 'publish' {
    dotnet build $projectPath -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Der Publish ist fehlgeschlagen.' }
}

foreach ($artifact in @(
        @{ Path = $rootExecutablePath; Name = 'ZoneManager.exe' },
        @{ Path = $rootHelperPath; Name = 'ZoneManager.Helper.exe' })) {
    if (-not (Test-Path -LiteralPath $artifact.Path -PathType Leaf)) {
        throw "$($artifact.Name) fehlt im Rootverzeichnis."
    }
}

$rootExecutableBytes = (Get-Item -LiteralPath $rootExecutablePath).Length
if ($rootExecutableBytes -gt $maximumExecutableBytes) {
    throw "Die veröffentlichte EXE ist mit $rootExecutableBytes Bytes grösser als das erlaubte Maximum von $maximumExecutableBytes Bytes."
}

# In einer Fernsitzung zeigt Windows dem Prozess nur die Platzhalteranzeige der Sitzung; die echten
# Monitore gehoeren zur getrennten Konsolensitzung. Die Diagnose meldet dann keinen Monitor und
# endet mit Rueckgabewert 2 -- kein Fehler des Programms, aber auch keine pruefbare Monitorsicht.
Add-Type -AssemblyName System.Windows.Forms
$remoteSession = [System.Windows.Forms.SystemInformation]::TerminalServerSession

# Der Diagnoselauf beweist zugleich, dass die eben gebaute Einzeldatei selbstaendig laeuft.
Invoke-Step 'diagnose' { & $rootExecutablePath --diagnostics | Out-File -LiteralPath $diagnosticPath -Encoding utf8 }
$diagnosticExitCode = $LASTEXITCODE
if ($diagnosticExitCode -ne 0 -and -not ($diagnosticExitCode -eq 2 -and $remoteSession)) {
    throw "Die Diagnose ist fehlgeschlagen (Rückgabewert $diagnosticExitCode)."
}

$diagnostic = Get-Content -LiteralPath $diagnosticPath -Raw | ConvertFrom-Json
if ($diagnostic.application -ne "Zone Manager") { throw 'Die Diagnose meldet einen unerwarteten Programmnamen.' }
if ($diagnostic.hookRegistered -ne $false) { throw 'Die Diagnose hat unerwartet einen Hook registriert.' }
if ($diagnostic.settingsChanged -ne $false) { throw 'Die Diagnose hat unerwartet Einstellungen verändert.' }
if (@($diagnostic.monitors).Count -lt 1 -and -not $remoteSession) { throw 'Die Diagnose hat keinen Monitor erkannt.' }
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
$frameOutput = $null
Invoke-Step 'fensterrand' { $script:frameOutput = & (Join-Path $scriptDirectory 'measure-window-frame.ps1') }
$frameOutput | Write-Output
$frameLine = @($frameOutput | Where-Object { $_ -is [string] -and $_ -match '^FRAME_(OK|SKIPPED)' })
$frameStatus = if ($frameLine.Count -gt 0 -and $frameLine[-1] -match '^FRAME_OK.*largest=(\d+)') {
    "measured-$($Matches[1])px"
}
else {
    'skipped'
}

$files = Get-ChildItem -LiteralPath $publishDirectory -File -Recurse
$bytes = ($files | Measure-Object -Property Length -Sum).Sum
$total = ($stepDurations.Values | Measure-Object -Sum).Sum
$ranking = ($stepDurations.GetEnumerator() |
    Sort-Object -Property Value -Descending |
    ForEach-Object { '{0}={1:n1}s' -f $_.Key, $_.Value }) -join ' '
Write-Output ("VERIFY_TIMING total={0:n1}s {1}" -f $total, $ranking)
Write-Output "VERIFY_OK tests=passed rootBuild=passed dpi=$dpiStatus windowFrame=$frameStatus monitors=$(@($diagnostic.monitors).Count) startupLayouts=$($diagnostic.startupLayoutCount) files=$($files.Count) bytes=$bytes maximumExecutableBytes=$maximumExecutableBytes rootExe=$rootExecutablePath rootHelper=$rootHelperPath hookRegistered=false settingsChanged=false"
