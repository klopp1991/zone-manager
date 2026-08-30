using SnapZones.Core.Geometry;
using SnapZones.Core.Models;
using Xunit;

namespace SnapZones.Tests.Geometry;

public sealed class ZoneMagnetismTests
{
    [Fact]
    public void SnapMove_attaches_left_edge_to_other_zone_right_edge_within_pixel_threshold()
    {
        var moving = new NormalizedRect(0.507, 0, 0.25, 1);
        var other = new NormalizedRect(0, 0, 0.5, 1);

        var result = ZoneMagnetism.SnapMove(moving, [other], 20, 1920, 1080);

        Assert.Equal(new NormalizedRect(0.5, 0, 0.25, 1), result);
    }

    [Fact]
    public void SnapMove_leaves_zone_free_outside_threshold()
    {
        var moving = new NormalizedRect(0.53, 0, 0.25, 1);
        var other = new NormalizedRect(0, 0, 0.5, 1);

        var result = ZoneMagnetism.SnapMove(moving, [other], 20, 1920, 1080);

        Assert.Equal(moving, result);
    }

    [Fact]
    public void SnapResize_attaches_only_moving_right_edge()
    {
        var resizing = new NormalizedRect(0, 0, 0.493, 1);
        var other = new NormalizedRect(0.5, 0, 0.5, 1);

        var result = ZoneMagnetism.SnapResize(
            resizing, [other], ZoneEdges.Right, 20, 1920, 1080);

        Assert.Equal(new NormalizedRect(0, 0, 0.5, 1), result);
    }
}

