using SnapZones.Core.Geometry;
using SnapZones.Core.Models;
using Xunit;

namespace SnapZones.Tests.Geometry;

public sealed class ZoneGeometryTests
{
    [Theory]
    [InlineData(-1920, 0, 1920, 1080, 8, 8, -1912, 8, 948, 1064)]
    [InlineData(0, 0, 3440, 1400, 10, 12, 10, 10, 1704, 1380)]
    public void ToPixels_applies_margin_gap_and_negative_origins(
        int x, int y, int width, int height, int margin, int gap,
        int expectedX, int expectedY, int expectedWidth, int expectedHeight)
    {
        var area = new MonitorWorkArea(x, y, width, height);
        var zone = new NormalizedRect(0, 0, 0.5, 1);

        var actual = ZoneGeometry.ToPixels(zone, area, new LayoutMetrics(margin, gap));

        Assert.Equal(new PixelRect(expectedX, expectedY, expectedWidth, expectedHeight), actual);
    }

    [Fact]
    public void ToPixels_without_overlay_metrics_preserves_exact_layout_bounds()
    {
        var area = new MonitorWorkArea(-1920, 20, 1920, 1040);

        var actual = ZoneGeometry.ToPixels(
            new NormalizedRect(0.5, 0, 0.5, 1),
            area);

        Assert.Equal(new PixelRect(-960, 20, 960, 1040), actual);
    }

    [Fact]
    public void Validate_rejects_overlapping_zones()
    {
        var zones = new[]
        {
            Zone("Haupt", 0, 0, 0.6, 1),
            Zone("Web", 0.5, 0, 0.5, 1)
        };

        var result = ZoneGeometry.Validate(zones);

        Assert.Contains(result.Errors, error => error.Code == "overlap");
    }

    [Fact]
    public void HitTest_includes_rightmost_and_bottommost_pixels()
    {
        var zone = Zone("Voll", 0, 0, 1, 1);
        var area = new MonitorWorkArea(0, 0, 1920, 1040);

        var hit = ZoneGeometry.HitTest([zone], area, new PointInt(1919, 1039));

        Assert.Equal(zone, hit);
    }

    [Fact]
    public void ToPixels_applies_independent_outer_margins()
    {
        var area = new MonitorWorkArea(0, 0, 2000, 1000);

        var actual = ZoneGeometry.ToPixels(
            NormalizedRect.Full,
            area,
            new LayoutMetrics(new EdgeInsets(10, 20, 30, 40), 0));

        Assert.Equal(new PixelRect(10, 20, 1960, 940), actual);
    }

    private static ZoneDefinition Zone(string name, double x, double y, double width, double height) =>
        new(Guid.NewGuid(), name, new NormalizedRect(x, y, width, height));
}
