using SnapZones.Core.Fullscreen;
using SnapZones.Core.Geometry;
using SnapZones.Core.Placement;
using Xunit;

namespace SnapZones.Tests.Fullscreen;

public sealed class ZoneFullscreenTests
{
    private static readonly Guid Profile = Guid.NewGuid();
    private static readonly PixelRect Monitor = new(0, 0, 1920, 1080);

    [Fact]
    public void CoversMonitor_accepts_a_window_on_the_full_monitor_area()
    {
        Assert.True(ZoneFullscreen.CoversMonitor(new PixelRect(0, 0, 1920, 1080), Monitor));
    }

    [Fact]
    public void CoversMonitor_accepts_a_window_that_reaches_one_pixel_beyond_the_monitor()
    {
        // Manche Programme setzen sich im Vollbild um ein Pixel groesser als der Monitor.
        Assert.True(ZoneFullscreen.CoversMonitor(new PixelRect(-1, -1, 1922, 1082), Monitor));
    }

    [Fact]
    public void CoversMonitor_rejects_a_window_that_ends_at_the_taskbar()
    {
        // Ein maximiertes Fenster endet an der Arbeitsflaeche und ist damit kein Vollbild.
        Assert.False(ZoneFullscreen.CoversMonitor(new PixelRect(0, 0, 1920, 1032), Monitor));
    }

    [Fact]
    public void CoversMonitor_rejects_a_window_in_a_zone()
    {
        Assert.False(ZoneFullscreen.CoversMonitor(new PixelRect(8, 8, 952, 1064), Monitor));
    }

    [Fact]
    public void FindSnappedArea_returns_the_zone_the_window_rests_on()
    {
        var zones = new[] { Zone(8, 8, 952, 1064), Zone(960, 8, 952, 1064) };
        // Der unsichtbare Griffbereich laesst das Fenster einige Pixel ueber die Zonenkanten hinausragen.
        var actual = ZoneFullscreen.FindSnappedArea(new PixelRect(953, 8, 966, 1071), zones, 40);
        Assert.Equal(new PixelRect(960, 8, 952, 1064), actual);
    }

    [Fact]
    public void FindSnappedArea_ignores_a_window_that_merely_overlaps_a_zone()
    {
        var zones = new[] { Zone(8, 8, 952, 1064), Zone(960, 8, 952, 1064) };
        Assert.Null(ZoneFullscreen.FindSnappedArea(new PixelRect(400, 300, 600, 400), zones, 40));
    }

    [Fact]
    public void FindSnappedArea_returns_the_combined_area_of_zones_dragged_together()
    {
        // Ein mit gedrueckter Strg-Taste ueber zwei Zonen gezogenes Fenster belegt deren gemeinsame
        // Flaeche und passt deshalb auf keine einzelne Zone.
        var zones = new[] { Zone(0, 0, 640, 1080), Zone(640, 0, 640, 1080), Zone(1280, 0, 640, 1080) };
        var actual = ZoneFullscreen.FindSnappedArea(new PixelRect(0, 0, 1280, 1080), zones, 40);
        Assert.Equal(new PixelRect(0, 0, 1280, 1080), actual);
    }

    [Fact]
    public void FindSnappedArea_returns_nothing_without_zones()
    {
        Assert.Null(ZoneFullscreen.FindSnappedArea(new PixelRect(0, 0, 800, 600), [], 40));
    }

    private static PlacementZoneTarget Zone(int x, int y, int width, int height) =>
        new(Profile, Guid.NewGuid(), "DISPLAY-A", new PixelRect(x, y, width, height));
}
