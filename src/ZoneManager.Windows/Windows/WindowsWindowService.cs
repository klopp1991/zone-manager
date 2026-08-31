using ZoneManager.Core.Drag;
using ZoneManager.Core.Geometry;
using ZoneManager.Core.Layouts;
using ZoneManager.Core.AppRules;
using ZoneManager.Core.PartMonitors;
using ZoneManager.Windows.Native;
using System.Runtime.InteropServices;
using System.Text;

namespace ZoneManager.Windows.Windows;

public sealed class WindowsWindowService : IWindowService
{
    private const int StyleIndex = -16;
    private const int ExtendedStyleIndex = -20;
    private const long ChildStyle = 0x40000000L;
    private const long ToolWindowStyle = 0x00000080L;
    private const uint RootAncestor = 2;
    private const uint NonClientHitTest = 0x0084;
    private const int CloakedAttribute = 14;
    private const uint AbortIfHung = 0x0002;
    private const int Restore = 9;
    private const uint NoZOrder = 0x0004;
    private const uint NoActivate = 0x0010;
    private const uint NoOwnerZOrder = 0x0200;
    private const uint AsyncWindowPosition = 0x4000;

    public WindowSnapshot? Inspect(nint window, PointInt cursor, int ownProcessId)
    {
        if (window == 0 || !User32.IsWindow(window))
        {
            return null;
        }

        var root = User32.GetAncestor(window, RootAncestor);
        var isChild = root != 0 && root != window;
        User32.GetWindowThreadProcessId(window, out var processId);
        var style = User32.GetWindowLongPtr(window, StyleIndex).ToInt64();
        var extendedStyle = User32.GetWindowLongPtr(window, ExtendedStyleIndex).ToInt64();
        var cloaked = 0;
        _ = DwmApi.DwmGetWindowAttribute(window, CloakedAttribute, out cloaked, sizeof(int));

        return new WindowSnapshot(
            User32.IsWindowVisible(window),
            isChild || (style & ChildStyle) != 0,
            processId == (uint)ownProcessId,
            (extendedStyle & ToolWindowStyle) != 0,
            cloaked != 0,
            IsTitleBarDrag(window, cursor));
    }

    public bool TrySnap(nint window, PixelRect bounds)
    {
        if (window == 0 || !User32.IsWindow(window) || bounds.Width < 1 || bounds.Height < 1)
        {
            return false;
        }

        _ = User32.ShowWindow(window, Restore);
        return User32.SetWindowPos(
            window,
            0,
            bounds.X,
            bounds.Y,
            bounds.Width,
            bounds.Height,
            NoZOrder | NoActivate | NoOwnerZOrder | AsyncWindowPosition);
    }

    public WindowPlacementSnapshot? Capture(nint window)
    {
        if (!TryGetIdentity(window, out var identity))
        {
            return null;
        }

        var placement = new WindowPlacementNative { Length = (uint)Marshal.SizeOf<WindowPlacementNative>() };
        if (!User32.GetWindowPlacement(window, ref placement))
        {
            return null;
        }

        return new WindowPlacementSnapshot(
            identity, placement.Flags, placement.ShowCommand,
            new PointInt(placement.MinPosition.X, placement.MinPosition.Y),
            new PointInt(placement.MaxPosition.X, placement.MaxPosition.Y),
            ToPixelRect(placement.NormalPosition));
    }

    public bool TryApplyNormal(WindowIdentity identity, PixelRect bounds)
    {
        if (!MatchesCurrentIdentity(identity) || bounds.Width < 1 || bounds.Height < 1)
        {
            return false;
        }

        _ = User32.ShowWindow(identity.Handle, Restore);
        if (!MatchesCurrentIdentity(identity))
        {
            return false;
        }

        return User32.SetWindowPos(identity.Handle, 0, bounds.X, bounds.Y, bounds.Width, bounds.Height,
            NoZOrder | NoActivate | NoOwnerZOrder | AsyncWindowPosition);
    }

