using SnapZones.App.Services;
using SnapZones.Core.AppRules;
using SnapZones.Core.Geometry;
using SnapZones.Core.PartMonitors;
using SnapZones.Core.Placement;
using SnapZones.Tests.Support;
using SnapZones.Windows.Hooks;
using SnapZones.Windows.Windows;
using Xunit;

namespace SnapZones.Tests.Fullscreen;

public sealed class ZoneFullscreenCoordinatorTests
{
    private const nint Window = 0x4321;
    private static readonly PixelRect Monitor = new(0, 0, 1920, 1080);
    private static readonly PixelRect LeftZone = new(8, 8, 952, 1064);
    private static readonly PixelRect Fullscreen = new(0, 0, 1920, 1080);

    [Fact]
    public void A_correction_that_did_not_take_is_retried_on_the_next_fallback()
    {
        // Das Nachmessen meldet die erste Platzierung als gescheitert, weil das Fenster ein paar Pixel
        // breiter blieb. Die gemerkte Zone muss das ueberleben: beim naechsten Rueckfall auf den ganzen
        // Monitor wird das Fenster wieder in die Zone geholt statt fuer immer vergessen.
        var harness = new Harness();
        harness.FillOutcomes.Enqueue(PlacementOutcome.Rejected("Das Fenster hält eine Mindestgrösse ein."));
        harness.FillOutcomes.Enqueue(PlacementOutcome.Success(LeftZone));

        harness.Report(LeftZone, borderless: false, WindowLifecycleEventKind.LocationChanged);
        harness.Report(Fullscreen, borderless: true, WindowLifecycleEventKind.LocationChanged);
        harness.Report(Fullscreen, borderless: true, WindowLifecycleEventKind.Focused);

        Assert.Equal(2, harness.Fills.Count);
        Assert.All(harness.Fills, bounds => Assert.Equal(LeftZone, bounds));
    }

    [Fact]
    public void A_focus_change_that_widens_the_held_window_pulls_it_back()
    {
        var harness = new Harness();

        harness.Report(LeftZone, borderless: false, WindowLifecycleEventKind.LocationChanged);
        harness.Report(Fullscreen, borderless: true, WindowLifecycleEventKind.LocationChanged);
        harness.Report(LeftZone, borderless: true, WindowLifecycleEventKind.LocationChanged);

        // Ein anderes Fenster bekommt den Fokus; Chromium stellt im Hintergrund die Breite wieder her.
        var widened = new PixelRect(LeftZone.X, LeftZone.Y, Monitor.Width, LeftZone.Height);
        harness.Report(widened, borderless: true, WindowLifecycleEventKind.LocationChanged);

        Assert.Equal(2, harness.Fills.Count);
        Assert.Equal(LeftZone, harness.Fills[1]);
    }

    [Fact]
    public void Polling_catches_a_fallback_that_produced_no_event()
    {
        // Chromium setzt das Fenster beim Aktivierungswechsel auf den Monitor zurueck, ohne dass ein
        // Lageereignis ankommt. Die Nachschau per Zeitgeber muss den Rueckfall trotzdem sehen.
        var harness = new Harness();

        harness.Report(LeftZone, borderless: false, WindowLifecycleEventKind.LocationChanged);
        harness.Report(Fullscreen, borderless: true, WindowLifecycleEventKind.LocationChanged);
        harness.Report(LeftZone, borderless: true, WindowLifecycleEventKind.LocationChanged);
        Assert.True(harness.HasHeldWindows);

        harness.SetState(Fullscreen, borderless: true);
        harness.Poll();

        Assert.Equal(2, harness.Fills.Count);
        Assert.Equal(LeftZone, harness.Fills[1]);
    }

    [Fact]
    public void Polling_a_window_that_sits_in_its_zone_does_nothing()
    {
        var harness = new Harness();

        harness.Report(LeftZone, borderless: false, WindowLifecycleEventKind.LocationChanged);
        harness.Report(Fullscreen, borderless: true, WindowLifecycleEventKind.LocationChanged);
        harness.SetState(LeftZone, borderless: true);

        harness.Poll();
        harness.Poll();

        Assert.Single(harness.Fills);
    }

    [Fact]
    public void An_exhausted_budget_is_logged_once_not_on_every_poll()
    {
        var harness = new Harness();
        harness.Report(LeftZone, borderless: false, WindowLifecycleEventKind.LocationChanged);
        var limit = harness.MaximumCorrections;
        for (var attempt = 0; attempt < limit; attempt++)
        {
            harness.Report(Fullscreen, borderless: true, WindowLifecycleEventKind.LocationChanged);
        }

        harness.Poll();
        harness.Poll();
        harness.Poll();

        Assert.Equal(limit, harness.Fills.Count);
        Assert.Single(harness.Log, line => line.Contains("in Folge", StringComparison.Ordinal));
    }

    private sealed class Harness
    {
        private readonly FakeReader reader = new();
        private readonly ZoneFullscreenCoordinator coordinator;

        public Harness()
        {
            var configuration = ConfigurationSamples.TwoLayouts();
            configuration = configuration with
            {
                Settings = configuration.Settings with { ZoneFullscreen = true }
            };
            MaximumCorrections = configuration.Settings.ZoneFullscreenMaxCorrections;
            var zones = new[]
            {
                new PlacementZoneTarget(configuration.Layouts[0].Id, Guid.NewGuid(), "DISPLAY-A", LeftZone),
                new PlacementZoneTarget(configuration.Layouts[0].Id, Guid.NewGuid(), "DISPLAY-A", new PixelRect(960, 8, 952, 1064))
            };
            var monitors = new[]
            {
                new PlacementMonitorTarget("DISPLAY-A", new MonitorWorkArea(0, 0, 1920, 1080), IsPrimary: true, Monitor)
            };
            coordinator = new ZoneFullscreenCoordinator(
                reader,
                (_, bounds) =>
                {
                    Fills.Add(bounds);
                    return FillOutcomes.Count > 0 ? FillOutcomes.Dequeue() : PlacementOutcome.Success(bounds);
                },
                _ => new AppWindowIdentity(100, @"C:\browser.exe", "Twitch", "Chrome_WidgetWin_1"),
                () => new PlacementEnvironment(configuration, monitors, zones),
                Log.Add);
        }

        public int MaximumCorrections { get; private set; }

        public bool HasHeldWindows => coordinator.HasHeldWindows;

        public void Poll() => coordinator.Poll();

        public void SetState(PixelRect bounds, bool borderless) =>
            reader.State = new FullscreenWindowState(bounds, Monitor, IsMaximized: false, IsMinimized: false, borderless);

        public List<PixelRect> Fills { get; } = [];
        public Queue<PlacementOutcome> FillOutcomes { get; } = new();
        public List<string> Log { get; } = [];

        public void Report(PixelRect bounds, bool borderless, WindowLifecycleEventKind kind)
        {
            reader.State = new FullscreenWindowState(bounds, Monitor, IsMaximized: false, IsMinimized: false, borderless);
            coordinator.Handle(new WindowLifecycleEvent(Window, kind));
        }

        private sealed class FakeReader : IFullscreenWindowReader
        {
            public FullscreenWindowState? State { get; set; }

            public FullscreenWindowState? Read(nint window) => State;
        }
    }
}
