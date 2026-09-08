using SnapZones.App.Services;
using SnapZones.Core.AppRules;
using SnapZones.Core.Geometry;
using SnapZones.Core.Monitors;
using SnapZones.Tests.Support;
using SnapZones.Windows.Windows;
using Xunit;

namespace SnapZones.Tests.Services;

/// <summary>
/// Eine Regel, die ein Fenster in eine Zone legt, meldet Fenster, Monitor und Zone weiter, damit die
/// Vollbildzone das Fenster uebernehmen kann — aber nur nach gelungener Platzierung.
/// </summary>
public sealed class VirtualZoneRuleTests
{
    [Fact]
    public async Task A_successful_rule_reports_window_monitor_and_zone()
    {
        var configuration = ConfigurationSamples.TwoLayouts();
        var layout = configuration.Layouts[0];
        configuration = configuration with
        {
            AppRules =
            [
                new AppRule(Guid.NewGuid(), "testhost.exe", "*Film*", "TestWindow", AppRuleEvent.WindowCreated, 0, 0, 50, true, layout.Id, layout.Zones[0].Id)
            ]
        };
        var candidate = new WindowRuleCandidate((nint)42, new AppWindowIdentity(123, @"C:\Tools\testhost.exe", "Film - Test", "TestWindow"));
        var gateway = new FakeGateway(candidate, succeed: true);
        var coordinator = CreateCoordinator(configuration, gateway);
        (nint Window, string Monitor, Guid Zone)? placed = null;
        coordinator.WindowPlaced = (window, monitor, zone) => placed = (window, monitor, zone);

        var result = await coordinator.HandleAsync(AppRuleEvent.WindowCreated, candidate.WindowHandle);

        Assert.Equal(AppRuleExecutionStatus.Applied, result.Status);
        Assert.NotNull(placed);
        Assert.Equal(((nint)42, layout.Monitor.StableId, layout.Zones[0].Id), placed.Value);
    }

    [Fact]
    public async Task A_rejected_placement_reports_nothing()
    {
        var configuration = ConfigurationSamples.TwoLayouts();
        var layout = configuration.Layouts[0];
        configuration = configuration with
        {
            AppRules =
            [
                new AppRule(Guid.NewGuid(), "testhost.exe", "*Film*", "TestWindow", AppRuleEvent.WindowCreated, 0, 0, 50, true, layout.Id, layout.Zones[0].Id)
            ]
        };
        var candidate = new WindowRuleCandidate((nint)42, new AppWindowIdentity(123, @"C:\Tools\testhost.exe", "Film - Test", "TestWindow"));
        var coordinator = CreateCoordinator(configuration, new FakeGateway(candidate, succeed: false));
        var calls = 0;
        coordinator.WindowPlaced = (_, _, _) => calls++;

        var result = await coordinator.HandleAsync(AppRuleEvent.WindowCreated, candidate.WindowHandle);

        Assert.Equal(AppRuleExecutionStatus.WindowsRejected, result.Status);
        Assert.Equal(0, calls);
    }

    private static AppRuleCoordinator CreateCoordinator(SnapZones.Core.Models.SnapConfiguration configuration, FakeGateway gateway)
    {
        var monitors = new[]
        {
            new LiveMonitor(configuration.Layouts[0].Monitor, new MonitorWorkArea(0, 0, 3440, 1440), 96, 96, true)
        };
        return new AppRuleCoordinator(() => configuration, monitors, gateway, (_, _) => Task.CompletedTask);
    }

    private sealed class FakeGateway(WindowRuleCandidate candidate, bool succeed) : IAppRuleWindowGateway
    {
        public WindowRuleCandidate? Inspect(nint windowHandle) => candidate;

        public IReadOnlyList<WindowRuleCandidate> GetCandidates() => [candidate];

        public bool TrySnap(nint windowHandle, PixelRect bounds) => succeed;
    }
}
