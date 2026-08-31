using SnapZones.Core.Drag;
using SnapZones.Core.Geometry;
using SnapZones.Core.Models;
using SnapZones.Core.Monitors;
using SnapZones.Core.PartMonitors;
using Xunit;

namespace SnapZones.Tests.Drag;

/// <summary>
/// Wird ein Fenster mit gedrückter Strg-Taste über mehrere Zonen gezogen, sammeln sich die
/// überstrichenen Zonen auf und das Fenster belegt beim Loslassen deren gemeinsame Fläche.
/// </summary>
public sealed class ZoneSpanningTests
{
    private static readonly Guid Left = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Middle = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Right = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void Union_of_two_rectangles_is_the_smallest_enclosing_rectangle()
    {
        var left = new PixelRect(0, 0, 100, 50);
        var right = new PixelRect(200, 20, 100, 80);

        Assert.Equal(new PixelRect(0, 0, 300, 100), left.Union(right));
        Assert.Equal(new PixelRect(0, 0, 300, 100), right.Union(left));
    }

    [Fact]
    public void Union_with_an_empty_rectangle_keeps_the_other()
    {
        var rectangle = new PixelRect(10, 10, 40, 40);

        Assert.Equal(rectangle, rectangle.Union(default));
        Assert.Equal(rectangle, default(PixelRect).Union(rectangle));
    }

    [Fact]
    public void Spanning_two_zones_fills_their_combined_area()
    {
        var coordinator = CreateCoordinator();
        var actions = Track(coordinator);
        coordinator.Start((nint)42, EligibleWindow(), new PointInt(100, 100));
        actions.Clear();

        coordinator.Update(new PointInt(100, 100), spanRequested: true);
        coordinator.Update(new PointInt(900, 100), spanRequested: true);
        coordinator.End();

        var fill = Assert.IsType<FillPartMonitorSpanAction>(actions[^1]);
        Assert.Equal("A", fill.MonitorId);
        Assert.Equal([Left, Middle], fill.PartMonitorIds);
        Assert.Equal(
            new PixelRect(0, 0, 1280, 1040),
            Resolver().ResolveSpan("A", fill.PartMonitorIds)!.Bounds);
    }

    [Fact]
    public void Every_newly_touched_zone_is_highlighted_together_with_the_earlier_ones()
    {
        var coordinator = CreateCoordinator();
        var actions = Track(coordinator);
        coordinator.Start((nint)42, EligibleWindow(), new PointInt(100, 100));
        actions.Clear();

        coordinator.Update(new PointInt(100, 100), spanRequested: true);
        coordinator.Update(new PointInt(900, 100), spanRequested: true);
        coordinator.Update(new PointInt(1700, 100), spanRequested: true);

        var highlights = actions.OfType<HighlightZoneSpanAction>().ToArray();
        Assert.Equal(3, highlights.Length);
        Assert.Equal([Left], highlights[0].ZoneIds);
        Assert.Equal([Left, Middle], highlights[1].ZoneIds);
        Assert.Equal([Left, Middle, Right], highlights[2].ZoneIds);
    }

    [Fact]
    public void Moving_back_over_an_already_selected_zone_changes_nothing()
    {
        var coordinator = CreateCoordinator();
        var actions = Track(coordinator);
        coordinator.Start((nint)42, EligibleWindow(), new PointInt(100, 100));
        coordinator.Update(new PointInt(100, 100), spanRequested: true);
        coordinator.Update(new PointInt(900, 100), spanRequested: true);
        actions.Clear();

        coordinator.Update(new PointInt(150, 200), spanRequested: true);

        Assert.Empty(actions);
    }

    [Fact]
    public void Releasing_the_key_falls_back_to_the_single_zone_under_the_cursor()
    {
        var coordinator = CreateCoordinator();
        var actions = Track(coordinator);
        coordinator.Start((nint)42, EligibleWindow(), new PointInt(100, 100));
        coordinator.Update(new PointInt(100, 100), spanRequested: true);
        coordinator.Update(new PointInt(900, 100), spanRequested: true);
        actions.Clear();

        coordinator.Update(new PointInt(900, 100), spanRequested: false);
        coordinator.End();

        var highlight = Assert.IsType<HighlightZoneAction>(actions[0]);
        Assert.Equal(Middle, highlight.ZoneId);
        var fill = Assert.IsType<FillPartMonitorAction>(actions[^1]);
        Assert.Equal(Middle, fill.PartMonitorId);
    }

