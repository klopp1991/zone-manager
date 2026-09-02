<#
.SYNOPSIS
    Misst den unsichtbaren Fensterrand an einem echten, fremden Fenster.

.DESCRIPTION
    Windows gibt Fenstern mit veraenderbarer Groesse einen Griffbereich zum Ziehen, der zum
    Fensterrechteck aus GetWindowRect zaehlt, aber nicht gezeichnet wird. Sichtbar ist nur der Rahmen
    aus DWMWA_EXTENDED_FRAME_BOUNDS. Von der Differenz haengen zwei Dinge ab: der Ausgleich beim
    Einrasten in WindowFrameCompensation und die Toleranz, mit der MainZoneFallback ein eingerastetes
    Fenster erkennt. Beide nehmen hoechstens MaximumBorderPixels an.

    Gemessen wird ein bereits offenes Fenster eines anderen Programms -- genau solche positioniert die
    Anwendung. Nichts wird gestartet, geschlossen oder verschoben; das Skript liest ausschliesslich.

    Der eigene Prozess wird zuvor pro Monitor DPI-bewusst gemacht. Ohne das liefert GetWindowRect
    virtualisierte und der Desktop Window Manager echte Koordinaten, und die gemessene Differenz waere
    reine Skalierung statt Rahmen.

.PARAMETER MaximumBorderPixels
    Die angenommene Obergrenze aus WindowFrameCompensation.MaximumBorderPixels.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File scripts\measure-window-frame.ps1
#>
[CmdletBinding()]
param(
    [int] $MaximumBorderPixels = 40
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not ('ZoneManager.FrameProbe' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ZoneManager
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    public sealed class WindowInfo
    {
        public IntPtr Handle;
        public int ProcessId;
        public string Title;
        public Rect WindowRect;
        public Rect VisibleFrame;
    }

    public static class FrameProbe
    {
        private delegate bool EnumWindowsProc(IntPtr window, IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr window);

        [DllImport("user32.dll")]
        private static extern bool IsZoomed(IntPtr window);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int GetWindowLong(IntPtr window, int index);

        [DllImport("user32.dll")]
        private static extern int GetWindowThreadProcessId(IntPtr window, out int processId);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr window, [Out] char[] text, int count);

        [DllImport("user32.dll")]
        public static extern bool GetWindowRect(IntPtr window, out Rect rectangle);

        [DllImport("dwmapi.dll")]
        public static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out Rect value, int size);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr context);

        private const int GwlStyle = -16;
        private const int WsThickFrame = 0x00040000;
        private const int WsCaption = 0x00C00000;
        private const int ExtendedFrameBounds = 9;

        // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2
        private static readonly IntPtr PerMonitorAwareV2 = new IntPtr(-4);

        public static bool MakeDpiAware()
        {
            return SetProcessDpiAwarenessContext(PerMonitorAwareV2);
        }

        /// <summary>
        /// Sichtbare, nicht minimierte und nicht maximierte Fenster fremder Prozesse mit Titelleiste und
        /// veraenderbarer Groesse -- also genau die Fenster, die die Anwendung einrasten laesst.
        /// </summary>
        public static List<WindowInfo> Candidates()
        {
            var ownProcessId = System.Diagnostics.Process.GetCurrentProcess().Id;
            var found = new List<WindowInfo>();
            EnumWindows((window, parameter) =>
            {
                if (!IsWindowVisible(window) || IsIconic(window) || IsZoomed(window))
                {
                    return true;
                }

                var style = GetWindowLong(window, GwlStyle);
                if ((style & WsThickFrame) == 0 || (style & WsCaption) == 0)
                {
                    return true;
                }

                int processId;
                GetWindowThreadProcessId(window, out processId);
                if (processId == ownProcessId || processId == 0)
                {
                    return true;
                }

                Rect windowRect;
                Rect frameRect;
                if (!GetWindowRect(window, out windowRect) ||
                    DwmGetWindowAttribute(window, ExtendedFrameBounds, out frameRect, Marshal.SizeOf(typeof(Rect))) != 0)
                {
                    return true;
                }

                if (windowRect.Right - windowRect.Left < 200 || windowRect.Bottom - windowRect.Top < 150)
                {
                    return true;
                }

                var buffer = new char[512];
                var length = GetWindowText(window, buffer, buffer.Length);
                found.Add(new WindowInfo
                {
                    Handle = window,
                    ProcessId = processId,
                    Title = length > 0 ? new string(buffer, 0, length) : string.Empty,
                    WindowRect = windowRect,
                    VisibleFrame = frameRect
                });
                return true;
            }, IntPtr.Zero);

            return found;
        }
    }
}
'@
}

if (-not [ZoneManager.FrameProbe]::MakeDpiAware()) {
    throw 'Der Pruefprozess liess sich nicht pro Monitor DPI-bewusst machen; die Messung waere ungueltig.'
}

$candidates = [ZoneManager.FrameProbe]::Candidates()
if ($candidates.Count -eq 0) {
    Write-Output 'FRAME_SKIPPED reason=no-foreign-window'
    return
}

$measurements = foreach ($candidate in $candidates) {
    $left = $candidate.VisibleFrame.Left - $candidate.WindowRect.Left
    $top = $candidate.VisibleFrame.Top - $candidate.WindowRect.Top
    $right = $candidate.WindowRect.Right - $candidate.VisibleFrame.Right
    $bottom = $candidate.WindowRect.Bottom - $candidate.VisibleFrame.Bottom

    [pscustomobject]@{
        Title         = $candidate.Title
        Process       = (Get-Process -Id $candidate.ProcessId -ErrorAction SilentlyContinue).ProcessName
        BorderLeft    = $left
        BorderTop     = $top
        BorderRight   = $right
        BorderBottom  = $bottom
        LargestBorder = ($left, $top, $right, $bottom | Measure-Object -Maximum).Maximum
        Plausible     = ($left -ge 0 -and $top -ge 0 -and $right -ge 0 -and $bottom -ge 0)
    }
}

$measurements | Format-Table -AutoSize | Out-String | Write-Output

$plausible = @($measurements | Where-Object { $_.Plausible })
if ($plausible.Count -eq 0) {
    throw 'Kein gemessenes Fenster lieferte einen plausiblen Rand; der sichtbare Rahmen lag nie im Fensterrechteck.'
}

$largest = ($plausible.LargestBorder | Measure-Object -Maximum).Maximum
if ($largest -gt $MaximumBorderPixels) {
    throw "Der gemessene unsichtbare Rand von $largest px ueberschreitet die angenommenen $MaximumBorderPixels px."
}

Write-Output "FRAME_OK windows=$($plausible.Count) largest=$largest limit=$MaximumBorderPixels"