    public bool TryRestore(WindowPlacementSnapshot snapshot)
    {
        if (!MatchesCurrentIdentity(snapshot.Identity))
        {
            return false;
        }

        var placement = new WindowPlacementNative
        {
            Length = (uint)Marshal.SizeOf<WindowPlacementNative>(),
            Flags = snapshot.Flags,
            ShowCommand = snapshot.ShowCommand,
            MinPosition = new PointNative { X = snapshot.MinPosition.X, Y = snapshot.MinPosition.Y },
            MaxPosition = new PointNative { X = snapshot.MaxPosition.X, Y = snapshot.MaxPosition.Y },
            NormalPosition = ToNativeRect(snapshot.NormalPosition)
        };
        return User32.SetWindowPlacement(snapshot.Identity.Handle, ref placement);
    }

    public IReadOnlyList<WindowPlacement> GetMovableTopLevelWindows(int ownProcessId)
    {
        var windows = new List<WindowPlacement>();
        _ = User32.EnumWindows((window, _) =>
        {
            if (!TryGetMovableTopLevelWindow(window, ownProcessId, out var placement))
            {
                return true;
            }

            windows.Add(placement);
            return true;
        }, 0);
        return windows;
    }

    public WindowRuleCandidate? InspectRuleCandidate(nint window, int ownProcessId)
    {
        if (!IsEligibleTopLevelWindow(window, ownProcessId, out var processId))
        {
            return null;
        }

        var processPath = ReadProcessPath(processId);
        var windowClass = ReadWindowClass(window);
        if (processPath is null || windowClass is null)
        {
            return null;
        }

        return new WindowRuleCandidate(
            window,
            new AppWindowIdentity(
                checked((int)processId),
                processPath,
                ReadWindowTitle(window),
                windowClass));
    }

    public IReadOnlyList<WindowRuleCandidate> GetRuleCandidates(int ownProcessId)
    {
        var windows = new List<WindowRuleCandidate>();
        _ = User32.EnumWindows((window, _) =>
        {
            var candidate = InspectRuleCandidate(window, ownProcessId);
            if (candidate is not null)
            {
                windows.Add(candidate);
            }

            return true;
        }, 0);
        return windows;
    }

    public bool TryGetCursorPosition(out PointInt point)
    {
        if (User32.GetCursorPos(out var nativePoint))
        {
            point = new PointInt(nativePoint.X, nativePoint.Y);
            return true;
        }

        point = default;
        return false;
    }

    public bool IsEscapePressed() => (User32.GetAsyncKeyState(0x1B) & 0x8000) != 0;

    public bool IsShiftPressed() => (User32.GetAsyncKeyState(0x10) & 0x8000) != 0;

    private static bool TryGetMovableTopLevelWindow(nint window, int ownProcessId, out WindowPlacement placement)
    {
        placement = default!;
        if (window == 0 || !User32.IsWindow(window) || !User32.IsWindowVisible(window))
        {
            return false;
        }

        var root = User32.GetAncestor(window, RootAncestor);
        User32.GetWindowThreadProcessId(window, out var processId);
        var style = User32.GetWindowLongPtr(window, StyleIndex).ToInt64();
        var extendedStyle = User32.GetWindowLongPtr(window, ExtendedStyleIndex).ToInt64();
        var cloaked = 0;
        _ = DwmApi.DwmGetWindowAttribute(window, CloakedAttribute, out cloaked, sizeof(int));
        if ((root != 0 && root != window) || processId == (uint)ownProcessId ||
            (style & ChildStyle) != 0 || (extendedStyle & ToolWindowStyle) != 0 || cloaked != 0 ||
            !User32.GetWindowRect(window, out var rectangle))
        {
            return false;
        }

        var bounds = new PixelRect(
            rectangle.Left,
            rectangle.Top,
            Math.Max(0, rectangle.Right - rectangle.Left),
            Math.Max(0, rectangle.Bottom - rectangle.Top));
        if (bounds.Width < 1 || bounds.Height < 1)
        {
            return false;
        }

        placement = new WindowPlacement(window, bounds);
        return true;
    }

