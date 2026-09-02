using System.IO;
using System.Runtime.InteropServices;
using SnapZones.Core.Geometry;

using SnapZones.Core.Placement;
using SnapZones.Windows.Native;

namespace SnapZones.Windows.Windows;

public sealed class WindowsPlacementWindowService : IPlacementWindowService
{
    private const uint OwnerWindow = 4;
    private const uint DefaultToNearestMonitor = 2;
    private const uint ShowMinimized = 2;
    private const uint ShowMaximized = 3;
    private const int ShowWithoutActivation = 4;
    private const uint Minimize = 6;
    private const uint ShowMinimizedWithoutActivation = 7;
    private const uint ForceMinimize = 11;
    private const uint NoZOrder = 0x0004;
    private const uint NoActivate = 0x0010;
    private const uint NoOwnerZOrder = 0x0200;

    private readonly IWindowStyleReader styleReader;
    private readonly Action<string>? trace;

    public WindowsPlacementWindowService(Action<string>? trace = null)
        : this(new User32WindowStyleReader(), trace)
    {
    }

    internal WindowsPlacementWindowService(IWindowStyleReader styleReader, Action<string>? trace = null)
    {
        this.styleReader = styleReader ?? throw new ArgumentNullException(nameof(styleReader));
        this.trace = trace;
    }

    public PlacementWindowSnapshot? Inspect(nint windowHandle, int excludedProcessId)
    {
        try
        {
            if (!TryReadEligibleWindow(windowHandle, excludedProcessId, out var classification))
            {
                return null;
            }

            var placement = CreatePlacement();
            if (!User32.GetWindowPlacement(windowHandle, ref placement) ||
                !TryReadMonitorInfo(User32.MonitorFromWindow(windowHandle, DefaultToNearestMonitor), out var monitorInfo))
            {
                return null;
            }

            var processPath = WindowEligibility.ReadProcessPath(classification.ProcessId);
            var applicationKey = Shell32.TryReadAppUserModelId(windowHandle) ?? processPath;
            if (string.IsNullOrWhiteSpace(applicationKey))
            {
                // Ohne Programmpfad und ohne App-Kennung liesse sich das Fenster beim naechsten Oeffnen
                // nicht wiedererkennen; alle solchen Fenster teilten sich sonst einen Eintrag.
                trace?.Invoke($"Fenster 0x{windowHandle:X} ({classification.WindowClass}): weder Programmpfad noch App-Kennung lesbar.");
                return null;
            }

            var kind = User32.GetWindow(windowHandle, OwnerWindow) != 0 ||
                string.Equals(classification.WindowClass, "#32770", StringComparison.Ordinal)
                ? WindowKind.Dialog
                : WindowKind.MainWindow;
            var identity = new WindowIdentity(applicationKey, classification.WindowClass, kind);
            var normalWorkspaceBounds = ToPixelRect(placement.NormalPosition);

            return new PlacementWindowSnapshot(
                windowHandle,
                identity,
                WindowEligibility.ReadWindowTitle(windowHandle),
                classification.Bounds,
                WorkspaceToScreen(normalWorkspaceBounds, monitorInfo.Monitor, monitorInfo.Work),
                placement.ShowCommand == ShowMaximized,
                IsMinimized(placement.ShowCommand),
                processPath is null ? null : Path.GetFullPath(processPath));
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or OverflowException or IOException)
        {
            trace?.Invoke($"Fenster 0x{windowHandle:X} konnte nicht gelesen werden: {exception.Message}");
            return null;
        }
    }

