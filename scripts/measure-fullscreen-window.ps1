<#
.SYNOPSIS
    Misst an einem echten Vollbildfenster, ob das Zonen-Vollbild darauf zugreifen kann.

.DESCRIPTION
    Das Zonen-Vollbild beruht auf einer Annahme ueber die Wirklichkeit: dass ein Browser oder Videoplayer
    im Vollbild keinen Exklusivmodus der Grafikkarte anfordert, sondern sein Fenster randlos ueber die
    volle Monitorflaeche legt. Nur dann laesst es sich auf eine Zone setzen. Dieses Skript prueft die
    Annahme am laufenden System statt sie zu glauben.

    Gemeldet wird je Fenster, das die ganze Monitorflaeche einnimmt: Prozess, Fensterklasse, Rechteck,
    Monitorflaeche, ob Windows es als maximiert fuehrt und welche Stile es traegt. Entscheidend ist
    WS_THICKFRAME: fehlt der Stil, gilt das Fenster sonst als Fenster fester Groesse und wuerde in der
    Zone nur zentriert -- deshalb setzt das Zonen-Vollbild die Groesse ueber IWindowService.Fill, das
    diese Pruefung uebergeht.

    Gelesen wird ausschliesslich. Nichts wird gestartet, geschlossen, verschoben oder umgeschaltet.

    Zum Messen einen Player im Vollbild oeffnen -- etwa ein Video auf Twitch oder YouTube im Browser --
    und das Skript aus einer zweiten Sitzung starten.

.PARAMETER TolerancePixels
    Wie weit das Fensterrechteck von den Monitorkanten abweichen darf und trotzdem als Vollbild gilt.
    Entspricht ZoneFullscreen.MonitorCoverageTolerancePixels.

.PARAMETER NoRelaunch
    Verhindert den Neustart in Windows PowerShell. Nur fuer den Neustart selbst gedacht.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File scripts\measure-fullscreen-window.ps1
#>
[CmdletBinding()]
param(
    [int] $TolerancePixels = 2,

    [switch] $NoRelaunch
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not ('ZoneManager.FullscreenProbe' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ZoneManager
{
    [StructLayout(LayoutKind.Sequential)]
    public struct FullscreenRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct MonitorInfoEx
    {
        public uint Size;
        public FullscreenRect Monitor;
        public FullscreenRect Work;
        public uint Flags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
    }

    public sealed class FullscreenWindow
    {
        public IntPtr Handle;
        public int ProcessId;
        public string Title;
        public string WindowClass;
        public FullscreenRect WindowRect;
        public FullscreenRect MonitorRect;
        public FullscreenRect WorkRect;
        public bool IsMaximized;
        public bool HasThickFrame;
        public bool HasCaption;
        public bool IsTopmost;
    }

    public static class FullscreenProbe
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

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetClassName(IntPtr window, [Out] char[] text, int count);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr window, out FullscreenRect rectangle);

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);

        [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", CharSet = CharSet.Unicode)]
        private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfoEx monitorInfo);

        [DllImport("user32.dll")]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr context);

        private const int GwlStyle = -16;
        private const int GwlExStyle = -20;
        private const int WsThickFrame = 0x00040000;
        private const int WsCaption = 0x00C00000;
        private const int WsExTopmost = 0x00000008;
        private const uint DefaultToNearestMonitor = 2;

        // DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2
        private static readonly IntPtr PerMonitorAwareV2 = new IntPtr(-4);

        public static bool MakeDpiAware()
        {
            return SetProcessDpiAwarenessContext(PerMonitorAwareV2);
        }

        /// <summary>
        /// Sichtbare Fenster fremder Prozesse, die die ganze Monitorflaeche einnehmen -- genau die, die
        /// das Zonen-Vollbild zurueckholen wuerde.
        /// </summary>
        public static List<FullscreenWindow> Candidates(int tolerance)
        {
            var ownProcessId = System.Diagnostics.Process.GetCurrentProcess().Id;
            var found = new List<FullscreenWindow>();
            EnumWindows((window, parameter) =>
            {
                if (!IsWindowVisible(window) || IsIconic(window))
                {
                    return true;
                }

                int processId;
                GetWindowThreadProcessId(window, out processId);
                if (processId == ownProcessId || processId == 0)
                {
                    return true;
                }

                FullscreenRect windowRect;
                if (!GetWindowRect(window, out windowRect))
                {
                    return true;
                }

                var monitor = MonitorFromWindow(window, DefaultToNearestMonitor);
                if (monitor == IntPtr.Zero)
                {
                    return true;
                }

                var monitorInfo = new MonitorInfoEx();
                monitorInfo.Size = (uint)Marshal.SizeOf(typeof(MonitorInfoEx));
                monitorInfo.DeviceName = string.Empty;
                if (!GetMonitorInfo(monitor, ref monitorInfo))
                {
                    return true;
                }

                if (windowRect.Left > monitorInfo.Monitor.Left + tolerance ||
                    windowRect.Top > monitorInfo.Monitor.Top + tolerance ||
                    windowRect.Right < monitorInfo.Monitor.Right - tolerance ||
                    windowRect.Bottom < monitorInfo.Monitor.Bottom - tolerance)
                {
                    return true;
                }

                var style = GetWindowLong(window, GwlStyle);
                var extendedStyle = GetWindowLong(window, GwlExStyle);
                var titleBuffer = new char[512];
                var titleLength = GetWindowText(window, titleBuffer, titleBuffer.Length);
                var classBuffer = new char[256];
                var classLength = GetClassName(window, classBuffer, classBuffer.Length);
                found.Add(new FullscreenWindow
                {
                    Handle = window,
                    ProcessId = processId,
                    Title = titleLength > 0 ? new string(titleBuffer, 0, titleLength) : string.Empty,
                    WindowClass = classLength > 0 ? new string(classBuffer, 0, classLength) : string.Empty,
                    WindowRect = windowRect,
                    MonitorRect = monitorInfo.Monitor,
                    WorkRect = monitorInfo.Work,
                    IsMaximized = IsZoomed(window),
                    HasThickFrame = (style & WsThickFrame) != 0,
                    HasCaption = (style & WsCaption) != 0,
                    IsTopmost = (extendedStyle & WsExTopmost) != 0
                });
                return true;
            }, IntPtr.Zero);

            return found;
        }
    }
}
'@
}

