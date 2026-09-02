using SnapZones.App.Services;
using SnapZones.Core.AppRules;
using SnapZones.Core.Geometry;
using SnapZones.Core.Models;
using SnapZones.Core.Monitors;
using SnapZones.Tests.Support;
using SnapZones.Windows.Windows;
using Xunit;

namespace SnapZones.Tests.Services;

public sealed class AppRuleCoordinatorTests
{
    [Fact]
    public async Task Handle_waits_rechecks_the_window_and_snaps_to_the_configured_zone()
    {
        var configuration = WithRule(ConfigurationSamples.TwoLayouts(), delayMilliseconds: 350, retryCount: 0);
        var window = Candidate();
        var gateway = new FakeGateway(window, snapResults: [true]);
        var delays = new List<TimeSpan>();
        var coordinator = CreateCoordinator(configuration, gateway, delays);

        var result = await coordinator.HandleAsync(AppRuleEvent.WindowCreated, window.WindowHandle);

        Assert.Equal(AppRuleExecutionStatus.Applied, result.Status);
        Assert.Equal([TimeSpan.FromMilliseconds(350)], delays);
        Assert.Equal(2, gateway.InspectionCount);
        // Dieselbe Geometrie wie Overlay und Ziehpfad: die Aussenabstaende der Einstellungen gelten auch
        // fuer Regeln (Voreinstellung 8 px, kein Zonenabstand).
        var metrics = new LayoutMetrics(configuration.Settings.EffectiveOuterMargins, configuration.Settings.ZoneGap);
        var expected = ZoneGeometry.ToPixels(
            configuration.Layouts[0].Zones[0].Bounds,
            new MonitorWorkArea(0, 0, 3440, 1440),
            metrics);
        Assert.Equal(new PixelRect(8, 8, 1712, 1424), expected);
        Assert.Equal(expected, Assert.Single(gateway.SnappedBounds));
    }

    [Fact]
    public async Task Handle_stops_when_the_window_identity_changes_during_the_delay()
    {
        var configuration = WithRule(ConfigurationSamples.TwoLayouts(), delayMilliseconds: 100, retryCount: 3);
        var initial = Candidate();
        var changed = initial with
        {
            Identity = initial.Identity with { WindowClass = "UnexpectedDialog" }
        };
        var gateway = new FakeGateway(initial, snapResults: [true]) { CandidateAfterFirstInspection = changed };
        var coordinator = CreateCoordinator(configuration, gateway, []);

        var result = await coordinator.HandleAsync(AppRuleEvent.WindowCreated, initial.WindowHandle);

        Assert.Equal(AppRuleExecutionStatus.CandidateUnavailable, result.Status);
        Assert.Empty(gateway.SnappedBounds);
    }

    [Fact]
    public async Task Handle_performs_at_most_three_retries_after_the_initial_attempt()
    {
        var configuration = WithRule(ConfigurationSamples.TwoLayouts(), delayMilliseconds: 0, retryCount: 3);
        var window = Candidate();
        var gateway = new FakeGateway(window, snapResults: [false, false, false, true]);
        var delays = new List<TimeSpan>();
        var coordinator = CreateCoordinator(configuration, gateway, delays);

        var result = await coordinator.HandleAsync(AppRuleEvent.WindowCreated, window.WindowHandle);

        Assert.Equal(AppRuleExecutionStatus.Applied, result.Status);
        Assert.Equal(4, gateway.SnappedBounds.Count);
        Assert.Equal(3, delays.Count(delay => delay == TimeSpan.FromMilliseconds(250)));
    }

