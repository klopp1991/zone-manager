using SnapZones.Core.Geometry;
using SnapZones.Core.Placement;
using SnapZones.Windows.Native;
using SnapZones.Windows.Windows;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Xunit;

namespace SnapZones.Tests.Windows;

public sealed class WindowsPlacementWindowServiceTests
{
    [Fact]
    public void Inspect_reads_class_normal_bounds_and_main_kind_from_a_controlled_form()
    {
        using var form = new Form
        {
            Bounds = new System.Drawing.Rectangle(120, 90, 900, 600),
            StartPosition = FormStartPosition.Manual,
            Text = "Placement test"
        };
        form.Show();

        var snapshot = new WindowsPlacementWindowService().Inspect(form.Handle, excludedProcessId: -1);

        Assert.NotNull(snapshot);
        Assert.Equal(Path.GetFullPath(Environment.ProcessPath!), snapshot.Identity.ApplicationKey);
        Assert.StartsWith("WindowsForms10.Window.", snapshot.Identity.WindowClass, StringComparison.Ordinal);
        Assert.Equal(WindowKind.MainWindow, snapshot.Identity.Kind);
        Assert.Equal(new PixelRect(120, 90, 900, 600), snapshot.NormalBounds);
        Assert.False(snapshot.IsMinimized);
    }

    [Fact]
    public void Inspect_classifies_an_owned_normal_window_as_dialog()
    {
        using var owner = new Form();
        using var dialog = new Form();
        owner.Show();
        dialog.Show(owner);

        var snapshot = new WindowsPlacementWindowService().Inspect(dialog.Handle, excludedProcessId: -1);

        Assert.NotNull(snapshot);
        Assert.Equal(WindowKind.Dialog, snapshot.Identity.Kind);
    }

    [Fact]
    public void TryPlace_rejects_an_invalid_handle_without_side_effects() =>
        Assert.False(new WindowsPlacementWindowService().TryPlace(0, new PixelRect(10, 10, 800, 600), false));

    [Fact]
    public void TryPlace_keeps_the_foreground_window_when_placing_a_background_window_normally()
    {
        using var target = CreateControlledForm(new System.Drawing.Rectangle(120, 90, 900, 600));
        using var foreground = CreateControlledForm(new System.Drawing.Rectangle(1080, 90, 700, 500));
        target.Show();
        target.WindowState = FormWindowState.Maximized;
        foreground.Show();
        Activate(foreground);
        var service = new WindowsPlacementWindowService();
        var foregroundBefore = service.GetForegroundWindow();

        var placed = service.TryPlace(target.Handle, new PixelRect(210, 140, 840, 560), maximize: false);
        var snapshot = service.Inspect(target.Handle, excludedProcessId: -1);

        Assert.True(placed);
        Assert.Equal(foreground.Handle, foregroundBefore);
        Assert.Equal(foregroundBefore, service.GetForegroundWindow());
        Assert.NotNull(snapshot);
        Assert.Equal(new PixelRect(210, 140, 840, 560), snapshot.NormalBounds);
        Assert.False(snapshot.IsMaximized);
        Assert.False(snapshot.IsMinimized);
    }

    [Fact]
    public void TryPlace_rejects_background_maximize_without_changing_foreground()
    {
        using var target = CreateControlledForm(new System.Drawing.Rectangle(120, 90, 900, 600));
        using var foreground = CreateControlledForm(new System.Drawing.Rectangle(1080, 90, 700, 500));
        target.Show();
        foreground.Show();
        Activate(foreground);
        var service = new WindowsPlacementWindowService();
        var foregroundBefore = service.GetForegroundWindow();

        var placed = service.TryPlace(target.Handle, new PixelRect(210, 140, 840, 560), maximize: true);
        var snapshot = service.Inspect(target.Handle, excludedProcessId: -1);

        Assert.False(placed);
        Assert.Equal(foreground.Handle, foregroundBefore);
        Assert.Equal(foregroundBefore, service.GetForegroundWindow());
        Assert.NotNull(snapshot);
        Assert.False(snapshot.IsMaximized);
    }

