using SnapZones.Core.Geometry;
using Xunit;

namespace SnapZones.Tests.Geometry;

/// <summary>
/// Der Zeiger wird nur uebergeben, nie nachgebaut: Eintritt ueber die Zone, Austritt ueber einen Rand
/// des virtuellen Monitors, Rueckweisung ueber die Desktopkante, und keine Freigabe an einem Rand,
/// hinter dem in der Zone nichts liegt.
/// </summary>
public sealed class CursorHandoffPlannerTests
{
    private static readonly PixelRect Dell = new(0, 0, 6144, 2560);
    private static readonly PixelRect Zone = new(8, 8, 2288, 1296);
    private static readonly PixelRect Virtual = new(6144, 0, 2288, 1296);
    private static readonly MirrorPlan Plan = MirrorGeometry.Plan(Zone.Width, Zone.Height, Virtual.Width, Virtual.Height);

    private static HandoffDecision Decide(bool inVirtual, PointInt point, bool locked = false, PixelRect? zone = null) =>
        CursorHandoffPlanner.Decide(inVirtual, locked, zone ?? Zone, Virtual, Plan, [Dell], point);

    [Fact]
    public void Entering_the_zone_jumps_to_the_matching_point_of_the_virtual_monitor()
    {
        Assert.Equal(new HandoffDecision(HandoffAction.Enter, new PointInt(6161, 592)), Decide(false, new PointInt(25, 600)));
        Assert.Equal(HandoffDecision.None, Decide(false, new PointInt(3000, 600)));
        Assert.Equal(HandoffDecision.None, Decide(false, new PointInt(25, 600), locked: true));
    }

    [Fact]
    public void Leaving_a_side_of_the_virtual_monitor_lands_just_outside_the_zone_on_that_side()
    {
        Assert.Equal(new HandoffDecision(HandoffAction.Exit, new PointInt(6, 256)), Decide(true, new PointInt(6076, 248)));
        Assert.Equal(new HandoffDecision(HandoffAction.Exit, new PointInt(2297, 100 + 8)), Decide(true, new PointInt(8500, 100)));
        Assert.Equal(new HandoffDecision(HandoffAction.Exit, new PointInt(1000, 6)), Decide(true, new PointInt(1000 + 6144 - 8, -40)));
        Assert.Equal(new HandoffDecision(HandoffAction.Exit, new PointInt(2000, 1305)), Decide(true, new PointInt(2000 + 6144 - 8, 1400)));
        Assert.Equal(HandoffDecision.None, Decide(true, new PointInt(7000, 500)));
    }

    [Fact]
    public void A_side_with_no_monitor_behind_it_keeps_the_pointer_on_the_virtual_monitor()
    {
        var flushLeft = new PixelRect(0, 0, 2288, 1296);
        var plan = MirrorGeometry.Plan(flushLeft.Width, flushLeft.Height, Virtual.Width, Virtual.Height);

        var decision = CursorHandoffPlanner.Decide(true, false, flushLeft, Virtual, plan, [Dell], new PointInt(6100, 300));

        Assert.Equal(HandoffAction.PushBack, decision.Action);
        Assert.Equal(new PointInt(6144, 300), decision.Target);
    }

    [Fact]
    public void Arriving_over_the_desktop_edge_is_pushed_back_to_the_nearest_real_monitor()
    {
        var decision = Decide(false, new PointInt(6402, 700));

        Assert.Equal(HandoffAction.PushBack, decision.Action);
        Assert.Equal(new PointInt(6143, 700), decision.Target);
    }

    [Fact]
    public void Degenerate_rectangles_never_move_the_pointer()
    {
        Assert.Equal(HandoffDecision.None, CursorHandoffPlanner.Decide(false, false, Zone, default, Plan, [Dell], new PointInt(25, 600)));
        Assert.Equal(HandoffDecision.None, CursorHandoffPlanner.Decide(true, false, default, Virtual, Plan, [Dell], new PointInt(25, 600)));
    }
}
