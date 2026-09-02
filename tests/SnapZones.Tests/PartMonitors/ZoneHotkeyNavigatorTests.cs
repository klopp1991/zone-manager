using SnapZones.Core.Geometry;
using SnapZones.Core.Models;
using SnapZones.Core.Monitors;
using SnapZones.Core.PartMonitors;
using Xunit;

namespace SnapZones.Tests.PartMonitors;

public sealed class ZoneHotkeyNavigatorTests
{
    private static readonly Guid LeftId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid RightId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly LayoutMetrics Metrics = new(0, 0);

    [Fact]
    public void Next_zone_from_a_snapped_window_cycles_to_the_neighbour()
    {
        var command = ZoneHotkeyNavigator.Plan(
            new ZoneHotkey(ZoneHotkeyAction.NextZone),
            (nint)42,
            new PixelRect(-7, 0, 974, 1047),
            Targets(),
            Metrics);

        var cycle = Assert.IsType<CyclePartMonitorCommand>(command);
        Assert.Equal(LeftId, cycle.CurrentPartMonitorId);
        Assert.Equal(1, cycle.Offset);
    }

    [Fact]
    public void Next_zone_from_a_free_window_starts_at_the_first_zone_and_previous_at_the_last()
    {
        var bounds = new PixelRect(300, 300, 400, 300);

        var next = Assert.IsType<FillPartMonitorCommand>(
            ZoneHotkeyNavigator.Plan(new ZoneHotkey(ZoneHotkeyAction.NextZone), (nint)1, bounds, Targets(), Metrics));
        var previous = Assert.IsType<FillPartMonitorCommand>(
            ZoneHotkeyNavigator.Plan(new ZoneHotkey(ZoneHotkeyAction.PreviousZone), (nint)1, bounds, Targets(), Metrics));

        Assert.Equal(LeftId, next.PartMonitorId);
        Assert.Equal(RightId, previous.PartMonitorId);
    }

    [Fact]
    public void Zone_by_number_targets_the_zone_on_the_windows_monitor_or_nothing()
    {
        var bounds = new PixelRect(1000, 100, 400, 300);

        var second = Assert.IsType<FillPartMonitorCommand>(
            ZoneHotkeyNavigator.Plan(new ZoneHotkey(ZoneHotkeyAction.ZoneByNumber, 2), (nint)1, bounds, Targets(), Metrics));
        Assert.Equal(RightId, second.PartMonitorId);
        Assert.Null(ZoneHotkeyNavigator.Plan(new ZoneHotkey(ZoneHotkeyAction.ZoneByNumber, 3), (nint)1, bounds, Targets(), Metrics));
    }

    [Fact]
    public void Restore_previous_needs_no_geometry()
    {
        var command = ZoneHotkeyNavigator.Plan(
            new ZoneHotkey(ZoneHotkeyAction.RestorePrevious), (nint)9, default, Targets(), Metrics);

        Assert.Equal((nint)9, Assert.IsType<RestorePreviousPlacementCommand>(command).WindowHandle);
    }

    private static IReadOnlyList<PartMonitorTarget> Targets()
    {
        var monitor = new LiveMonitor(
            new MonitorIdentity("DISPLAY-A", "DISPLAY1", "Haupt"),
            new MonitorWorkArea(0, 0, 1920, 1040),
            96,
            96,
            true,
            Bounds: new PixelRect(0, 0, 1920, 1080));
        return
        [
            new PartMonitorTarget(monitor,
            [
                new ZoneDefinition(LeftId, "Links", new NormalizedRect(0, 0, 0.5, 1)),
                new ZoneDefinition(RightId, "Rechts", new NormalizedRect(0.5, 0, 0.5, 1))
            ])
        ];
    }
}