    [Fact]
    public void TryPlace_maximizes_and_restores_a_foreground_target_without_losing_foreground()
    {
        using var form = CreateControlledForm(new System.Drawing.Rectangle(120, 90, 900, 600));
        form.Show();
        Activate(form);
        var service = new WindowsPlacementWindowService();

        var maximized = service.TryPlace(form.Handle, new PixelRect(210, 140, 840, 560), maximize: true);
        var maximizedSnapshot = service.Inspect(form.Handle, excludedProcessId: -1);
        var foregroundAfterMaximize = service.GetForegroundWindow();
        var restored = service.TryPlace(form.Handle, new PixelRect(260, 180, 800, 520), maximize: false);
        var restoredSnapshot = service.Inspect(form.Handle, excludedProcessId: -1);
        var foregroundAfterRestore = service.GetForegroundWindow();

        Assert.True(maximized);
        Assert.NotNull(maximizedSnapshot);
        Assert.True(maximizedSnapshot.IsMaximized);
        Assert.Equal(new PixelRect(210, 140, 840, 560), maximizedSnapshot.NormalBounds);
        Assert.Equal(form.Handle, foregroundAfterMaximize);
        Assert.True(restored);
        Assert.NotNull(restoredSnapshot);
        Assert.False(restoredSnapshot.IsMaximized);
        Assert.False(restoredSnapshot.IsMinimized);
        Assert.Equal(new PixelRect(260, 180, 800, 520), restoredSnapshot.NormalBounds);
        Assert.Equal(form.Handle, foregroundAfterRestore);
    }

    [Fact]
    public void Inspect_fails_closed_when_native_style_read_fails()
    {
        using var form = new Form
        {
            Bounds = new System.Drawing.Rectangle(120, 90, 900, 600),
            StartPosition = FormStartPosition.Manual
        };
        form.Show();

        var snapshot = new WindowsPlacementWindowService(new FailingWindowStyleReader())
            .Inspect(form.Handle, excludedProcessId: -1);

        Assert.Null(snapshot);
    }

    [Fact]
    public void Inspect_rejects_the_excluded_process_and_owned_tool_windows()
    {
        using var main = new Form();
        using var tool = new Form { ShowInTaskbar = false, FormBorderStyle = FormBorderStyle.FixedToolWindow };
        main.Show();
        tool.Show(main);
        var service = new WindowsPlacementWindowService();

        Assert.Null(service.Inspect(main.Handle, Environment.ProcessId));
        Assert.Null(service.Inspect(tool.Handle, excludedProcessId: -1));
    }

    [Fact]
    public void WorkspaceToScreen_applies_non_zero_monitor_work_area_offset()
    {
        var monitor = new RectNative { Left = -1920, Top = 100, Right = 0, Bottom = 1180 };
        var workArea = new RectNative { Left = -1872, Top = 130, Right = 0, Bottom = 1140 };

        var actual = WindowsPlacementWindowService.WorkspaceToScreen(
            new PixelRect(-1820, 75, 800, 600),
            monitor,
            workArea);

        Assert.Equal(new PixelRect(-1772, 105, 800, 600), actual);
    }

    [Fact]
    public void ScreenToWorkspace_removes_non_zero_monitor_work_area_offset()
    {
        var monitor = new RectNative { Left = -1920, Top = 100, Right = 0, Bottom = 1180 };
        var workArea = new RectNative { Left = -1872, Top = 130, Right = 0, Bottom = 1140 };

        var actual = WindowsPlacementWindowService.ScreenToWorkspace(
            new PixelRect(-1772, 105, 800, 600),
            monitor,
            workArea);

        Assert.Equal(new PixelRect(-1820, 75, 800, 600), actual);
    }

    private static Form CreateControlledForm(System.Drawing.Rectangle bounds) => new()
    {
        Bounds = bounds,
        StartPosition = FormStartPosition.Manual
    };

    private static void Activate(Form form)
    {
        var currentThread = GetCurrentThreadId();
        var foregroundThread = User32.GetWindowThreadProcessId(User32.GetForegroundWindow(), out _);
        var attached = foregroundThread != 0 &&
            foregroundThread != currentThread &&
            AttachThreadInput(currentThread, foregroundThread, attach: true);
        try
        {
            _ = BringWindowToTop(form.Handle);
            _ = SetActiveWindow(form.Handle);
            _ = SetFocus(form.Handle);
        }
        finally
        {
            if (attached)
            {
                _ = AttachThreadInput(currentThread, foregroundThread, attach: false);
            }
        }

        var deadline = DateTime.UtcNow.AddSeconds(2);
        var service = new WindowsPlacementWindowService();
        while (service.GetForegroundWindow() != form.Handle && DateTime.UtcNow < deadline)
        {
            Application.DoEvents();
            Thread.Sleep(10);
        }

        Assert.Equal(form.Handle, service.GetForegroundWindow());
    }

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachThreadInput(uint attachThread, uint attachToThread, [MarshalAs(UnmanagedType.Bool)] bool attach);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool BringWindowToTop(nint window);

    [DllImport("user32.dll")]
    private static extern nint SetActiveWindow(nint window);

    [DllImport("user32.dll")]
    private static extern nint SetFocus(nint window);

    private sealed class FailingWindowStyleReader : IWindowStyleReader
    {
        public bool TryRead(nint window, int index, out long value)
        {
            value = 0;
            return false;
        }
    }
}
