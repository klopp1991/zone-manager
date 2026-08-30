using SnapZones.Core.Geometry;
using SnapZones.Core.Placement;
using SnapZones.Windows.Native;
using SnapZones.Windows.Windows;
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
    public void TryPlace_changes_normal_screen_bounds_of_a_controlled_form()
    {
        using var form = new Form
        {
            Bounds = new System.Drawing.Rectangle(120, 90, 900, 600),
            StartPosition = FormStartPosition.Manual
        };
        form.Show();
        var service = new WindowsPlacementWindowService();

        var placed = service.TryPlace(form.Handle, new PixelRect(210, 140, 840, 560), maximize: false);
        var snapshot = service.Inspect(form.Handle, excludedProcessId: -1);

        Assert.True(placed);
        Assert.NotNull(snapshot);
        Assert.Equal(new PixelRect(210, 140, 840, 560), snapshot.NormalBounds);
        Assert.False(snapshot.IsMaximized);
        Assert.False(snapshot.IsMinimized);
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
}
