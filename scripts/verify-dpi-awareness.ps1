param(
    [Parameter(Mandatory = $true)]
    [string]$ExecutablePath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$resolvedExecutable = [System.IO.Path]::GetFullPath($ExecutablePath)
if (-not (Test-Path -LiteralPath $resolvedExecutable -PathType Leaf)) {
    throw "Die ausführbare Datei fehlt: $resolvedExecutable"
}

$settingsPath = Join-Path $env:APPDATA 'SnapZones\settings.json'
if (Test-Path -LiteralPath $settingsPath) {
    $settings = (Get-Content -LiteralPath $settingsPath -Raw | ConvertFrom-Json).Settings
    if ($settings.SnappingEnabled -eq $true) {
        throw 'Der DPI-Test startet Sascha Window Zones nur bei ausgeschalteter Snap-Funktion.'
    }
}

if (-not ('SnapZones.ProcessDpiProbe' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

namespace SnapZones
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
    [void]$process.WaitForInputIdle(5000)
    if ($process.HasExited) {
        throw "Sascha Window Zones wurde vor dem DPI-Test beendet: ExitCode $($process.ExitCode)"
    }

    $awareness = -1
    $result = [SnapZones.ProcessDpiProbe]::GetProcessDpiAwareness($process.Handle, [ref]$awareness)
    if ($result -ne 0) {
        throw "GetProcessDpiAwareness ist fehlgeschlagen: HRESULT $result"
    }

    if ($awareness -ne 2) {
        throw "Sascha Window Zones ist nicht pro Monitor DPI-bewusst: PROCESS_DPI_AWARENESS=$awareness"
    }

    Write-Output 'DPI_OK awareness=PerMonitor'
}
finally {
    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -ErrorAction Stop
    }
}
