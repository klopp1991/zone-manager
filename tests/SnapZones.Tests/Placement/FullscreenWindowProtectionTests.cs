using System.Windows.Forms;
using SnapZones.Core.Geometry;
using SnapZones.Windows.Windows;
using Xunit;

namespace SnapZones.Tests.Placement;

public sealed class FullscreenWindowProtectionTests
{
    [Fact]
    public void Rule_inspection_recognises_fullscreen_and_snapping_leaves_it_unchanged()
    {
        using var form = new BorderlessResizableForm();
        form.Show();
        form.Bounds = Screen.FromHandle(form.Handle).Bounds;
        Application.DoEvents();
        var original = form.Bounds;
        var service = new WindowsWindowService();

        var candidate = Assert.IsType<WindowRuleCandidate>(service.InspectRuleCandidate(form.Handle, -1));
        Assert.True(candidate.IsFullscreen);
        var result = service.Snap(form.Handle, new PixelRect(original.X + 20, original.Y + 20, 600, 400));

        Assert.False(result.Succeeded);
        Assert.Contains("Vollbild", result.Rejection);
        Assert.Equal(original, form.Bounds);
    }

    [Fact]
    public void Placement_service_does_not_resize_a_fullscreen_window()
    {
        using var form = new BorderlessResizableForm();
        form.Show();
        form.Bounds = Screen.FromHandle(form.Handle).Bounds;
        Application.DoEvents();
        var original = form.Bounds;
        var service = new WindowsPlacementWindowService();

        Assert.True(Assert.IsType<PlacementWindowSnapshot>(service.Inspect(form.Handle, -1)).IsFullscreen);
        Assert.False(service.TryPlace(form.Handle, new PixelRect(original.X + 20, original.Y + 20, 600, 400), false));
        Assert.Equal(original, form.Bounds);
    }

    [Fact]
    public void Rule_inspection_recognises_a_minimized_window()
    {
        using var form = new Form();
        form.Show();
        form.WindowState = FormWindowState.Minimized;
        Application.DoEvents();

        var candidate = Assert.IsType<WindowRuleCandidate>(new WindowsWindowService().InspectRuleCandidate(form.Handle, -1));

        Assert.True(candidate.IsMinimized);
    }

    [Fact]
    public void A_borderless_window_inside_a_zone_can_still_be_snapped()
    {
        using var form = new BorderlessResizableForm();
        form.Show();
        var area = Screen.FromHandle(form.Handle).WorkingArea;
        form.Bounds = new System.Drawing.Rectangle(area.X + 30, area.Y + 30, 500, 350);
        Application.DoEvents();
        var service = new WindowsWindowService();

        Assert.False(Assert.IsType<WindowRuleCandidate>(service.InspectRuleCandidate(form.Handle, -1)).IsFullscreen);
        Assert.True(service.Snap(form.Handle, new PixelRect(area.X + 40, area.Y + 40, 600, 400)).Succeeded);
    }

    // Ein rahmenloses Programmfenster mit Groessenrahmen wie bei Browsern; ohne Popup-Filter.
    private sealed class BorderlessResizableForm : Form
    {
        protected override bool ShowWithoutActivation => true;

        protected override CreateParams CreateParams
        {
            get
            {
                var parameters = base.CreateParams;
                parameters.Style = (parameters.Style & ~0x00C00000) | 0x00040000;
                return parameters;
            }
        }
    }
}
