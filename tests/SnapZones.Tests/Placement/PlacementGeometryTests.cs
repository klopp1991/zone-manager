using SnapZones.Core.Geometry;
using SnapZones.Core.Models;
using SnapZones.Core.Placement;
using Xunit;

namespace SnapZones.Tests.Placement;

public sealed class PlacementGeometryTests
{
    private static readonly WindowIdentity Identity = new("C:\\Apps\\excel.exe", "XLMAIN", WindowKind.MainWindow);

    [Fact]
    public void Resolve_uses_exact_pixels_when_the_saved_work_area_is_unchanged()
    {
        var entry = Entry("DISPLAY-A", new PixelRect(120, 80, 1200, 800));
        var actual = PlacementGeometry.Resolve(
            entry,
            [new PlacementMonitorTarget("DISPLAY-A", new MonitorWorkArea(0, 0, 1920, 1080), true)],
            []);
        Assert.Equal(new PixelRect(120, 80, 1200, 800), actual);
    }

    [Fact]
    public void Resolve_maps_to_the_primary_monitor_and_keeps_the_window_visible_when_saved_monitor_is_missing()
    {
        var entry = Entry("MISSING", new PixelRect(1500, 700, 900, 700));
        var actual = PlacementGeometry.Resolve(
            entry,
            [new PlacementMonitorTarget("DISPLAY-B", new MonitorWorkArea(100, 50, 1280, 720), true)],
            []);
        Assert.True(actual.X >= 100 && actual.Y >= 50);
        Assert.True(actual.Right <= 1380 && actual.Bottom <= 770);
        Assert.True(actual.Width >= 160 && actual.Height >= 120);
    }

    [Fact]
    public void ClassifyZone_returns_the_unique_zone_with_at_least_twenty_five_percent_overlap()
    {
        var profile = Guid.NewGuid();
        var zones = new[]
        {
            new PlacementZoneTarget(profile, Guid.NewGuid(), "DISPLAY-A", new PixelRect(0, 0, 960, 1080)),
            new PlacementZoneTarget(profile, Guid.NewGuid(), "DISPLAY-A", new PixelRect(960, 0, 960, 1080))
        };
        Assert.Equal(zones[1].ZoneId, PlacementGeometry.ClassifyZone(new PixelRect(1000, 100, 800, 800), zones));
    }

    [Fact]
    public void ClassifyZone_returns_null_when_the_highest_overlap_is_exactly_equal()
    {
        var profile = Guid.NewGuid();
        var zones = new[]
        {
            new PlacementZoneTarget(profile, Guid.NewGuid(), "DISPLAY-A", new PixelRect(0, 0, 500, 1000)),
            new PlacementZoneTarget(profile, Guid.NewGuid(), "DISPLAY-A", new PixelRect(500, 0, 500, 1000))
        };

        Assert.Null(PlacementGeometry.ClassifyZone(new PixelRect(0, 0, 1000, 1000), zones));
    }

    [Fact]
    public void ClassifyZone_chooses_the_larger_overlap_when_the_difference_is_one_pixel()
    {
        var profile = Guid.NewGuid();
        var larger = Guid.NewGuid();
        var smaller = Guid.NewGuid();
        var zones = new[]
        {
            new PlacementZoneTarget(profile, larger, "DISPLAY-A", new PixelRect(0, 0, 500, 1001)),
            new PlacementZoneTarget(profile, smaller, "DISPLAY-A", new PixelRect(499, 0, 501, 999))
        };

        Assert.Equal(larger, PlacementGeometry.ClassifyZone(new PixelRect(0, 0, 1000, 1001), zones));
    }

    [Fact]
    public void ClassifyZone_handles_maximum_positive_pixel_dimensions_without_overflow()
    {
        var profile = Guid.NewGuid();
        var qualifyingZone = Guid.NewGuid();
        var zones = new[]
        {
            new PlacementZoneTarget(profile, qualifyingZone, "DISPLAY-A", new PixelRect(0, 0, int.MaxValue, int.MaxValue))
        };

        Assert.Equal(
            qualifyingZone,
            PlacementGeometry.ClassifyZone(new PixelRect(0, 0, int.MaxValue, int.MaxValue), zones));
    }

    private static WindowPlacementEntry Entry(string monitorId, PixelRect bounds) => new(
        Identity, monitorId, null, new MonitorWorkArea(0, 0, 1920, 1080), bounds,
        PlacementGeometry.Normalize(bounds, new MonitorWorkArea(0, 0, 1920, 1080)),
        false, DateTimeOffset.Parse("2026-08-30T12:00:00Z"));
}
