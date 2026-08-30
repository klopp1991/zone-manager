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

$process = Start-Process -FilePath $resolvedExecutable -ArgumentList '--dpi-probe' -PassThru -WindowStyle Hidden
try {
    $deadline = [DateTime]::UtcNow.AddSeconds(5)
    while ($process.HasExited -eq $false -and [DateTime]::UtcNow -lt $deadline) {
        try {
            $null = $process.Handle
            break
        }
        catch {
            Start-Sleep -Milliseconds 50
        }
    }
    if ($process.HasExited -or [DateTime]::UtcNow -ge $deadline) {
        $exitDescription = if ($process.HasExited) { "ExitCode $($process.ExitCode)" } else { 'läuft noch' }
        throw "Der DPI-Probeprozess wurde nicht rechtzeitig bereit: $exitDescription"
    }

    $awareness = -1
    $result = [SnapZones.ProcessDpiProbe]::GetProcessDpiAwareness($process.Handle, [ref]$awareness)
    if ($result -ne 0) {
        throw "GetProcessDpiAwareness ist fehlgeschlagen: HRESULT $result"
    }

    if ($awareness -ne 2) {
        throw "Sascha Window Zones ist nicht pro Monitor DPI-bewusst: PROCESS_DPI_AWARENESS=$awareness"
    }

    if (-not $process.WaitForExit(10000)) {
        throw 'Der DPI-Probeprozess hat das Zeitlimit überschritten.'
    }
    if ($process.ExitCode -ne 0) {
        throw "Der DPI-Probeprozess wurde mit ExitCode $($process.ExitCode) beendet."
    }

    Write-Output 'DPI_OK awareness=PerMonitor exitCode=0'
}
finally {
    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -ErrorAction SilentlyContinue
    }
}
