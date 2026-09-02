using SnapZones.Core.Geometry;
using SnapZones.Core.Layouts;
using SnapZones.Core.Models;
using SnapZones.Core.Monitors;
using SnapZones.Core.PartMonitors;
using SnapZones.Core.Placement;
using SnapZones.Tests.Support;
using WindowIdentity = SnapZones.Core.Placement.WindowIdentity;
using Xunit;

namespace SnapZones.Tests.Placement;

/// <summary>
/// Seit dem 02.09.2026 wird nach jedem Setzen nachgemessen, kehren gemerkte Fenster in ihre Zone
/// zurueck statt an alte Pixel, und der Layoutwechsel erkennt Fenster mit unsichtbarem Rand.
/// </summary>
public sealed class VerifiedPlacementTests
{
    private static readonly WindowIdentity Identity = new("C:\\Apps\\excel.exe", "XLMAIN", WindowKind.MainWindow);

    [Fact]
    public void A_remembered_zone_wins_over_remembered_pixels_while_the_zone_exists()
    {
        var zoneId = Guid.NewGuid();
        var zone = new PlacementZoneTarget(Guid.NewGuid(), zoneId, "DISPLAY-A", new PixelRect(960, 0, 960, 1080));
        var entry = new WindowPlacementEntry(
            Identity, "DISPLAY-A", zoneId, new MonitorWorkArea(0, 0, 1920, 1080),
            new PixelRect(10, 10, 500, 500),
            PlacementGeometry.Normalize(new PixelRect(10, 10, 500, 500), new MonitorWorkArea(0, 0, 1920, 1080)),
            false, DateTimeOffset.UtcNow);
        var monitors = new[] { new PlacementMonitorTarget("DISPLAY-A", new MonitorWorkArea(0, 0, 1920, 1080), true) };

        Assert.Equal(zone.Bounds, PlacementGeometry.Resolve(entry, monitors, [zone]));
        Assert.Equal(new PixelRect(10, 10, 500, 500), PlacementGeometry.Resolve(entry, monitors, []));
    }

    [Fact]
    public void A_small_remembered_window_stays_small()
    {
        var entry = new WindowPlacementEntry(
            Identity, "DISPLAY-A", null, new MonitorWorkArea(0, 0, 1920, 1080),
            new PixelRect(100, 100, 90, 70),
            PlacementGeometry.Normalize(new PixelRect(100, 100, 90, 70), new MonitorWorkArea(0, 0, 1920, 1080)),
            false, DateTimeOffset.UtcNow);

        var actual = PlacementGeometry.Resolve(
            entry,
            [new PlacementMonitorTarget("DISPLAY-A", new MonitorWorkArea(0, 0, 1920, 1080), true)],
            []);

        Assert.Equal(new PixelRect(100, 100, 90, 70), actual);
    }

    [Fact]
    public void Reflow_recognises_a_window_that_sits_on_a_zone_with_its_invisible_border()
    {
        var configuration = ConfigurationSamples.TwoLayouts();
        var oldLayout = configuration.Layouts[0];
        var newLayout = oldLayout with
        {
            Zones =
            [
                oldLayout.Zones[0] with { Bounds = new NormalizedRect(0, 0, 0.4, 1) },
                oldLayout.Zones[1] with { Bounds = new NormalizedRect(0.4, 0, 0.6, 1) }
            ]
        };
        var workArea = new MonitorWorkArea(0, 0, 2000, 1000);
        var metrics = new LayoutMetrics(0, 0);
        // Links: Zone 0..1000, Fenster mit 7 px unsichtbarem Rand links, rechts und unten.
        var windows = new[] { new WindowPlacement((nint)7, new PixelRect(-7, 0, 1014, 1007)) };

        var planned = LayoutWindowReflow.Plan(oldLayout, newLayout, workArea, metrics, windows);

        var target = Assert.Single(planned);
        Assert.Equal(new PixelRect(0, 0, 800, 1000), target.Bounds);
    }

    [Fact]
    public void Command_service_passes_the_measured_rejection_through()
    {
        var monitor = new LiveMonitor(
            new MonitorIdentity("DISPLAY-A", "DISPLAY1", "Haupt"),
            new MonitorWorkArea(0, 0, 1920, 1040),
            96,
            96,
            true);
        var zoneId = Guid.NewGuid();
        var resolver = new PartMonitorResolver(
            [new PartMonitorTarget(monitor, [new ZoneDefinition(zoneId, "Links", new NormalizedRect(0, 0, 0.5, 1))])],
            new LayoutMetrics(0, 0));
        var gateway = new MeasuringGateway(PlacementOutcome.Rejected("Mindestgrösse", new PixelRect(0, 0, 1200, 1040)));
        var service = new PartMonitorCommandService(resolver, new PlacementHistory(), gateway);

        var result = service.Execute(new FillPartMonitorCommand((nint)42, "DISPLAY-A", zoneId));

        Assert.Equal(PartMonitorCommandStatus.WindowsRejected, result.Status);
        Assert.Equal("Mindestgrösse", result.Reason);
        Assert.True(result.Outcome?.WindowMoved);
    }

    private sealed class MeasuringGateway(PlacementOutcome outcome) : IPartMonitorWindowGateway
    {
        public WindowPlacementSnapshot? Capture(nint windowHandle) => new(
            new SnapZones.Core.PartMonitors.WindowIdentity(windowHandle, 100, "TestWindow"),
            0,
            1,
            new PointInt(-1, -1),
            new PointInt(-1, -1),
            new PixelRect(20, 20, 800, 600));

        public bool TryApplyNormal(SnapZones.Core.PartMonitors.WindowIdentity identity, PixelRect bounds) => outcome.Succeeded;

        public PlacementOutcome ApplyNormal(SnapZones.Core.PartMonitors.WindowIdentity identity, PixelRect bounds) => outcome;

        public bool TryRestore(WindowPlacementSnapshot snapshot) => true;
    }
}