    private static bool IsEligibleTopLevelWindow(nint window, int ownProcessId, out uint processId)
    {
        processId = 0;
        if (window == 0 || !User32.IsWindow(window) || !User32.IsWindowVisible(window))
        {
            return false;
        }

        var root = User32.GetAncestor(window, RootAncestor);
        User32.GetWindowThreadProcessId(window, out processId);
        var style = User32.GetWindowLongPtr(window, StyleIndex).ToInt64();
        var extendedStyle = User32.GetWindowLongPtr(window, ExtendedStyleIndex).ToInt64();
        var cloaked = 0;
        _ = DwmApi.DwmGetWindowAttribute(window, CloakedAttribute, out cloaked, sizeof(int));
        return (root == 0 || root == window) &&
            processId != (uint)ownProcessId &&
            (style & ChildStyle) == 0 &&
            (extendedStyle & ToolWindowStyle) == 0 &&
            cloaked == 0;
    }

    private static string? ReadProcessPath(uint processId)
    {
        using var process = Kernel32.OpenProcess(Kernel32.QueryLimitedInformation, false, processId);
        if (process.IsInvalid)
        {
            return null;
        }

        var capacity = 32768;
        var path = new StringBuilder(capacity);
        return Kernel32.QueryFullProcessImageName(process, 0, path, ref capacity)
            ? path.ToString()
            : null;
    }

    private static string ReadWindowTitle(nint window)
    {
        var capacity = Math.Clamp(User32.GetWindowTextLength(window) + 1, 1, 32768);
        var title = new StringBuilder(capacity);
        _ = User32.GetWindowText(window, title, capacity);
        return title.ToString();
    }

    private static string? ReadWindowClass(nint window)
    {
        var className = new StringBuilder(256);
        return User32.GetClassName(window, className, className.Capacity) > 0
            ? className.ToString()
            : null;
    }

    private static bool MatchesCurrentIdentity(WindowIdentity expected) =>
        TryGetIdentity(expected.Handle, out var current) && current == expected;

    private static bool TryGetIdentity(nint window, out WindowIdentity identity)
    {
        identity = new WindowIdentity(0, 0, string.Empty);
        if (window == 0 || !User32.IsWindow(window))
        {
            return false;
        }

        _ = User32.GetWindowThreadProcessId(window, out var processId);
        var className = new StringBuilder(256);
        if (processId == 0 || User32.GetClassName(window, className, className.Capacity) < 1)
        {
            return false;
        }

        identity = new WindowIdentity(window, processId, className.ToString());
        return true;
    }

    private static PixelRect ToPixelRect(RectNative rectangle) => new(
        rectangle.Left, rectangle.Top, rectangle.Right - rectangle.Left, rectangle.Bottom - rectangle.Top);

    private static RectNative ToNativeRect(PixelRect rectangle) => new()
    {
        Left = rectangle.X, Top = rectangle.Y, Right = rectangle.Right, Bottom = rectangle.Bottom
    };

    private static bool IsTitleBarDrag(nint window, PointInt cursor)
    {
        var packedPoint = (nint)(((long)(cursor.Y & 0xffff) << 16) | (uint)(cursor.X & 0xffff));
        var callResult = User32.SendMessageTimeout(
            window,
            NonClientHitTest,
            0,
            packedPoint,
            AbortIfHung,
            50,
            out var hitResult);
        if (callResult != 0)
        {
            return WindowHitTestClassifier.IsMoveOperation((int)hitResult);
        }

        if (!User32.GetWindowRect(window, out var rectangle))
        {
            return false;
        }

        return cursor.X >= rectangle.Left + 8 &&
            cursor.X < rectangle.Right - 8 &&
            cursor.Y >= rectangle.Top + 4 &&
            cursor.Y < Math.Min(rectangle.Bottom, rectangle.Top + 72);
    }
}