    /// <summary>
    /// Setzt die Normalposition und misst nach. Ein Maximieren wird nur ausgefuehrt, wenn das Fenster
    /// gerade im Vordergrund ist, weil Windows dafuer die Aktivierung verlangt; die Normalposition ist
    /// dann trotzdem gesetzt, sodass ein spaeteres Wiederherstellen richtig landet. Frueher scheiterte
    /// in diesem Fall die gesamte Platzierung.
    /// </summary>
    public bool TryPlace(nint windowHandle, PixelRect normalBounds, bool maximize)
    {
        try
        {
            if (normalBounds.Width < 1 ||
                normalBounds.Height < 1 ||
                !TryReadEligibleWindow(windowHandle, excludedProcessId: -1, out _))
            {
                return false;
            }

            _ = User32.ShowWindow(windowHandle, ShowWithoutActivation);
            if (!SetPosition(windowHandle, normalBounds))
            {
                trace?.Invoke($"Fenster 0x{windowHandle:X}: Windows hat die Platzierung abgelehnt ({Marshal.GetLastWin32Error()}).");
                return false;
            }

            if (!User32.GetWindowRect(windowHandle, out var measured))
            {
                return false;
            }

            var actual = WindowEligibility.ToPixelRect(measured);
            if (!actual.IsWithinTolerance(normalBounds, SnapZones.Core.PartMonitors.PlacementOutcome.TolerancePixels))
            {
                // Zweiter Anlauf: beim Wechsel auf einen Monitor mit anderer Skalierung passt Windows
                // das Fenster erst nach dem ersten Setzen an.
                _ = SetPosition(windowHandle, normalBounds);
                if (User32.GetWindowRect(windowHandle, out measured))
                {
                    actual = WindowEligibility.ToPixelRect(measured);
                }
            }

            if (!actual.IsWithinTolerance(normalBounds, SnapZones.Core.PartMonitors.PlacementOutcome.TolerancePixels))
            {
                trace?.Invoke($"Fenster 0x{windowHandle:X} sitzt nicht wie gemerkt: Ziel {normalBounds}, Ergebnis {actual}.");
            }

            if (maximize)
            {
                if (User32.GetForegroundWindow() == windowHandle)
                {
                    _ = User32.ShowWindow(windowHandle, (int)ShowMaximized);
                }
                else
                {
                    trace?.Invoke($"Fenster 0x{windowHandle:X} wurde nicht maximiert, weil es nicht im Vordergrund ist.");
                }
            }

            return true;
        }
        catch (Exception exception) when (exception is InvalidOperationException or ArgumentException or OverflowException)
        {
            trace?.Invoke($"Fenster 0x{windowHandle:X} konnte nicht platziert werden: {exception.Message}");
            return false;
        }
    }

    public IReadOnlyList<nint> EnumerateEligibleWindows(int excludedProcessId)
    {
        var windows = new List<nint>();
        User32.WindowEnumProc callback = (window, data) =>
        {
            // Billige Vorpruefung ueber Stil und Klasse; die teure Identitaet liest erst Inspect.
            if (TryReadEligibleWindow(window, excludedProcessId, out _))
            {
                windows.Add(window);
            }

            return true;
        };

        return User32.EnumWindows(callback, 0) ? windows : [];
    }

    public nint GetForegroundWindow() => User32.GetForegroundWindow();

    // rcNormalPosition verwendet Workspace-Koordinaten; die Differenz der Ursprünge konvertiert zu Bildschirmkoordinaten.
    internal static PixelRect WorkspaceToScreen(PixelRect bounds, RectNative monitor, RectNative workArea) =>
        new(
            checked(bounds.X + workArea.Left - monitor.Left),
            checked(bounds.Y + workArea.Top - monitor.Top),
            bounds.Width,
            bounds.Height);

    // Die inverse Konvertierung bleibt für Workspace-basierte Placement-Pfade und den Offset-Regressionstest erhalten.
    internal static PixelRect ScreenToWorkspace(PixelRect bounds, RectNative monitor, RectNative workArea) =>
        new(
            checked(bounds.X - workArea.Left + monitor.Left),
            checked(bounds.Y - workArea.Top + monitor.Top),
            bounds.Width,
            bounds.Height);

    private bool TryReadEligibleWindow(nint window, int excludedProcessId, out WindowClassification classification)
    {
        if (!WindowEligibility.TryClassify(window, excludedProcessId, styleReader, out classification, out var reason) ||
            reason != WindowRejectionReason.None ||
            classification.CloakStateUnknown)
        {
            return false;
        }

        return WindowsIntegrityLevelReader.CanControl(classification.ProcessId);
    }

    private static bool SetPosition(nint window, PixelRect bounds) =>
        User32.SetWindowPos(
            window,
            0,
            bounds.X,
            bounds.Y,
            bounds.Width,
            bounds.Height,
            NoActivate | NoZOrder | NoOwnerZOrder);

    private static bool TryReadMonitorInfo(nint monitor, out MonitorInfoEx monitorInfo)
    {
        monitorInfo = new MonitorInfoEx
        {
            Size = (uint)Marshal.SizeOf<MonitorInfoEx>(),
            DeviceName = string.Empty
        };
        return monitor != 0 && User32.GetMonitorInfo(monitor, ref monitorInfo);
    }

    private static WindowPlacementNative CreatePlacement() => new()
    {
        Length = (uint)Marshal.SizeOf<WindowPlacementNative>()
    };

    private static PixelRect ToPixelRect(RectNative rectangle) =>
        new(
            rectangle.Left,
            rectangle.Top,
            checked(rectangle.Right - rectangle.Left),
            checked(rectangle.Bottom - rectangle.Top));

    private static bool IsMinimized(uint showCommand) => showCommand is
        ShowMinimized or Minimize or ShowMinimizedWithoutActivation or ForceMinimize;
}
