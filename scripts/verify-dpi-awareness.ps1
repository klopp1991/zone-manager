param(
    [Parameter(Mandatory = $true)]
    [string]$ExecutablePath,

    [ValidateRange(5, 600)]
    [int]$StartupTimeoutSeconds = 30
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$resolvedExecutable = [System.IO.Path]::GetFullPath($ExecutablePath)
if (-not (Test-Path -LiteralPath $resolvedExecutable -PathType Leaf)) {
    throw "Die ausführbare Datei fehlt: $resolvedExecutable"
}

$settingsPath = Join-Path $env:APPDATA 'ZoneManager\settings.json'
if (Test-Path -LiteralPath $settingsPath) {
    $settings = (Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json).Settings
    if ($settings.SnappingEnabled -eq $true) {
        throw "Der DPI-Test startet Sascha’s Zone Manager nur bei ausgeschalteter Snap-Funktion."
    }
}

if (-not ('ZoneManager.ProcessDpiProbe' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

namespace ZoneManager
{
    public static class ProcessDpiProbe
    {
        [DllImport("shcore.dll")]
        public static extern int GetProcessDpiAwareness(IntPtr processHandle, out int awareness);
    }
}
'@
}

$process = Start-Process -FilePath $resolvedExecutable -PassThru
try {
    # Begrenzte Wartezeit: ohne sie bleibt der Prüflauf bei einer offenen Rechteabfrage unbegrenzt stehen.
    $deadline = (Get-Date).AddSeconds($StartupTimeoutSeconds)
    $ready = $false
    while (-not $ready -and (Get-Date) -lt $deadline) {
        if ($process.HasExited) {
            throw "Sascha’s Zone Manager wurde vor dem DPI-Test beendet: ExitCode $($process.ExitCode)"
        }

        $ready = $process.WaitForInputIdle(1000)
    }

    if (-not $ready) {
        throw ("Sascha’s Zone Manager war innerhalb von $StartupTimeoutSeconds Sekunden nicht bedienbereit. " +
            'Bleibt eine Abfrage der Benutzerkontensteuerung unbeantwortet, die Prüfung in einer interaktiven Sitzung wiederholen ' +
            'oder verify.ps1 mit -SkipDpiCheck aufrufen.')
    }

    $awareness = -1
    $result = [ZoneManager.ProcessDpiProbe]::GetProcessDpiAwareness($process.Handle, [ref]$awareness)
    if ($result -ne 0) {
        throw "GetProcessDpiAwareness ist fehlgeschlagen: HRESULT $result"
    }

    if ($awareness -ne 2) {
        throw "Sascha’s Zone Manager ist nicht pro Monitor DPI-bewusst: PROCESS_DPI_AWARENESS=$awareness"
    }

    Write-Output 'DPI_OK awareness=PerMonitor'
}
finally {
    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -ErrorAction Stop
    }
}