    [Fact]
    public async Task Handle_pauses_a_rule_whose_target_no_longer_exists()
    {
        var configuration = WithRule(ConfigurationSamples.TwoLayouts(), delayMilliseconds: 0, retryCount: 0);
        configuration = configuration with
        {
            AppRules = [configuration.AppRules.Single() with { TargetZoneId = Guid.NewGuid() }]
        };
        var window = Candidate();
        var gateway = new FakeGateway(window, snapResults: [true]);
        var messages = new List<string>();
        var coordinator = CreateCoordinator(configuration, gateway, [], messages.Add);

        var result = await coordinator.HandleAsync(AppRuleEvent.WindowCreated, window.WindowHandle);

        Assert.Equal(AppRuleExecutionStatus.TargetMissing, result.Status);
        Assert.Contains(messages, message => message.Contains("pausiert", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(gateway.SnappedBounds);
    }

    [Fact]
    public async Task Layout_activation_resolves_only_rules_targeting_the_activated_layout()
    {
        var configuration = WithRule(ConfigurationSamples.TwoLayouts(), delayMilliseconds: 0, retryCount: 0);
        var first = configuration.AppRules.Single() with
        {
            Event = AppRuleEvent.LayoutActivated,
            Priority = 100
        };
        var secondLayout = configuration.Layouts[1];
        var second = first with
        {
            Id = Guid.NewGuid(),
            Priority = 10,
            TargetLayoutId = secondLayout.Id,
            TargetZoneId = secondLayout.Zones[0].Id
        };
        configuration = configuration with { AppRules = [first, second] };
        var window = Candidate();
        var gateway = new FakeGateway(window, snapResults: [true]);
        var coordinator = CreateCoordinator(configuration, gateway, []);

        var results = await coordinator.HandleLayoutActivatedAsync(secondLayout.Id);

        Assert.Equal(second.Id, Assert.Single(results).RuleId);
        Assert.Single(gateway.SnappedBounds);
    }

    [Fact]
    public async Task An_excluded_window_is_never_moved_by_a_rule()
    {
        // Ausschluss schlaegt Regel: wer ein Fenster ausdruecklich frei laesst, will es auch dann frei
        // haben, wenn dafuer noch eine aeltere Regel besteht.
        var configuration = WithRule(ConfigurationSamples.TwoLayouts(), delayMilliseconds: 0, retryCount: 0);
        configuration = configuration with
        {
            AppExclusions = [new AppExclusion(Guid.NewGuid(), "testhost.exe", null, null, true)]
        };
        var window = Candidate();
        var gateway = new FakeGateway(window, snapResults: [true]);
        var coordinator = CreateCoordinator(configuration, gateway, []);

        var result = await coordinator.HandleAsync(AppRuleEvent.WindowCreated, window.WindowHandle);

        Assert.Equal(AppRuleExecutionStatus.Excluded, result.Status);
        Assert.Empty(gateway.SnappedBounds);
    }

    [Fact]
    public async Task A_disabled_exclusion_leaves_the_rule_in_charge()
    {
        var configuration = WithRule(ConfigurationSamples.TwoLayouts(), delayMilliseconds: 0, retryCount: 0);
        configuration = configuration with
        {
            AppExclusions = [new AppExclusion(Guid.NewGuid(), "testhost.exe", null, null, false)]
        };
        var window = Candidate();
        var gateway = new FakeGateway(window, snapResults: [true]);
        var coordinator = CreateCoordinator(configuration, gateway, []);

        var result = await coordinator.HandleAsync(AppRuleEvent.WindowCreated, window.WindowHandle);

        Assert.Equal(AppRuleExecutionStatus.Applied, result.Status);
        Assert.Single(gateway.SnappedBounds);
    }

    private static AppRuleCoordinator CreateCoordinator(
        SnapZones.Core.Models.SnapConfiguration configuration,
        FakeGateway gateway,
        List<TimeSpan> delays,
        Action<string>? status = null)
    {
        var monitor = configuration.Layouts[0].Monitor;
        var monitors = new[]
        {
            new LiveMonitor(monitor, new MonitorWorkArea(0, 0, 3440, 1440), 96, 96, true)
        };
        return new AppRuleCoordinator(
            () => configuration,
            monitors,
            gateway,
            (delay, _) =>
            {
                delays.Add(delay);
                return Task.CompletedTask;
            },
            status);
    }

    private static SnapZones.Core.Models.SnapConfiguration WithRule(
        SnapZones.Core.Models.SnapConfiguration configuration,
        int delayMilliseconds,
        int retryCount)
    {
        var layout = configuration.Layouts[0];
        return configuration with
        {
            AppRules =
            [
                new AppRule(
                    Guid.Parse("99999999-9999-9999-9999-999999999999"),
                    "testhost.exe",
                    "*YouTube*",
                    "TestWindow",
                    AppRuleEvent.WindowCreated,
                    delayMilliseconds,
                    retryCount,
                    50,
                    true,
                    layout.Id,
                    layout.Zones[0].Id)
            ]
        };
    }

    private static WindowRuleCandidate Candidate() => new(
        (nint)42,
        new AppWindowIdentity(
            123,
            @"C:\Tools\testhost.exe",
            "YouTube - Test",
            "TestWindow"));

    private sealed class FakeGateway(
        WindowRuleCandidate initialCandidate,
        IEnumerable<bool> snapResults) : IAppRuleWindowGateway
    {
        private readonly Queue<bool> results = new(snapResults);

        public WindowRuleCandidate? CandidateAfterFirstInspection { get; init; }
        public int InspectionCount { get; private set; }
        public List<PixelRect> SnappedBounds { get; } = [];

        public WindowRuleCandidate? Inspect(nint windowHandle)
        {
            _ = windowHandle;
            InspectionCount++;
            return InspectionCount > 1 && CandidateAfterFirstInspection is not null
                ? CandidateAfterFirstInspection
                : initialCandidate;
        }

        public IReadOnlyList<WindowRuleCandidate> GetCandidates() => [initialCandidate];

        public bool TrySnap(nint windowHandle, PixelRect bounds)
        {
            _ = windowHandle;
            SnappedBounds.Add(bounds);
            return results.Count > 0 && results.Dequeue();
        }
    }
}
