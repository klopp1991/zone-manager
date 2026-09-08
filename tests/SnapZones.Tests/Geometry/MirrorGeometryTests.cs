using SnapZones.Core.Geometry;
using Xunit;

namespace SnapZones.Tests.Geometry;

/// <summary>
/// Zone und virtueller Monitor sind meist gleich gross; sind sie es nicht, liegt das Bild mittig mit
/// Rand oder wird mittig beschnitten, und der Zeiger muss in beide Richtungen auf denselben Punkt
/// treffen.
/// </summary>
public sealed class MirrorGeometryTests
{
    private static readonly PixelRect Zone = new(8, 8, 2288, 1296);
    private static readonly PixelRect Virtual = new(6144, 0, 2288, 1296);

    [Fact]
    public void Equal_sizes_copy_the_whole_surface()
    {
        var plan = MirrorGeometry.Plan(2288, 1296, 2288, 1296);

        Assert.Equal(new MirrorPlan(0, 0, 0, 0, 2288, 1296), plan);
        Assert.True(plan.IsExact);
    }

    [Fact]
    public void A_smaller_monitor_is_centred_with_a_border()
    {
        var plan = MirrorGeometry.Plan(2288, 1296, 1920, 1080);

        Assert.Equal(new MirrorPlan(0, 0, 184, 108, 1920, 1080), plan);
        Assert.False(plan.IsExact);
    }

    [Fact]
    public void A_larger_monitor_is_cropped_in_the_middle()
    {
        var plan = MirrorGeometry.Plan(1920, 1080, 2288, 1296);

        Assert.Equal(new MirrorPlan(184, 108, 0, 0, 1920, 1080), plan);
    }

    [Fact]
    public void Pointer_positions_map_both_ways_and_stay_inside()
    {
        var exact = MirrorGeometry.Plan(Zone.Width, Zone.Height, Virtual.Width, Virtual.Height);
        Assert.Equal(new PointInt(6161, 592), MirrorGeometry.ToVirtual(exact, Zone, Virtual, new PointInt(25, 600)));
        Assert.Equal(new PointInt(25, 600), MirrorGeometry.ToZone(exact, Zone, Virtual, new PointInt(6161, 592)));

        var bordered = MirrorGeometry.Plan(Zone.Width, Zone.Height, 1920, 1080);
        var small = Virtual with { Width = 1920, Height = 1080 };
        Assert.Equal(new PointInt(6144, 0), MirrorGeometry.ToVirtual(bordered, Zone, small, new PointInt(8, 8)));
        Assert.Equal(new PointInt(6144 + 1919, 1079), MirrorGeometry.ToVirtual(bordered, Zone, small, new PointInt(8 + 2287, 8 + 1295)));
        Assert.Equal(new PointInt(8 + 184, 8 + 108), MirrorGeometry.ToZone(bordered, Zone, small, new PointInt(6144, 0)));

        var cropped = MirrorGeometry.Plan(1920, 1080, Virtual.Width, Virtual.Height);
        var smallZone = Zone with { Width = 1920, Height = 1080 };
        Assert.Equal(new PointInt(6144 + 184, 108), MirrorGeometry.ToVirtual(cropped, smallZone, Virtual, new PointInt(8, 8)));
        Assert.Equal(new PointInt(8, 8), MirrorGeometry.ToZone(cropped, smallZone, Virtual, new PointInt(6144 + 184, 108)));
        Assert.Equal(new PointInt(8, 8), MirrorGeometry.ToZone(cropped, smallZone, Virtual, new PointInt(6144, 0)));
    }
}
