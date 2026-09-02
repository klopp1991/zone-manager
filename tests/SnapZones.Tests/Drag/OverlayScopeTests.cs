using SnapZones.Core.Drag;
using SnapZones.Core.Geometry;
using SnapZones.Core.Models;
using SnapZones.Core.Monitors;
using SnapZones.Core.PartMonitors;
using Xunit;

namespace SnapZones.Tests.Drag;

/// <summary>
/// Wo die Zonen beim Ziehen erscheinen. Drei Fälle: überall, auf dem Bildschirm des Ziehbeginns, oder
/// auf dem Bildschirm unter dem Mauszeiger — dann wandern sie beim Monitorwechsel mit.
/// </summary>
public sealed class OverlayScopeTests
{
    private const string Left = "A";
    private const string Right = "B";
    private static readonly PointInt OnLeft = new(100, 100);
    private static readonly PointInt OnRight = new(2500, 100);

    [Fact]
    public void All_monitors_shows_every_screen_at_once()
    {
        var (coordinator, actions) = Start(OverlayScope.AllMonitors, OnLeft);

        var show = Assert.IsType<ShowOverlaysAction>(Assert.Single(actions));
        Assert.Equal([Left, Right], show.MonitorIds);
    }

    [Fact]
    public void The_starting_monitor_keeps_the_zones_even_after_the_cursor_leaves()
    {
        // Das bisherige Verhalten, unveraendert: die Zonen bleiben, wo das Ziehen begann.
        var (coordinator, actions) = Start(OverlayScope.ActiveMonitor, OnLeft);
        Assert.Equal([Left], Assert.IsType<ShowOverlaysAction>(actions[0]).MonitorIds);
        actions.Clear();

        coordinator.Update(OnRight);

        Assert.DoesNotContain(actions, action => action is ShowOverlaysAction);
    }

    [Fact]
    public void The_cursor_monitor_moves_the_zones_across_the_screen_border()
    {
        var (coordinator, actions) = Start(OverlayScope.CursorMonitor, OnLeft);
        Assert.Equal([Left], Assert.IsType<ShowOverlaysAction>(actions[0]).MonitorIds);
        actions.Clear();

        coordinator.Update(OnRight);

        // Genau ein Monitor wird genannt; alle uebrigen blenden dadurch aus.
        var show = Assert.IsType<ShowOverlaysAction>(actions.First(action => action is ShowOverlaysAction));
        Assert.Equal([Right], show.MonitorIds);
    }

    [Fact]
    public void Staying_on_the_same_monitor_does_not_redraw_the_overlay()
    {
        // Bei 33 Millisekunden Taktung waere ein Neuaufbau je Tick ein sichtbares Flackern.
        var (coordinator, actions) = Start(OverlayScope.CursorMonitor, OnLeft);
        actions.Clear();

        coordinator.Update(new PointInt(200, 300));
        coordinator.Update(new PointInt(900, 500));

        Assert.DoesNotContain(actions, action => action is ShowOverlaysAction);
    }

    [Fact]
    public void A_cursor_between_the_screens_leaves_the_current_overlay_standing()
    {
        // Ausblenden und sofort wieder einblenden ergaebe nur ein Flackern.
        var (coordinator, actions) = Start(OverlayScope.CursorMonitor, OnLeft);
        actions.Clear();

        coordinator.Update(new PointInt(500, 5000));

        Assert.DoesNotContain(actions, action => action is ShowOverlaysAction);
    }

    [Fact]
    public void Moving_back_and_forth_follows_the_cursor_each_time()
    {
        var (coordinator, actions) = Start(OverlayScope.CursorMonitor, OnLeft);
        actions.Clear();

        coordinator.Update(OnRight);
        coordinator.Update(OnLeft);
        coordinator.Update(OnRight);

        var shown = actions.OfType<ShowOverlaysAction>()
            .Select(action => Assert.Single(action.MonitorIds))
            .ToArray();
        Assert.Equal([Right, Left, Right], shown);
    }

    [Fact]
    public void A_new_drag_starts_over_on_the_monitor_it_began_on()
    {
        // Ohne Zuruecksetzen wuerde der erste Zug des naechsten Ziehens uebersprungen, weil der
        // zuletzt gezeigte Monitor noch gemerkt waere.
        var (coordinator, actions) = Start(OverlayScope.CursorMonitor, OnLeft);
        coordinator.Update(OnRight);
        coordinator.End();
        actions.Clear();

        coordinator.Start((nint)42, EligibleWindow(), OnLeft);

        Assert.Equal([Left], Assert.IsType<ShowOverlaysAction>(actions[0]).MonitorIds);
    }

    private static (WindowDragCoordinator Coordinator, List<DragAction> Actions) Start(
        OverlayScope scope,
        PointInt cursor)
    {
        var first = new LiveMonitor(
            new MonitorIdentity(Left, "DISPLAY1", "Links"),
            new MonitorWorkArea(0, 0, 1920, 1040),
            96,
            96,
            true);
        var second = new LiveMonitor(
            new MonitorIdentity(Right, "DISPLAY2", "Rechts"),
            new MonitorWorkArea(1920, 0, 1920, 1040),
            96,
            96,
            false);
        var targets = new[]
        {
            new PartMonitorTarget(first, [new ZoneDefinition(Guid.NewGuid(), "Voll", NormalizedRect.Full)]),
            new PartMonitorTarget(second, [new ZoneDefinition(Guid.NewGuid(), "Voll", NormalizedRect.Full)])
        };

        var coordinator = new WindowDragCoordinator(targets, new LayoutMetrics(0, 0), scope);
        var actions = new List<DragAction>();
        coordinator.ActionRequested += actions.Add;
        coordinator.Start((nint)42, EligibleWindow(), cursor);
        return (coordinator, actions);
    }

    private static WindowSnapshot EligibleWindow() => new(true, false, false, false, false, true);
}
