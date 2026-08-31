using SnapZones.Core.Drag;
using SnapZones.Core.Geometry;
using SnapZones.Core.Models;
using SnapZones.Core.Monitors;
using SnapZones.Core.PartMonitors;
using Xunit;

namespace SnapZones.Tests.Drag;

public sealed class WindowDragCoordinatorTests
{
    [Fact]
    public void Start_requests_overlays_for_titlebar_drag()
    {
        var coordinator = CreateCoordinator();
        var actions = new List<DragAction>();
        coordinator.ActionRequested += actions.Add;

        coordinator.Start((nint)42, EligibleWindow(), new PointInt(100, 100));

        var show = Assert.IsType<ShowOverlaysAction>(Assert.Single(actions));
        Assert.Equal(2, show.MonitorIds.Count);
    }

    [Fact]
    public void Update_then_end_requests_exact_zone_and_hides_overlay_first()
    {
        var coordinator = CreateCoordinator();
        var actions = new List<DragAction>();
        coordinator.ActionRequested += actions.Add;
        coordinator.Start((nint)42, EligibleWindow(), new PointInt(100, 100));
        actions.Clear();

        coordinator.Update(new PointInt(1500, 500));
        coordinator.End();

        Assert.IsType<HighlightZoneAction>(actions[0]);
        Assert.IsType<HideOverlaysAction>(actions[1]);
        var fill = Assert.IsType<FillPartMonitorAction>(actions[2]);
        Assert.Equal((nint)42, fill.WindowHandle);
        Assert.Equal("A", fill.MonitorId);
        Assert.Equal(Guid.Parse("22222222-2222-2222-2222-222222222222"), fill.PartMonitorId);
    }

    [Fact]
    public void End_with_final_cursor_snaps_even_before_first_timer_update()
    {
        var coordinator = CreateCoordinator();
        var actions = new List<DragAction>();
        coordinator.ActionRequested += actions.Add;
        coordinator.Start((nint)42, EligibleWindow(), new PointInt(100, 100));
        actions.Clear();

        coordinator.End(new PointInt(1500, 500));

        Assert.IsType<HighlightZoneAction>(actions[0]);
        Assert.IsType<HideOverlaysAction>(actions[1]);
        var fill = Assert.IsType<FillPartMonitorAction>(actions[2]);
        Assert.Equal((nint)42, fill.WindowHandle);
        Assert.Equal("A", fill.MonitorId);
        Assert.Equal(Guid.Parse("22222222-2222-2222-2222-222222222222"), fill.PartMonitorId);
    }

    [Fact]
    public void Cancel_then_end_never_requests_snap()
    {
        var coordinator = CreateCoordinator();
        var actions = new List<DragAction>();
        coordinator.ActionRequested += actions.Add;
        coordinator.Start((nint)42, EligibleWindow(), new PointInt(1500, 500));
        coordinator.Update(new PointInt(1500, 500));
        actions.Clear();

        coordinator.Cancel();
        coordinator.End();

        Assert.Single(actions);
        Assert.IsType<HideOverlaysAction>(actions[0]);
        Assert.DoesNotContain(actions, action => action is FillPartMonitorAction);
    }

    private static WindowDragCoordinator CreateCoordinator()
    {
        var first = new LiveMonitor(
            new MonitorIdentity("A", "DISPLAY1", "Links"),
            new MonitorWorkArea(0, 0, 1920, 1040),
            96,
            96,
            true);
        var second = new LiveMonitor(
            new MonitorIdentity("B", "DISPLAY2", "Rechts"),
            new MonitorWorkArea(1920, 0, 1920, 1040),
            96,
            96,
            false);
        var zones = new[]
        {
            new ZoneDefinition(Guid.Parse("11111111-1111-1111-1111-111111111111"), "Links", new NormalizedRect(0, 0, 0.5, 1)),
            new ZoneDefinition(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Rechts", new NormalizedRect(0.5, 0, 0.5, 1))
        };
        var targets = new[]
        {
            new PartMonitorTarget(first, zones),
            new PartMonitorTarget(second, [new ZoneDefinition(Guid.NewGuid(), "Voll", NormalizedRect.Full)])
        };
        return new WindowDragCoordinator(targets, new LayoutMetrics(0, 0), OverlayScope.AllMonitors);
    }

    private static WindowSnapshot EligibleWindow() => new(true, false, false, false, false, true);
}
