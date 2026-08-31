using ZoneManager.Core.Geometry;
using ZoneManager.Core.Models;
using ZoneManager.Core.Monitors;
using ZoneManager.Core.PartMonitors;
using Xunit;

namespace ZoneManager.Tests.PartMonitors;

public sealed class PartMonitorCommandServiceTests
{
    private static readonly Guid LeftId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid RightId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Fill_applies_exact_target_and_remembers_previous_placement_only_after_success()
    {
        var gateway = new FakeGateway();
        var history = new PlacementHistory();
        var service = CreateService(gateway, history);

        var result = service.Execute(new FillPartMonitorCommand((nint)42, "DISPLAY-A", RightId));

        Assert.Equal(PartMonitorCommandStatus.Successful, result.Status);
        Assert.Equal(new PixelRect(960, 0, 960, 1040), gateway.AppliedBounds);
        Assert.True(history.TryPeek(gateway.Current.Identity, out _));
    }

    [Fact]
    public void Fill_does_not_record_history_when_windows_rejects_placement()
    {
        var gateway = new FakeGateway { AcceptApply = false };
        var history = new PlacementHistory();
        var service = CreateService(gateway, history);

        var result = service.Execute(new FillPartMonitorCommand((nint)42, "DISPLAY-A", RightId));

        Assert.Equal(PartMonitorCommandStatus.WindowsRejected, result.Status);
        Assert.False(history.TryPeek(gateway.Current.Identity, out _));
    }

    [Fact]
    public void Restore_discards_history_only_after_windows_accepts_restore()
    {
        var gateway = new FakeGateway();
        var history = new PlacementHistory();
        history.Remember(gateway.Current);
        var service = CreateService(gateway, history);

        gateway.AcceptRestore = false;
        Assert.Equal(
            PartMonitorCommandStatus.WindowsRejected,
            service.Execute(new RestorePreviousPlacementCommand((nint)42)).Status);
        Assert.True(history.TryPeek(gateway.Current.Identity, out _));

        gateway.AcceptRestore = true;
        Assert.Equal(
            PartMonitorCommandStatus.Successful,
            service.Execute(new RestorePreviousPlacementCommand((nint)42)).Status);
        Assert.False(history.TryPeek(gateway.Current.Identity, out _));
    }

    [Fact]
    public void Cycle_wraps_and_unknown_target_does_not_capture_window()
    {
        var gateway = new FakeGateway();
        var service = CreateService(gateway, new PlacementHistory());

        var cycle = service.Execute(new CyclePartMonitorCommand(
            (nint)42,
            "DISPLAY-A",
            RightId,
            1));
        var missing = service.Execute(new FillPartMonitorCommand(
            (nint)42,
            "MISSING",
            RightId));

        Assert.Equal(PartMonitorCommandStatus.Successful, cycle.Status);
        Assert.Equal(LeftId, cycle.Placement?.PartMonitorId);
        Assert.Equal(PartMonitorCommandStatus.TargetMissing, missing.Status);
        Assert.Equal(1, gateway.CaptureCount);
    }

    private static PartMonitorCommandService CreateService(
        IPartMonitorWindowGateway gateway,
        PlacementHistory history)
    {
        var monitor = new LiveMonitor(
            new MonitorIdentity("DISPLAY-A", "DISPLAY1", "Haupt"),
            new MonitorWorkArea(0, 0, 1920, 1040),
            96,
            96,
            true);
        var resolver = new PartMonitorResolver(
        [
            new PartMonitorTarget(monitor,
            [
                new ZoneDefinition(LeftId, "Links", new NormalizedRect(0, 0, 0.5, 1)),
                new ZoneDefinition(RightId, "Rechts", new NormalizedRect(0.5, 0, 0.5, 1))
            ])
        ],
        new LayoutMetrics(0, 0));
        return new PartMonitorCommandService(resolver, history, gateway);
    }

    private sealed class FakeGateway : IPartMonitorWindowGateway
    {
        public WindowPlacementSnapshot Current { get; } = new(
            new WindowIdentity((nint)42, 100, "TestWindow"),
            0,
            1,
            new PointInt(-1, -1),
            new PointInt(-1, -1),
            new PixelRect(20, 20, 800, 600));

        public bool AcceptApply { get; set; } = true;
        public bool AcceptRestore { get; set; } = true;
        public int CaptureCount { get; private set; }
        public PixelRect? AppliedBounds { get; private set; }

        public WindowPlacementSnapshot? Capture(nint windowHandle)
        {
            CaptureCount++;
            return windowHandle == Current.Identity.Handle ? Current : null;
        }

        public bool TryApplyNormal(WindowIdentity identity, PixelRect bounds)
        {
            AppliedBounds = bounds;
            return AcceptApply;
        }

        public bool TryRestore(WindowPlacementSnapshot snapshot) => AcceptRestore;
    }
}
