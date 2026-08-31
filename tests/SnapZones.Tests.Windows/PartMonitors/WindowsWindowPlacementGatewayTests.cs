using System.Windows.Forms;
using SnapZones.Core.Geometry;
using SnapZones.Core.PartMonitors;
using SnapZones.Windows.Windows;
using Xunit;

namespace SnapZones.Tests.PartMonitors;

public sealed class WindowsWindowPlacementGatewayTests
{
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
