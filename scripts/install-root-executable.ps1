param(
    [Parameter(Mandatory = $true)]
    [string]$PublishedExecutablePath,

    [Parameter(Mandatory = $true)]
    [string]$RootExecutablePath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

function Get-Sha256Hash([string]$Path) {
    $stream = [System.IO.File]::OpenRead($Path)
    try {
        $algorithm = [System.Security.Cryptography.SHA256]::Create()
        try {
            return [System.BitConverter]::ToString($algorithm.ComputeHash($stream)).Replace('-', '')
        }
        finally {
            $algorithm.Dispose()
        }
    }
    finally {
        $stream.Dispose()
    }
}

# Die Prozesse, die aus der Zieldatei laufen. Der Vergleich geht ueber den vollen Pfad, damit eine
# installierte Kopie unter «Programme» nicht mitgemeint ist.
function Get-RunningInstances([string]$Path) {
    return @(Get-Process -ErrorAction SilentlyContinue | Where-Object {
        try { $_.Path -and $_.Path.Equals($Path, [System.StringComparison]::OrdinalIgnoreCase) }
        catch { $false }
    })
}

# Bittet eine laufende Instanz ueber ihren eigenen Schalter um ein geordnetes Beenden und wartet.
#
# Eine Single-File-Anwendung laedt viele Bausteine erst bei Bedarf aus der eigenen Programmdatei nach,
# ueber deren Pfad. Wird die Datei unter dem laufenden Prozess weggeschoben, scheitert jedes spaetere
# Nachladen; am 03. und 04.09.2026 stuerzte das Programm dreimal so ab, jeweils Minuten nach einem
# Build. Deshalb wird die Instanz zuerst beendet und nach dem Austausch neu gestartet.
function Stop-RunningInstance([string]$Path, [System.Diagnostics.Process[]]$Instances) {
    if ($Instances.Count -eq 0) {
        return $true
    }

    Write-Host "Laufende Instanz wird beendet: $($Instances.Id -join ', ')"
    try {
        $request = Start-Process -FilePath $Path -ArgumentList '--exit' -PassThru -WindowStyle Hidden
        $request.WaitForExit(15000) | Out-Null
    }
    catch {
        Write-Warning "Die Bitte um Beenden liess sich nicht senden: $($_.Exception.Message)"
    }

    $deadline = [DateTime]::UtcNow.AddSeconds(30)
    while ([DateTime]::UtcNow -lt $deadline) {
        $alive = @($Instances | Where-Object { -not $_.HasExited })
        if ($alive.Count -eq 0) {
            return $true
        }

        Start-Sleep -Milliseconds 250
    }

    return $false
}

$scriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectRoot = [System.IO.Path]::GetFullPath((Join-Path $scriptDirectory '..'))
$workRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot 'work'))
$sourcePath = [System.IO.Path]::GetFullPath($PublishedExecutablePath)
$destinationPath = [System.IO.Path]::GetFullPath($RootExecutablePath)
$destinationDirectory = [System.IO.Path]::GetDirectoryName($destinationPath)
# Das Skript taugt fuer die Programmdatei und fuer den Fensterhelfer; beide liegen nebeneinander und
# werden nach demselben Muster ausgetauscht. Ein anderer Name deutet auf einen Aufruffehler hin.
$allowedNames = @('ZoneManager.exe', 'ZoneManager.Helper.exe')
$destinationName = [System.IO.Path]::GetFileName($destinationPath)
$isTestDestination = $destinationPath.StartsWith(
    $workRoot + [System.IO.Path]::DirectorySeparatorChar,
    [System.StringComparison]::OrdinalIgnoreCase)

if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) {
    throw "Die veröffentlichte EXE fehlt: $sourcePath"
}

if ($allowedNames -notcontains $destinationName) {
    throw 'Der Name der Root-EXE ist unerwartet.'
}

$defaultDestination = [System.IO.Path]::GetFullPath((Join-Path $projectRoot $destinationName))
if (-not $destinationPath.Equals($defaultDestination, [System.StringComparison]::OrdinalIgnoreCase) -and -not $isTestDestination) {
    throw 'Der Zielpfad liegt weder im Root- noch im Testverzeichnis.'
}