if (-not [ZoneManager.FullscreenProbe]::MakeDpiAware()) {
    # Wie in measure-window-frame.ps1: PowerShell 7 traegt die DPI-Bewusstheit im Manifest und laesst sie
    # nicht mehr aendern. Ohne sie waeren die gemessenen Koordinaten virtualisiert und die Aussage wertlos.
    $windowsPowerShell = Join-Path $env:SystemRoot 'System32\WindowsPowerShell\v1.0\powershell.exe'
    if ($NoRelaunch -or -not (Test-Path -LiteralPath $windowsPowerShell -PathType Leaf)) {
        throw 'Der Pruefprozess liess sich nicht pro Monitor DPI-bewusst machen; die Messung waere ungueltig.'
    }

    & $windowsPowerShell -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath `
        -TolerancePixels $TolerancePixels -NoRelaunch
    exit $LASTEXITCODE
}

$candidates = [ZoneManager.FullscreenProbe]::Candidates($TolerancePixels)
if ($candidates.Count -eq 0) {
    Write-Output 'FULLSCREEN_SKIPPED reason=no-fullscreen-window'
    Write-Output 'Zum Messen einen Player im Vollbild oeffnen und das Skript erneut starten.'
    exit 0
}

# Die Shell selbst und die Eingabeerfahrung decken den Monitor dauerhaft ab, ohne je ein Vollbild zu sein.
# Sie stehen in jeder Messung und wuerden das Ergebnis verdecken; das Zonen-Vollbild fasst sie nie an, weil
# sie nie in einer Zone eingerastet lagen.
$shellClasses = @('Progman', 'WorkerW', 'Shell_TrayWnd', 'Windows.UI.Core.CoreWindow')
$shell = @($candidates | Where-Object { $shellClasses -contains $_.WindowClass })
$candidates = @($candidates | Where-Object { $shellClasses -notcontains $_.WindowClass })
if ($shell.Count -gt 0) {
    Write-Output "FULLSCREEN_SHELL_IGNORED count=$($shell.Count) classes=$(($shell.WindowClass | Sort-Object -Unique) -join ',')"
}

if ($candidates.Count -eq 0) {
    Write-Output 'FULLSCREEN_SKIPPED reason=no-fullscreen-window'
    Write-Output 'Zum Messen einen Player im Vollbild oeffnen und das Skript erneut starten.'
    exit 0
}

foreach ($candidate in $candidates) {
    $process = try { (Get-Process -Id $candidate.ProcessId -ErrorAction Stop).ProcessName } catch { 'unbekannt' }
    $width = $candidate.WindowRect.Right - $candidate.WindowRect.Left
    $height = $candidate.WindowRect.Bottom - $candidate.WindowRect.Top
    $monitorWidth = $candidate.MonitorRect.Right - $candidate.MonitorRect.Left
    $monitorHeight = $candidate.MonitorRect.Bottom - $candidate.MonitorRect.Top

    # Nur ein nicht maximiertes Fenster gilt als Vollbild: ein maximiertes endet gewoehnlich an der
    # Taskleiste und deckt den Monitor nur bei automatischem Ausblenden.
    $verdict = if ($candidate.IsMaximized) { 'maximiert-nicht-vollbild' } else { 'zonen-vollbild-moeglich' }

    Write-Output ''
    Write-Output "FULLSCREEN_WINDOW hwnd=0x$('{0:X}' -f [int64]$candidate.Handle) prozess=$process"
    Write-Output "  titel        : $($candidate.Title)"
    Write-Output "  klasse       : $($candidate.WindowClass)"
    Write-Output "  fenster      : $($candidate.WindowRect.Left),$($candidate.WindowRect.Top) ${width}x${height}"
    Write-Output "  monitor      : $($candidate.MonitorRect.Left),$($candidate.MonitorRect.Top) ${monitorWidth}x${monitorHeight}"
    Write-Output "  maximiert    : $($candidate.IsMaximized)"
    Write-Output "  WS_THICKFRAME: $($candidate.HasThickFrame)   WS_CAPTION: $($candidate.HasCaption)   topmost: $($candidate.IsTopmost)"
    Write-Output "  ergebnis     : $verdict"
    if (-not $candidate.IsMaximized -and -not $candidate.HasThickFrame) {
        Write-Output '  hinweis      : ohne WS_THICKFRAME wuerde der gewoehnliche Weg das Fenster nur zentrieren; das Zonen-Vollbild setzt die Groesse deshalb erzwungen.'
    }
}

Write-Output ''
Write-Output "FULLSCREEN_MEASURED count=$($candidates.Count)"
Write-Output 'Zurueckgeholt wird davon nur ein Fenster, das vor dem Vollbild in einer Zone eingerastet lag.'
exit 0
