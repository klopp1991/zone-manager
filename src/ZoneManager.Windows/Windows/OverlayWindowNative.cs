using System.ComponentModel;
using System.Runtime.InteropServices;
using ZoneManager.Core.Geometry;
using ZoneManager.Windows.Native;

namespace ZoneManager.Windows.Windows;

public static class OverlayWindowNative
{
    private const int ExtendedStyleIndex = -20;
    private const long Transparent = 0x00000020L;
    private const long ToolWindow = 0x00000080L;
    private const long NoActivate = 0x08000000L;
    private const uint NoActivatePosition = 0x0010;
    private const uint ShowWindow = 0x0040;
    private static readonly nint TopMost = new(-1);

    public static void Configure(nint window)
    {
        var existing = User32.GetWindowLongPtr(window, ExtendedStyleIndex).ToInt64();
        Marshal.SetLastPInvokeError(0);
        var previous = User32.SetWindowLongPtr(window, ExtendedStyleIndex, new nint(existing | Transparent | ToolWindow | NoActivate));
        var error = Marshal.GetLastWin32Error();
        if (previous == 0 && error != 0)
        {
            throw new Win32Exception(error, "Das Overlay konnte nicht sicher konfiguriert werden.");
        }
    }

    public static void Position(nint window, PixelRect bounds)
    {
        if (!User32.SetWindowPos(
            window,
            TopMost,
            bounds.X,
            bounds.Y,
            bounds.Width,
            bounds.Height,
            NoActivatePosition | ShowWindow))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Das Overlay konnte nicht positioniert werden.");
        }
    }
}