    [Fact]
    public void A_span_of_a_single_zone_behaves_like_an_ordinary_snap()
    {
        var coordinator = CreateCoordinator();
        var actions = Track(coordinator);
        coordinator.Start((nint)42, EligibleWindow(), new PointInt(100, 100));
        actions.Clear();

        coordinator.Update(new PointInt(100, 100), spanRequested: true);
        coordinator.End();

        var fill = Assert.IsType<FillPartMonitorAction>(actions[^1]);
        Assert.Equal(Left, fill.PartMonitorId);
    }

    [Fact]
    public void A_span_never_reaches_across_two_monitors()
    {
        // Die Huellbox zweier Bildschirme wuerde den Zwischenraum und fremde Zonen mit einschliessen.
        var coordinator = CreateCoordinator();
        var actions = Track(coordinator);
        coordinator.Start((nint)42, EligibleWindow(), new PointInt(100, 100));
        actions.Clear();

        coordinator.Update(new PointInt(100, 100), spanRequested: true);
        coordinator.Update(new PointInt(2500, 100), spanRequested: true);
        coordinator.End();

        var fill = Assert.IsType<FillPartMonitorAction>(actions[^1]);
        Assert.Equal(Left, fill.PartMonitorId);
        Assert.Equal("A", fill.MonitorId);
    }

    [Fact]
    public void Cancelling_a_span_places_nothing()
    {
        var coordinator = CreateCoordinator();
        var actions = Track(coordinator);
        coordinator.Start((nint)42, EligibleWindow(), new PointInt(100, 100));
        coordinator.Update(new PointInt(100, 100), spanRequested: true);
        coordinator.Update(new PointInt(900, 100), spanRequested: true);
        actions.Clear();

        coordinator.Cancel();
        coordinator.End();

        Assert.Single(actions);
        Assert.IsType<HideOverlaysAction>(actions[0]);
    }

    [Fact]
    public void Resolving_a_span_skips_zones_that_no_longer_exist()
    {
        var resolver = Resolver();

        var placement = resolver.ResolveSpan("A", [Left, Guid.NewGuid()]);

        Assert.NotNull(placement);
        Assert.Equal(Left, placement.PartMonitorId);
        Assert.Equal(new PixelRect(0, 0, 640, 1040), placement.Bounds);
        Assert.Null(resolver.ResolveSpan("A", [Guid.NewGuid()]));
        Assert.Null(resolver.ResolveSpan("Unbekannt", [Left]));
    }

    private static List<DragAction> Track(WindowDragCoordinator coordinator)
    {
        var actions = new List<DragAction>();
        coordinator.ActionRequested += actions.Add;
        return actions;
    }

    private static IReadOnlyList<PartMonitorTarget> Targets()
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
        return
        [
            new PartMonitorTarget(first,
            [
                new ZoneDefinition(Left, "Links", new NormalizedRect(0, 0, 1d / 3d, 1)),
                new ZoneDefinition(Middle, "Mitte", new NormalizedRect(1d / 3d, 0, 1d / 3d, 1)),
                new ZoneDefinition(Right, "Rechts", new NormalizedRect(2d / 3d, 0, 1d / 3d, 1))
            ]),
            new PartMonitorTarget(second, [new ZoneDefinition(Guid.NewGuid(), "Voll", NormalizedRect.Full)])
        ];
    }

    private static PartMonitorResolver Resolver() => new(Targets(), new LayoutMetrics(0, 0));

    private static WindowDragCoordinator CreateCoordinator() =>
        new(Targets(), new LayoutMetrics(0, 0), OverlayScope.AllMonitors);

    private static WindowSnapshot EligibleWindow() => new(true, false, false, false, false, true);
}
