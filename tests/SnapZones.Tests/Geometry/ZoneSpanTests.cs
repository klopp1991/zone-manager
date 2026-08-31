using SnapZones.Core.Geometry;
using SnapZones.Core.Models;
using Xunit;

namespace SnapZones.Tests.Geometry;

public sealed class ZoneSpanTests
{
    private static readonly MonitorWorkArea Area = new(0, 0, 1920, 1040);

    private static ZoneDefinition Zone(string name, double x, double y, double width, double height) =>
        new(Guid.NewGuid(), name, new NormalizedRect(x, y, width, height));

    [Fact]
    public void A_single_zone_spans_exactly_itself()
    {
        var zone = Zone("Links", 0, 0, 0.5, 1);

        Assert.Equal(
            ZoneGeometry.ToPixels(zone.Bounds, Area),
            ZoneSpan.BoundingBox([zone], Area));
    }

    [Fact]
    public void Two_neighbouring_zones_span_their_union()
    {
        var left = Zone("Links", 0, 0, 0.5, 1);
        var right = Zone("Rechts", 0.5, 0, 0.5, 1);

        Assert.Equal(new PixelRect(0, 0, 1920, 1040), ZoneSpan.BoundingBox([left, right], Area));
    }

    [Fact]
    public void The_order_the_zones_were_collected_in_does_not_matter()
    {
        var left = Zone("Links", 0, 0, 0.5, 0.5);
        var right = Zone("Rechts", 0.5, 0.5, 0.5, 0.5);

        Assert.Equal(
            ZoneSpan.BoundingBox([left, right], Area),
            ZoneSpan.BoundingBox([right, left], Area));
    }

    [Fact]
    public void Zones_that_do_not_touch_still_span_one_rectangle_covering_the_gap()
    {
        var topLeft = Zone("Oben links", 0, 0, 0.25, 0.25);
        var bottomRight = Zone("Unten rechts", 0.75, 0.75, 0.25, 0.25);

        // Spanning opposite corners deliberately covers everything in between,
        // because a window can only ever be one rectangle.
        Assert.Equal(new PixelRect(0, 0, 1920, 1040), ZoneSpan.BoundingBox([topLeft, bottomRight], Area));
    }

    [Fact]
    public void Overlapping_zones_span_their_outer_bounds()
    {
        var wide = Zone("Breit", 0, 0.25, 1, 0.5);
        var tall = Zone("Hoch", 0.25, 0, 0.5, 1);

        Assert.Equal(new PixelRect(0, 0, 1920, 1040), ZoneSpan.BoundingBox([wide, tall], Area));
    }

    [Fact]
    public void A_span_is_offset_by_the_work_area_of_a_secondary_monitor()
    {
        var secondary = new MonitorWorkArea(1920, 0, 1920, 1040);
        var left = Zone("Links", 0, 0, 0.5, 1);
        var right = Zone("Rechts", 0.5, 0, 0.5, 1);

        Assert.Equal(new PixelRect(1920, 0, 1920, 1040), ZoneSpan.BoundingBox([left, right], secondary));
    }

    [Fact]
    public void An_empty_selection_is_rejected() =>
        Assert.Throws<ArgumentException>(() => ZoneSpan.BoundingBox([], Area));

    [Fact]
    public void A_span_is_described_by_joining_the_zone_names()
    {
        var left = Zone("Links", 0, 0, 0.5, 1);
        var right = Zone("Rechts", 0.5, 0, 0.5, 1);

        Assert.Equal("Links + Rechts", ZoneSpan.Describe([left, right]));
        Assert.Equal("Links", ZoneSpan.Describe([left]));
    }
}
