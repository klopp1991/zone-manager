using SnapZones.Core.Geometry;
using SnapZones.Core.Monitors;
using Xunit;

namespace SnapZones.Tests.Monitors;

public sealed class VirtualDisplayPlacementTests
{
    [Fact]
    public void The_virtual_monitor_goes_right_of_the_rightmost_monitor_top_aligned()
    {
        var monitors = new[]
        {
            new PixelRect(0, 0, 6144, 2560),
            new PixelRect(-2560, 200, 2560, 1440)
        };

        Assert.Equal(new PointInt(6144, 0), VirtualDisplayPlacement.ChoosePosition(monitors));
    }

    [Fact]
    public void Two_monitors_with_the_same_right_edge_yield_the_upper_one()
    {
        var monitors = new[]
        {
            new PixelRect(0, 1440, 2560, 1440),
            new PixelRect(0, 0, 2560, 1440)
        };

        Assert.Equal(new PointInt(2560, 0), VirtualDisplayPlacement.ChoosePosition(monitors));
    }

    [Fact]
    public void Empty_or_degenerate_input_falls_back_to_the_origin()
    {
        Assert.Equal(new PointInt(0, 0), VirtualDisplayPlacement.ChoosePosition([]));
        Assert.Equal(new PointInt(0, 0), VirtualDisplayPlacement.ChoosePosition([new PixelRect(5, 5, 0, 0)]));
    }
}
