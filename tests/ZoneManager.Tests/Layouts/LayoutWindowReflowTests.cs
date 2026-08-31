using ZoneManager.Core.Geometry;
using ZoneManager.Core.Layouts;
using ZoneManager.Core.Models;
using Xunit;

namespace ZoneManager.Tests.Layouts;

public sealed class LayoutWindowReflowTests
{
    [Fact]
    public void Plan_moves_a_window_fully_contained_in_a_changed_zone_to_that_zone_new_bounds()
    {
        var zoneId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var monitor = new MonitorIdentity("DISPLAY-A", "\\\\.\\DISPLAY1", "Monitor A");
        var oldLayout = Layout(monitor, new ZoneDefinition(zoneId, "Links", new NormalizedRect(0, 0, 0.5, 1)));
        var newLayout = Layout(monitor, new ZoneDefinition(zoneId, "Links", new NormalizedRect(0, 0, 0.6, 1)));

        var targets = LayoutWindowReflow.Plan(
            oldLayout,
            newLayout,
            new MonitorWorkArea(0, 0, 1000, 800),
            [new WindowPlacement((nint)42, new PixelRect(0, 0, 500, 800))]);

        var target = Assert.Single(targets);
        Assert.Equal((nint)42, target.WindowHandle);
        Assert.Equal(new PixelRect(0, 0, 600, 800), target.Bounds);
    }

    [Fact]
    public void Plan_ignores_a_window_that_is_not_fully_contained_in_an_old_zone()
    {
        var zoneId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var monitor = new MonitorIdentity("DISPLAY-A", "\\\\.\\DISPLAY1", "Monitor A");
        var oldLayout = Layout(monitor, new ZoneDefinition(zoneId, "Links", new NormalizedRect(0, 0, 0.5, 1)));
        var newLayout = Layout(monitor, new ZoneDefinition(zoneId, "Links", new NormalizedRect(0, 0, 0.6, 1)));

        var targets = LayoutWindowReflow.Plan(
            oldLayout,
            newLayout,
            new MonitorWorkArea(0, 0, 1000, 800),
            [new WindowPlacement((nint)42, new PixelRect(0, 0, 501, 800))]);

        Assert.Empty(targets);
    }

    private static MonitorLayout Layout(MonitorIdentity monitor, ZoneDefinition zone) =>
        new(monitor, 1000, 800, [zone]) { Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa") };
}
