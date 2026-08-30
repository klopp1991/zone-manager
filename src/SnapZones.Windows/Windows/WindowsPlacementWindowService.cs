using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using SnapZones.Core.Geometry;
using SnapZones.Core.Placement;
using SnapZones.Windows.Native;

namespace SnapZones.Windows.Windows;

public sealed class WindowsPlacementWindowService : IPlacementWindowService
{
    private const int StyleIndex = -16;
    private const int ExtendedStyleIndex = -20;
    private const long ChildStyle = 0x40000000L;
    private const long ToolWindowStyle = 0x00000080L;
    private const uint RootAncestor = 2;
    private const uint OwnerWindow = 4;
    private const int CloakedAttribute = 14;
    private const uint DefaultToNearestMonitor = 2;
    private const uint ShowMinimized = 2;
    private const uint ShowMaximized = 3;
    private const uint Minimize = 6;
    private const uint ShowMinimizedWithoutActivation = 7;
    private const uint ForceMinimize = 11;
    private const uint ShowNormal = 1;

    private static readonly HashSet<string> ShellWindowClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Progman",
        "WorkerW",
        "Shell_TrayWnd"
    };

    public PlacementWindowSnapshot? Inspect(nint windowHandle, int excludedProcessId)
    {
        try
        {
            if (!TryReadEligibleWindow(windowHandle, excludedProcessId, out var processId, out var windowClass))
            {
                return null;
            }

            if (!User32.GetWindowRect(windowHandle, out var currentRectangle))
            {
                return null;
            }

            var placement = CreatePlacement();
            if (!User32.GetWindowPlacement(windowHandle, ref placement) ||
                !TryReadMonitorInfo(User32.MonitorFromWindow(windowHandle, DefaultToNearestMonitor), out var monitorInfo))
            {
                return null;
            }

            var applicationKey = Shell32.TryReadAppUserModelId(windowHandle) ?? ReadCanonicalProcessPath(processId);
            var kind = User32.GetWindow(windowHandle, OwnerWindow) != 0 ||
                string.Equals(windowClass, "#32770", StringComparison.Ordinal)
                ? WindowKind.Dialog
                : WindowKind.MainWindow;
            var identity = new WindowIdentity(applicationKey, windowClass, kind);
            var normalWorkspaceBounds = ToPixelRect(placement.NormalPosition);

            return new PlacementWindowSnapshot(
                windowHandle,
                identity,
                ReadWindowTitle(windowHandle),
                ToPixelRect(currentRectangle),
                WorkspaceToScreen(normalWorkspaceBounds, monitorInfo.Monitor, monitorInfo.Work),
                placement.ShowCommand == ShowMaximized,
                IsMinimized(placement.ShowCommand));
        }
        catch (Exception)
        {
            return null;
        }
    }

    public bool TryPlace(nint windowHandle, PixelRect normalBounds, bool maximize)
    {
        try
        {
            if (normalBounds.Width < 1 ||
                normalBounds.Height < 1 ||
                !TryReadEligibleWindow(windowHandle, excludedProcessId: -1, out _, out _))
            {
                return false;
            }

            var screenRectangle = ToNativeRect(normalBounds);
            var monitor = User32.MonitorFromRect(ref screenRectangle, DefaultToNearestMonitor);
            if (!TryReadMonitorInfo(monitor, out var monitorInfo))
            {
                return false;
            }

            var placement = CreatePlacement();
            if (!User32.GetWindowPlacement(windowHandle, ref placement))
            {
                return false;
            }

            var workspaceBounds = ScreenToWorkspace(normalBounds, monitorInfo.Monitor, monitorInfo.Work);
            placement.NormalPosition = ToNativeRect(workspaceBounds);
            placement.ShowCommand = maximize ? ShowMaximized : ShowNormal;
            return User32.SetWindowPlacement(windowHandle, ref placement);
        }
        catch (Exception)
        {
            return false;
        }
    }

    public IReadOnlyList<nint> EnumerateEligibleWindows(int excludedProcessId)
    {
        try
        {
            var windows = new List<nint>();
            User32.EnumWindowsProc callback = (window, _) =>
            {
                if (Inspect(window, excludedProcessId) is not null)
                {
                    windows.Add(window);
                }

                return true;
            };

            return User32.EnumWindows(callback, 0) ? windows : [];
        }
        catch (Exception)
        {
            return [];
        }
    }

    public nint GetForegroundWindow() => User32.GetForegroundWindow();

    // rcNormalPosition verwendet Workspace-Koordinaten; die Differenz der Ursprünge konvertiert zu Bildschirmkoordinaten.
    internal static PixelRect WorkspaceToScreen(PixelRect bounds, RectNative monitor, RectNative workArea) =>
        new(
            checked(bounds.X + workArea.Left - monitor.Left),
            checked(bounds.Y + workArea.Top - monitor.Top),
            bounds.Width,
            bounds.Height);

    // Beim Schreiben wird der Work-Area-Abstand abgezogen, damit SetWindowPlacement denselben Bildschirmbereich erhält.
    internal static PixelRect ScreenToWorkspace(PixelRect bounds, RectNative monitor, RectNative workArea) =>
        new(
            checked(bounds.X - workArea.Left + monitor.Left),
            checked(bounds.Y - workArea.Top + monitor.Top),
            bounds.Width,
            bounds.Height);

    private static bool TryReadEligibleWindow(
        nint window,
        int excludedProcessId,
        out uint processId,
        out string windowClass)
    {
        processId = 0;
        windowClass = string.Empty;
        if (window == 0 || !User32.IsWindow(window) || !User32.IsWindowVisible(window))
        {
            return false;
        }

        var root = User32.GetAncestor(window, RootAncestor);
        var style = User32.GetWindowLongPtr(window, StyleIndex).ToInt64();
        var extendedStyle = User32.GetWindowLongPtr(window, ExtendedStyleIndex).ToInt64();
        if ((root != 0 && root != window) ||
            (style & ChildStyle) != 0 ||
            (extendedStyle & ToolWindowStyle) != 0)
        {
            return false;
        }

        var cloaked = 0;
        if (DwmApi.DwmGetWindowAttribute(window, CloakedAttribute, out cloaked, sizeof(int)) != 0 || cloaked != 0)
        {
            return false;
        }

        if (User32.GetWindowThreadProcessId(window, out processId) == 0 ||
            processId == 0 ||
            processId == excludedProcessId ||
            !WindowsIntegrityLevelReader.CanControl(processId))
        {
            return false;
        }

        windowClass = ReadWindowClass(window);
        return !ShellWindowClasses.Contains(windowClass);
    }

    private static string ReadWindowClass(nint window)
    {
        var buffer = new StringBuilder(256);
        if (User32.GetClassName(window, buffer, buffer.Capacity) == 0)
        {
            throw new InvalidOperationException("Die Fensterklasse konnte nicht gelesen werden.");
        }

        return buffer.ToString();
    }

    private static string ReadWindowTitle(nint window)
    {
        var buffer = new StringBuilder(1024);
        _ = User32.GetWindowText(window, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    private static string ReadCanonicalProcessPath(uint processId)
    {
        if (processId > int.MaxValue)
        {
            throw new InvalidOperationException("Die Prozess-ID liegt ausserhalb des unterstützten Bereichs.");
        }

        using var process = Process.GetProcessById((int)processId);
        var processPath = process.MainModule?.FileName;
        if (string.IsNullOrWhiteSpace(processPath))
        {
            throw new InvalidOperationException("Der Prozesspfad konnte nicht gelesen werden.");
        }

        return Path.GetFullPath(processPath);
    }

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

    private static RectNative ToNativeRect(PixelRect rectangle) => new()
    {
        Left = rectangle.X,
        Top = rectangle.Y,
        Right = checked(rectangle.X + rectangle.Width),
        Bottom = checked(rectangle.Y + rectangle.Height)
    };

    private static bool IsMinimized(uint showCommand) => showCommand is
        ShowMinimized or Minimize or ShowMinimizedWithoutActivation or ForceMinimize;
}
