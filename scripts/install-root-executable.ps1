param(
    [Parameter(Mandatory = $true)]
    [string]$PublishedExecutablePath,

    [Parameter(Mandatory = $true)]
    [string]$RootExecutablePath,

    [ValidateRange(1, 60)]
    [int]$ShutdownTimeoutSeconds = 30
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

# QueryFullProcessImageName statt Process.Path: jenes liest den Pfad ueber das Hauptmodul und braucht
# dafuer PROCESS_VM_READ, was ein gewoehnlicher Build-Prozess bei einer erhoeht laufenden Instanz nicht
# bekommt — der Pfad kam leer zurueck, und damit wurde nie eine Instanz gefunden. Diese Abfrage genuegt
# PROCESS_QUERY_LIMITED_INFORMATION und funktioniert ueber die Rechtegrenze hinweg.
if (-not ('SnapZones.Build.ProcessImage' -as [type])) {
    Add-Type -Namespace 'SnapZones.Build' -Name 'ProcessImage' -MemberDefinition @'
[DllImport("kernel32.dll", SetLastError = true)]
public static extern IntPtr OpenProcess(int access, bool inheritHandle, int processId);

[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
public static extern bool QueryFullProcessImageNameW(IntPtr process, int flags, System.Text.StringBuilder name, ref int size);

[DllImport("kernel32.dll")]
public static extern bool CloseHandle(IntPtr handle);
'@
}

function Get-ProcessImagePath([int]$ProcessId) {
    $queryLimitedInformation = 0x1000
    $process = [SnapZones.Build.ProcessImage]::OpenProcess($queryLimitedInformation, $false, $ProcessId)
    if ($process -eq [IntPtr]::Zero) {
        return $null
    }

    try {
        $size = 32768
        $name = New-Object System.Text.StringBuilder $size
        if ([SnapZones.Build.ProcessImage]::QueryFullProcessImageNameW($process, 0, $name, [ref]$size)) {
            return $name.ToString()
        }

        return $null
    }
    finally {
        [void][SnapZones.Build.ProcessImage]::CloseHandle($process)
    }
}

# Windows nennt den Pfad eines Prozesses so, wie der Prozess ihn geoeffnet hat: bei einem verbundenen
# Netzlaufwerk als UNC-Pfad, waehrend der Build denselben Ort ueber den Laufwerksbuchstaben angibt. Ein
# Zeichenvergleich fiele deshalb auseinander; beide Seiten kommen erst auf denselben Nenner.
function Resolve-ComparablePath([string]$Path) {
    $full = [System.IO.Path]::GetFullPath($Path)
    if ($full.Length -ge 2 -and $full[1] -eq ':') {
        $drive = Get-PSDrive -Name $full[0] -PSProvider FileSystem -ErrorAction SilentlyContinue
        if ($drive -and $drive.DisplayRoot) {
            $full = $drive.DisplayRoot.TrimEnd('\') + $full.Substring(2)
        }
    }

    return $full.TrimEnd('\')
}

# Die Prozesse, die aus der Zieldatei laufen. Der Vergleich geht ueber den vollen Pfad, damit eine
# installierte Kopie unter «Programme» nicht mitgemeint ist.
function Get-RunningInstances([string]$Path) {
    $wanted = Resolve-ComparablePath $Path
    $processName = [System.IO.Path]::GetFileNameWithoutExtension($Path)

    # @(...) um die Pipeline liefert auch bei null und bei einem Treffer ein echtes Feld.
    $found = @(Get-Process -Name $processName -ErrorAction SilentlyContinue | Where-Object {
        $imagePath = Get-ProcessImagePath $_.Id
        $imagePath -and (Resolve-ComparablePath $imagePath) -eq $wanted
    })

    # Das Komma haelt das Feld beim Verlassen der Funktion zusammen: ein Feld als Rueckgabewert wird
    # sonst in die Pipeline zerlegt, und aus keinem Treffer wuerde $null statt eines leeren Feldes.
    # Genau daran scheiterte der Aufrufer mit «Die Eigenschaft Count wurde fuer dieses Objekt nicht
    # gefunden». So liefert die Funktion immer ein Feld, und der Aufrufer braucht kein @() darum —
    # eines darum gelegt ergaebe ein Feld, das das leere Feld als einzigen Eintrag enthaelt.
    return ,$found
}

# Bittet eine laufende Instanz ueber ihren eigenen Schalter um ein geordnetes Beenden und wartet.
#
# Eine Single-File-Anwendung laedt viele Bausteine erst bei Bedarf aus der eigenen Programmdatei nach,
# ueber deren Pfad. Wird die Datei unter dem laufenden Prozess weggeschoben, scheitert jedes spaetere
# Nachladen; am 03. und 04.09.2026 stuerzte das Programm dreimal so ab, jeweils Minuten nach einem
# Build. Deshalb wird die Instanz zuerst beendet und nach dem Austausch neu gestartet.
function Stop-RunningInstance([string]$Path, [System.Diagnostics.Process[]]$Instances = @()) {
    if (@($Instances).Count -eq 0) {
        return $true
    }

    Write-Host "Laufende Instanz wird beendet: $($Instances.Id -join ', ')"
    try {
        $request = Start-Process -FilePath $Path -ArgumentList '--exit' -PassThru -WindowStyle Hidden
        $request.WaitForExit([Math]::Min(15000, $ShutdownTimeoutSeconds * 1000)) | Out-Null
    }
    catch {
        Write-Warning "Die Bitte um Beenden liess sich nicht senden: $($_.Exception.Message)"
    }

    $deadline = [DateTime]::UtcNow.AddSeconds($ShutdownTimeoutSeconds)
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

# Eine gemeinsame Sperre verhindert zwei gleichzeitige Austauschvorgaenge im selben Verzeichnis.
# Die Sperrdatei bleibt bestehen; allein der exklusiv geoeffnete Handle sperrt den Zugriff.
$installationLock = [IO.File]::Open(
    (Join-Path $destinationDirectory '.zonemanager-install.lock'), 'OpenOrCreate', 'ReadWrite', 'None')
$artifacts = [Collections.Generic.List[object]]::new()
$transactionId = [guid]::NewGuid().ToString('N')
$applicationPath = Join-Path $destinationDirectory 'ZoneManager.exe'
$helperPath = Join-Path $destinationDirectory 'ZoneManager.Helper.exe'
$restartAfterSwap = $false
$safeToRestart = $true
$committed = $false

try {
    $names = if ($destinationName -eq 'ZoneManager.exe') { $allowedNames } else { @($destinationName) }
    foreach ($name in $names) {
        $source = if ($name -eq $destinationName) { $sourcePath } else { Join-Path ([IO.Path]::GetDirectoryName($sourcePath)) $name }
        if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
            throw "Der Austausch wurde abgebrochen: Quelldatei fehlt: $source"
        }
        $target = Join-Path $destinationDirectory $name
        $artifacts.Add([pscustomobject]@{
            Source = $source; Target = $target
            Temporary = "$target.new.$transactionId"; Backup = "$target.previous.$transactionId"
            Hash = Get-Sha256Hash $source
            OriginalHash = if (Test-Path -LiteralPath $target -PathType Leaf) { Get-Sha256Hash $target } else { $null }
            OriginalMoved = $false; Installed = $false
        })
    }

    # Beide Dateien vollstaendig vorbereiten und pruefen, bevor eine laufende Anwendung beendet wird.
    foreach ($artifact in $artifacts) {
        Copy-Item -LiteralPath $artifact.Source -Destination $artifact.Temporary
        if ((Get-Sha256Hash $artifact.Temporary) -ne $artifact.Hash) {
            throw "Die vorbereitete Datei stimmt nicht mit dem Publish-Artefakt ueberein: $($artifact.Source)"
        }
    }

    # Auch ein separater Helfertausch darf erst nach dem Ende seiner Hauptanwendung erfolgen.
    $instances = Get-RunningInstances $applicationPath
    if ($instances.Count -gt 0) {
        if (-not (Stop-RunningInstance $applicationPath $instances)) {
            throw 'Die laufende Instanz hat sich nicht beendet. Keine Programmdatei wurde ersetzt.'
        }
        $restartAfterSwap = $true
    }

    $deadline = [DateTime]::UtcNow.AddSeconds(5)
    do {
        $helpers = Get-RunningInstances $helperPath
        if ($helpers.Count -eq 0) { break }
        Start-Sleep -Milliseconds 100
    } while ([DateTime]::UtcNow -lt $deadline)
    if ($helpers.Count -gt 0 -or (Get-RunningInstances $applicationPath).Count -gt 0) {
        throw 'Anwendung oder Fensterhelfer laeuft noch. Keine Programmdatei wurde ersetzt.'
    }

    $safeToRestart = $false
    foreach ($artifact in $artifacts) {
        if (Test-Path -LiteralPath $artifact.Target -PathType Leaf) {
            Move-Item -LiteralPath $artifact.Target -Destination $artifact.Backup
            $artifact.OriginalMoved = $true
        }
        Move-Item -LiteralPath $artifact.Temporary -Destination $artifact.Target
        $artifact.Installed = $true
        if ((Get-Sha256Hash $artifact.Target) -ne $artifact.Hash) {
            throw "Die Datei stimmt nach dem Austausch nicht mit dem Publish-Artefakt ueberein: $($artifact.Target)"
        }
    }
    $committed = $true
    $safeToRestart = $true
}
catch {
    $installationError = $_
    $rollbackErrors = [Collections.Generic.List[string]]::new()
    # Rueckwaerts zuruecksetzen: Sicherungen bleiben erhalten, falls auch die Ruecknahme scheitert.
    for ($index = $artifacts.Count - 1; $index -ge 0; $index--) {
        $artifact = $artifacts[$index]
        try {
            if ($artifact.Installed) {
                Remove-Item -LiteralPath $artifact.Target -Force -Confirm:$false
            }
            if ($artifact.OriginalMoved) {
                Move-Item -LiteralPath $artifact.Backup -Destination $artifact.Target
                if ((Get-Sha256Hash $artifact.Target) -ne $artifact.OriginalHash) {
                    throw 'Die wiederhergestellte Datei hat eine abweichende Pruefsumme.'
                }
            }
        }
        catch { $rollbackErrors.Add("$($artifact.Target): $($_.Exception.Message)") }
    }
    $safeToRestart = $rollbackErrors.Count -eq 0
    if (-not $safeToRestart) {
        throw "Austausch fehlgeschlagen: $installationError; Ruecknahme unvollstaendig: $($rollbackErrors -join '; '); Sicherungen: $destinationDirectory\*.previous.$transactionId"
    }
    throw $installationError
}
finally {
    foreach ($artifact in $artifacts) {
        $cleanup = @($artifact.Temporary)
        if ($committed) { $cleanup += $artifact.Backup }
        foreach ($path in $cleanup) {
            if (Test-Path -LiteralPath $path -PathType Leaf) {
                try { Remove-Item -LiteralPath $path -Force -Confirm:$false }
                catch { Write-Warning "Datei bleibt erhalten: $path" }
            }
        }
    }
    try {
        if ($restartAfterSwap -and $safeToRestart) {
            try {
                Start-Process -FilePath $applicationPath -ArgumentList '--autostart' `
                    -WorkingDirectory $destinationDirectory -WindowStyle Hidden | Out-Null
                Write-Host 'Die zuvor laufende Instanz wurde neu gestartet.'
            }
            catch { Write-Warning "Die Anwendung muss manuell gestartet werden: $($_.Exception.Message)" }
        }
    }
    finally { $installationLock.Dispose() }
}

$bytes = (Get-Item -LiteralPath $destinationPath).Length
$hash = Get-Sha256Hash $destinationPath
Write-Output "ROOT_EXE_UPDATED path=$destinationPath bytes=$bytes sha256=$hash"
