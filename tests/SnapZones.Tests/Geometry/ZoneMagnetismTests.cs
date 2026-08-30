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

    [Fact]
    public void SnapResize_extends_right_zone_left_edge_exactly_to_neighbour()
    {
        var resizing = new NormalizedRect(0.507, 0, 0.493, 1);
        var other = new NormalizedRect(0, 0, 0.5, 1);

        var result = ZoneMagnetism.SnapResize(
            resizing, [other], ZoneEdges.Left, 20, 1920, 1080);

        Assert.Equal(new NormalizedRect(0.5, 0, 0.5, 1), result);
    }

    [Theory]
    [InlineData(ZoneEdges.Left, 0.507, 0, 0.493, 1, 0.5, 0, 0.5, 1)]
    [InlineData(ZoneEdges.Right, 0, 0, 0.493, 1, 0, 0, 0.5, 1)]
    [InlineData(ZoneEdges.Top, 0, 0.507, 1, 0.493, 0, 0.5, 1, 0.5)]
    [InlineData(ZoneEdges.Bottom, 0, 0, 1, 0.493, 0, 0, 1, 0.5)]
    public void SnapResize_supports_every_moving_edge(
        ZoneEdges edge,
        double x,
        double y,
        double width,
        double height,
        double expectedX,
        double expectedY,
        double expectedWidth,
        double expectedHeight)
    {
        var resizing = new NormalizedRect(x, y, width, height);
        var neighbour = edge is ZoneEdges.Left or ZoneEdges.Right
            ? new NormalizedRect(0, 0, 0.5, 1)
            : new NormalizedRect(0, 0, 1, 0.5);

        var result = ZoneMagnetism.SnapResize(
            resizing, [neighbour], edge, 20, 1920, 1080);

        Assert.Equal(
            new NormalizedRect(expectedX, expectedY, expectedWidth, expectedHeight),
            result);
    }

    [Fact]
    public void SnapMoveWithResult_reports_both_visual_snap_edges()
    {
        var moving = new NormalizedRect(0.507, 0.507, 0.25, 0.25);
        var neighbour = new NormalizedRect(0, 0, 0.5, 0.5);

        var result = ZoneMagnetism.SnapMoveWithResult(
            moving, [neighbour], 20, 1920, 1080);

        Assert.Equal(new NormalizedRect(0.5, 0.5, 0.25, 0.25), result.Bounds);
        Assert.Equal(ZoneEdges.Left | ZoneEdges.Top, result.SnappedEdges);
    }

    [Fact]
    public void SnapResizeWithResult_reports_the_moving_left_edge_for_visual_feedback()
    {
        var resizing = new NormalizedRect(0.507, 0, 0.493, 1);
        var neighbour = new NormalizedRect(0, 0, 0.5, 1);

        var result = ZoneMagnetism.SnapResizeWithResult(
            resizing, [neighbour], ZoneEdges.Left, 20, 1920, 1080);

        Assert.Equal(new NormalizedRect(0.5, 0, 0.5, 1), result.Bounds);
        Assert.Equal(ZoneEdges.Left, result.SnappedEdges);
    }

    [Fact]
    public void SnapMoveWithResult_reports_no_visual_edge_outside_threshold()
    {
        var moving = new NormalizedRect(0.53, 0.53, 0.25, 0.25);
        var neighbour = new NormalizedRect(0, 0, 0.5, 0.5);

        var result = ZoneMagnetism.SnapMoveWithResult(
            moving, [neighbour], 20, 1920, 1080);

        Assert.Equal(moving, result.Bounds);
        Assert.Equal(ZoneEdges.None, result.SnappedEdges);
    }
}