if (-not (Test-Path -LiteralPath $destinationDirectory -PathType Container)) {
    New-Item -ItemType Directory -Path $destinationDirectory | Out-Null
}

# Nur die Programmdatei hat eine Instanz, die man bitten kann; der Fensterhelfer endet mit ihr.
$restartAfterSwap = $false
if ($destinationName -eq 'ZoneManager.exe' -and (Test-Path -LiteralPath $destinationPath -PathType Leaf)) {
    $instances = Get-RunningInstances $destinationPath
    if ($instances.Count -gt 0) {
        if (Stop-RunningInstance $destinationPath $instances) {
            $restartAfterSwap = $true
        }
        else {
            Write-Warning 'Die laufende Instanz hat sich nicht beendet. Die Datei wird trotzdem ersetzt; die Instanz erkennt den Austausch und startet sich selbst neu.'
        }
    }
}

$temporaryPath = "$destinationPath.new"
$backupPath = "$destinationPath.previous.$([DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()).$PID"
$destinationWasMoved = $false

try {
    Copy-Item -LiteralPath $sourcePath -Destination $temporaryPath -Force
    if ((Get-Sha256Hash $sourcePath) -ne (Get-Sha256Hash $temporaryPath)) {
        throw 'Die vorbereitete Root-EXE stimmt nicht mit dem Publish-Artefakt überein.'
    }

    if (Test-Path -LiteralPath $destinationPath -PathType Leaf) {
        Move-Item -LiteralPath $destinationPath -Destination $backupPath
        $destinationWasMoved = $true
    }

    try {
        Move-Item -LiteralPath $temporaryPath -Destination $destinationPath
    }
    catch {
        if ($destinationWasMoved -and -not (Test-Path -LiteralPath $destinationPath)) {
            Move-Item -LiteralPath $backupPath -Destination $destinationPath
            $destinationWasMoved = $false
        }

        throw
    }

    if ((Get-Sha256Hash $sourcePath) -ne (Get-Sha256Hash $destinationPath)) {
        throw 'Die Root-EXE stimmt nach dem Austausch nicht mit dem Publish-Artefakt überein.'
    }
}
finally {
    if (Test-Path -LiteralPath $temporaryPath -PathType Leaf) {
        Remove-Item -LiteralPath $temporaryPath -Force
    }
}

if ($destinationWasMoved -and (Test-Path -LiteralPath $backupPath -PathType Leaf)) {
    try {
        Remove-Item -LiteralPath $backupPath -Force
    }
    catch {
        Write-Warning "Die noch laufende Vorgängerversion bleibt bis zum nächsten Start erhalten: $backupPath"
    }
}

# Der Fensterhelfer gehoert zur Programmdatei und wird im selben Zug ersetzt, solange keine Instanz
# laeuft, die ihn gerade benutzt. Erst danach darf die Instanz wieder starten.
if ($destinationName -eq 'ZoneManager.exe') {
    $sourceHelper = Join-Path ([System.IO.Path]::GetDirectoryName($sourcePath)) 'ZoneManager.Helper.exe'
    if (Test-Path -LiteralPath $sourceHelper -PathType Leaf) {
        try {
            Copy-Item -LiteralPath $sourceHelper -Destination (Join-Path $destinationDirectory 'ZoneManager.Helper.exe') -Force
        }
        catch {
            Write-Warning "Der Fensterhelfer liess sich nicht ersetzen: $($_.Exception.Message)"
        }
    }
}

if ($restartAfterSwap) {
    # Die zuvor beendete Instanz laeuft mit dem neuen Stand weiter, still im Infobereich.
    Start-Process -FilePath $destinationPath -ArgumentList '--autostart' -WorkingDirectory $destinationDirectory | Out-Null
    Write-Host 'Die zuvor laufende Instanz wurde mit dem neuen Stand neu gestartet.'
}

$bytes = (Get-Item -LiteralPath $destinationPath).Length
$hash = Get-Sha256Hash $destinationPath
Write-Output "ROOT_EXE_UPDATED path=$destinationPath bytes=$bytes sha256=$hash"
