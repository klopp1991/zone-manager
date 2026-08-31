using ZoneManager.Core.Geometry;
using ZoneManager.Core.Models;
using ZoneManager.Core.Monitors;
using ZoneManager.Core.PartMonitors;
using Xunit;

namespace ZoneManager.Tests.PartMonitors;

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
    public void Resolve_returns_exact_layout_bounds_even_when_overlay_has_margins_and_gap()
    {
        var resolver = CreateResolver(new LayoutMetrics(8, 8));

        var placement = resolver.Resolve("LEFT-MONITOR", LeftId);

        Assert.NotNull(placement);
        Assert.Equal(new PixelRect(-1920, 0, 960, 1040), placement.Bounds);
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
            false);
        var right = new LiveMonitor(
            new MonitorIdentity("RIGHT-MONITOR", "DISPLAY2", "Rechts"),
            new MonitorWorkArea(0, 0, 1920, 1040),
            96,
            96,
            true);

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
