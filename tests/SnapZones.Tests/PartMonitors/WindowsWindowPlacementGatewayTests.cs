using System.Windows.Forms;
using SnapZones.Core.Geometry;
using SnapZones.Core.PartMonitors;
using SnapZones.Windows.Windows;
using System.Runtime.InteropServices;
using Xunit;

namespace SnapZones.Tests.PartMonitors;

public sealed class WindowsWindowPlacementGatewayTests
{
    [Theory]
    [InlineData(FormWindowState.Maximized)]
    [InlineData(FormWindowState.Minimized)]
    public void Restoring_before_snapping_aligns_the_visible_frame(FormWindowState state)
    {
        using var dpi = new PerMonitorDpiScope();
        using var form = new Form { StartPosition = FormStartPosition.Manual };
        form.Show();
        var area = Screen.FromHandle(form.Handle).WorkingArea;
        form.Bounds = new System.Drawing.Rectangle(area.X + 50, area.Y + 50, 640, 480);
        form.WindowState = state;
        Application.DoEvents();
        var target = new PixelRect(area.X + 100, area.Y + 100, 800, 600);

        Assert.True(new WindowsWindowService().Snap(form.Handle, target).Succeeded);
        Application.DoEvents();

        Assert.Equal(0, DwmGetWindowAttribute(form.Handle, 9, out var frame, Marshal.SizeOf<NativeRect>()));
        var visible = new PixelRect(frame.Left, frame.Top, frame.Right - frame.Left, frame.Bottom - frame.Top);
        Assert.True(visible.IsWithinTolerance(target, 2),
            $"Sichtbarer Rahmen {visible} weicht von der Zone {target} ab.");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(nint window, int attribute, out NativeRect value, int size);

    [DllImport("user32.dll")]
    private static extern nint SetThreadDpiAwarenessContext(nint context);

    private sealed class PerMonitorDpiScope : IDisposable
    {
        private readonly nint previous = SetThreadDpiAwarenessContext(-4);

        public void Dispose() => SetThreadDpiAwarenessContext(previous);
    }

    [Fact]
    public void Invalid_handle_is_rejected_without_side_effects()
    {
        var service = new WindowsWindowService();
        var identity = new WindowIdentity(0, 0, string.Empty);

        Assert.Null(service.Capture(0));
        Assert.False(service.TryApplyNormal(identity, new PixelRect(0, 0, 800, 600)));
    }

    [Fact]
    public void Visible_window_can_be_filled_then_restored()
    {
        using var form = new Form
        {
            StartPosition = FormStartPosition.Manual,
            Bounds = new System.Drawing.Rectangle(80, 90, 640, 480)
        };
        form.Show();
        Application.DoEvents();
        var service = new WindowsWindowService();
        var original = Assert.IsType<WindowPlacementSnapshot>(service.Capture(form.Handle));

        Assert.True(service.TryApplyNormal(
            original.Identity,
            new PixelRect(160, 170, 800, 600)));
        Assert.True(service.TryRestore(original));
        Application.DoEvents();

        var restored = Assert.IsType<WindowPlacementSnapshot>(service.Capture(form.Handle));
        Assert.Equal(original, restored);
    }
}
