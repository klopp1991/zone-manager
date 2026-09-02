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

    # Der Zustand der Snap-Funktion wird nicht gespeichert; er lebt nur zur Laufzeit in SnappingState.
    # Der gespeicherte Schalter SnappingEnabled ist damit laengst weg, und unter Set-StrictMode war der
    # blosse Zugriff darauf ein Abbruch -- verify.ps1 kam nie ueber diese Zeile hinaus. Die Pruefung
    # bleibt trotzdem stehen: taucht der Wert je wieder auf, greift sie wie gedacht.
    if ($null -ne $settings -and
        ($settings.PSObject.Properties.Name -contains 'SnappingEnabled') -and
        $settings.SnappingEnabled -eq $true) {
        throw "Der DPI-Test startet Zone Manager nur bei ausgeschalteter Snap-Funktion."
    }
}

if (-not ('SnapZones.ProcessDpiProbe' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;

namespace SnapZones
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public static class ProcessDpiProbe
    {
        [DllImport("shcore.dll")]
        public static extern int GetProcessDpiAwareness(IntPtr processHandle, out int awareness);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr window, out Rect rectangle);

        [DllImport("dwmapi.dll")]
        public static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out Rect value, int size);

        // DWMWA_EXTENDED_FRAME_BOUNDS
        public const int ExtendedFrameBounds = 9;
    }
}
'@
}

$process = Start-Process -FilePath $resolvedExecutable -PassThru
try {
    [void]$process.WaitForInputIdle(5000)
    if ($process.HasExited) {
        throw "Zone Manager wurde vor dem DPI-Test beendet: ExitCode $($process.ExitCode)"
    }

    $awareness = -1
    $result = [SnapZones.ProcessDpiProbe]::GetProcessDpiAwareness($process.Handle, [ref]$awareness)
    if ($result -ne 0) {
        throw "GetProcessDpiAwareness ist fehlgeschlagen: HRESULT $result"
    }

    if ($awareness -ne 2) {
        throw "Zone Manager ist nicht pro Monitor DPI-bewusst: PROCESS_DPI_AWARENESS=$awareness"
    }

    Write-Output 'DPI_OK awareness=PerMonitor'

    # Der unsichtbare Fensterrand, gemessen an einem wirklich dargestellten Fenster eines pro Monitor
    # DPI-bewussten Prozesses. Nur hier ist die Messung gueltig: ein nicht DPI-bewusster Prozess bekommt
    # von GetWindowRect virtualisierte und vom Desktop Window Manager echte Koordinaten, und die
    # Differenz waere reine Skalierung statt Rahmen. Von diesem Wert haengen der Ausgleich in
    # WindowFrameCompensation und die Toleranz in MainZoneFallback ab.
    $maximumBorderPixels = 40
    $handle = [IntPtr]::Zero
    $deadline = (Get-Date).AddSeconds(10)
    while ((Get-Date) -lt $deadline) {
        $current = Get-Process -Id $process.Id -ErrorAction SilentlyContinue
        if ($null -ne $current -and $null -ne $current.MainWindowHandle -and $current.MainWindowHandle -ne [IntPtr]::Zero) {
            $handle = $current.MainWindowHandle
            break
        }

        Start-Sleep -Milliseconds 250
    }

    if ($handle -eq [IntPtr]::Zero) {
        Write-Output 'FRAME_SKIPPED reason=no-main-window'
    }
    else {
        # Der Desktop Window Manager meldet den sichtbaren Rahmen erst am zusammengesetzten Fenster.
        Start-Sleep -Milliseconds 600
        $windowRect = New-Object SnapZones.Rect
        $frameRect = New-Object SnapZones.Rect
        $gotWindow = [SnapZones.ProcessDpiProbe]::GetWindowRect($handle, [ref]$windowRect)
        $frameResult = [SnapZones.ProcessDpiProbe]::DwmGetWindowAttribute(
            $handle,
            [SnapZones.ProcessDpiProbe]::ExtendedFrameBounds,
            [ref]$frameRect,
            [System.Runtime.InteropServices.Marshal]::SizeOf($frameRect))

        if (-not $gotWindow -or $frameResult -ne 0) {
            Write-Output 'FRAME_SKIPPED reason=not-composed'
        }
        else {
            $left = $frameRect.Left - $windowRect.Left
            $top = $frameRect.Top - $windowRect.Top
            $right = $windowRect.Right - $frameRect.Right
            $bottom = $windowRect.Bottom - $frameRect.Bottom
            $largest = ($left, $top, $right, $bottom | Measure-Object -Maximum).Maximum

            if ($left -lt 0 -or $top -lt 0 -or $right -lt 0 -or $bottom -lt 0) {
                throw "Der sichtbare Rahmen liegt nicht im Fensterrechteck: l=$left t=$top r=$right b=$bottom"
            }

            if ($largest -gt $maximumBorderPixels) {
                throw "Der gemessene unsichtbare Rand von $largest px ueberschreitet die angenommenen $maximumBorderPixels px."
            }

            Write-Output "FRAME_OK left=$left top=$top right=$right bottom=$bottom largest=$largest limit=$maximumBorderPixels"
        }
    }
}
finally {
    if (-not $process.HasExited) {
        Stop-Process -Id $process.Id -ErrorAction Stop
    }
}
