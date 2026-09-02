using SnapZones.Core.Geometry;
using Xunit;

namespace SnapZones.Tests.Geometry;

public sealed class PixelRectTests
{
    [Fact]
    public void Tolerance_accepts_the_invisible_border_and_rejects_a_real_deviation()
    {
        var zone = new PixelRect(0, 0, 960, 1040);
        var snappedWithBorder = new PixelRect(-7, 0, 974, 1047);
        var elsewhere = new PixelRect(100, 0, 960, 1040);

        Assert.True(snappedWithBorder.IsWithinTolerance(zone, 13));
        Assert.False(elsewhere.IsWithinTolerance(zone, 13));
        Assert.False(new PixelRect(0, 0, 0, 0).IsWithinTolerance(zone, 13));
    }

    [Fact]
    public void A_fixed_size_window_is_centred_in_its_zone()
    {
        var zone = new PixelRect(100, 100, 800, 600);

        var centred = new PixelRect(0, 0, 400, 300).CenteredIn(zone);

        Assert.Equal(new PixelRect(300, 250, 400, 300), centred);
    }

    [Fact]
    public void A_window_larger_than_the_zone_sticks_to_the_top_left_corner()
    {
        var zone = new PixelRect(100, 100, 300, 200);

        Assert.Equal(new PixelRect(100, 100, 500, 400), new PixelRect(0, 0, 500, 400).CenteredIn(zone));
    }

    [Fact]
    public void Distance_is_zero_inside_and_grows_outside()
    {
        var rect = new PixelRect(0, 0, 100, 100);

        Assert.Equal(0, rect.DistanceSquaredTo(new PointInt(50, 50)));
        Assert.Equal(0, rect.DistanceSquaredTo(new PointInt(99, 99)));
        Assert.Equal(25, rect.DistanceSquaredTo(new PointInt(50, 104)));
        Assert.Equal(2, rect.DistanceSquaredTo(new PointInt(-1, -1)));
    }
}
