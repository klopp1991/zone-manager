using SnapZones.Core.Geometry;
using SnapZones.Core.Models;
using SnapZones.Core.Monitors;
using SnapZones.Core.PartMonitors;
using Xunit;

namespace SnapZones.Tests.PartMonitors;

public sealed class PartMonitorResolverTests
{
    private static readonly Guid LeftId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid RightId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid FullId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void FindAt_resolves_negative_desktop_coordinates_and_boundary_to_right_part()
    {
        var resolver = CreateResolver();

        var placement = resolver.FindAt(new PointInt(-960, 400));

        Assert.NotNull(placement);
        Assert.Equal("LEFT-MONITOR", placement.MonitorId);
        Assert.Equal(RightId, placement.PartMonitorId);
        Assert.Equal(new PixelRect(-960, 0, 960, 1040), placement.Bounds);
    }

    [Fact]
    public void Resolve_applies_margins_and_gap_exactly_like_the_overlay()
    {
        // Seit dem 02.09.2026 landet das Fenster auf der Flaeche, die das Overlay zeigt. Frueher
        // zeigte die Vorschau Abstaende, gesetzt wurde aber die volle Zone.
        var metrics = new LayoutMetrics(8, 8);
        var resolver = CreateResolver(metrics);

        var placement = resolver.Resolve("LEFT-MONITOR", LeftId);

        Assert.NotNull(placement);
        Assert.Equal(
            ZoneGeometry.ToPixels(new NormalizedRect(0, 0, 0.5, 1), new MonitorWorkArea(-1920, 0, 1920, 1040), metrics),
            placement.Bounds);
        Assert.Equal(new PixelRect(-1912, 8, 948, 1024), placement.Bounds);
    }

    [Fact]
    public void FindAt_hits_a_zone_even_inside_the_gap_between_two_zones()
    {
        var resolver = CreateResolver(new LayoutMetrics(0, 20));

        // Ein Pixel rechts der Mitte liegt im Zwischenraum, gehoert aber zur rechten Zone.
        var placement = resolver.FindAt(new PointInt(-959, 400));

        Assert.NotNull(placement);
        Assert.Equal(RightId, placement.PartMonitorId);
    }

    [Fact]
    public void FindNearestMonitor_falls_back_to_the_closest_screen_when_the_cursor_is_on_the_taskbar()
    {
        var resolver = CreateResolver();

        // Unterhalb der Arbeitsflaeche des rechten Monitors: dort liegt die Taskleiste.
        var target = resolver.FindNearestMonitor(new PointInt(500, 1060));

        Assert.NotNull(target);
        Assert.Equal("RIGHT-MONITOR", target.Monitor.Identity.StableId);
        Assert.Null(resolver.FindPhysicalMonitor(new PointInt(500, 1060)));
    }

    [Fact]
    public void Cycle_uses_monitor_then_zone_order_and_wraps()
    {
        var resolver = CreateResolver();

        var next = resolver.Cycle("RIGHT-MONITOR", FullId, 1);
        var previous = resolver.Cycle("LEFT-MONITOR", LeftId, -1);

        Assert.Equal(LeftId, next?.PartMonitorId);
        Assert.Equal(FullId, previous?.PartMonitorId);
    }

    private static PartMonitorResolver CreateResolver(LayoutMetrics? metrics = null)
    {
        var left = new LiveMonitor(
            new MonitorIdentity("LEFT-MONITOR", "DISPLAY1", "Links"),
            new MonitorWorkArea(-1920, 0, 1920, 1040),
            96,
            96,
            false,
            Bounds: new PixelRect(-1920, 0, 1920, 1080));
        var right = new LiveMonitor(
            new MonitorIdentity("RIGHT-MONITOR", "DISPLAY2", "Rechts"),
            new MonitorWorkArea(0, 0, 1920, 1040),
            96,
            96,
            true,
            Bounds: new PixelRect(0, 0, 1920, 1080));

        return new PartMonitorResolver(
        [
            new PartMonitorTarget(left,
            [
                new ZoneDefinition(LeftId, "Links", new NormalizedRect(0, 0, 0.5, 1)),
                new ZoneDefinition(RightId, "Rechts", new NormalizedRect(0.5, 0, 0.5, 1))
            ]),
            new PartMonitorTarget(right,
            [
                new ZoneDefinition(FullId, "Voll", NormalizedRect.Full)
            ])
        ],
        metrics ?? new LayoutMetrics(0, 0));
    }
}
