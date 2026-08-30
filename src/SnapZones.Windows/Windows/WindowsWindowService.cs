using SnapZones.Core.Drag;
using SnapZones.Core.Geometry;
using SnapZones.Windows.Native;

namespace SnapZones.Windows.Windows;

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
