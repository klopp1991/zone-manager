using SnapZones.Core.Fullscreen;
using SnapZones.Core.Geometry;
using SnapZones.Core.Placement;
using Xunit;

namespace SnapZones.Tests.Fullscreen;

public sealed class ZoneFullscreenTrackerTests
{
    private const nint Window = 0x1234;
    private static readonly Guid Profile = Guid.NewGuid();
    private static readonly PixelRect Monitor = new(0, 0, 1920, 1080);
    private static readonly PixelRect LeftZone = new(8, 8, 952, 1064);
    private static readonly PixelRect RightZone = new(960, 8, 952, 1064);
    private static readonly PixelRect Fullscreen = new(0, 0, 1920, 1080);
    private static readonly DateTimeOffset Start = new(2026, 9, 3, 20, 0, 0, TimeSpan.Zero);

    private static readonly ZoneFullscreenOptions Enabled =
        new(Enabled: true, SnappedTolerancePixels: 40, MaximumCorrections: 3, CorrectionWindow: TimeSpan.FromSeconds(5));

    [Fact]
    public void A_window_snapped_to_a_zone_is_pulled_back_into_it_when_it_goes_fullscreen()
    {
        var tracker = new ZoneFullscreenTracker();
        tracker.Evaluate(Window, Observe(RightZone), Enabled);

        var decision = tracker.Evaluate(Window, Observe(Fullscreen), Enabled);

        Assert.Equal(ZoneFullscreenAction.HoldInZone, decision.Action);
        Assert.Equal(RightZone, decision.Bounds);
        Assert.True(tracker.IsHeld(Window));
    }

    [Fact]
    public void A_window_that_was_not_in_a_zone_keeps_the_whole_monitor()
    {
        var tracker = new ZoneFullscreenTracker();
        tracker.Evaluate(Window, Observe(new PixelRect(300, 200, 800, 600)), Enabled);

        var decision = tracker.Evaluate(Window, Observe(Fullscreen), Enabled);

        Assert.Equal(ZoneFullscreenAction.None, decision.Action);
        Assert.False(tracker.IsHeld(Window));
    }

    [Fact]
    public void A_maximized_window_is_left_alone_even_when_it_covers_the_whole_monitor()
    {
        // Bei automatisch ausgeblendeter Taskleiste deckt ein maximiertes Fenster den ganzen Monitor.
        // Es ist trotzdem kein Vollbild und wird nicht angefasst.
        var tracker = new ZoneFullscreenTracker();
        tracker.Evaluate(Window, Observe(LeftZone), Enabled);

        var decision = tracker.Evaluate(Window, Observe(Fullscreen) with { IsMaximized = true }, Enabled);

        Assert.Equal(ZoneFullscreenAction.None, decision.Action);
    }

    [Fact]
    public void The_echo_of_the_own_correction_does_not_trigger_another_one()
    {
        var tracker = new ZoneFullscreenTracker();
        tracker.Evaluate(Window, Observe(LeftZone), Enabled);
        tracker.Evaluate(Window, Observe(Fullscreen), Enabled);

        // Das Ereignis, das die eigene Platzierung ausloest.
        var decision = tracker.Evaluate(Window, Observe(LeftZone), Enabled);

        Assert.Equal(ZoneFullscreenAction.None, decision.Action);
        Assert.True(tracker.IsHeld(Window));
    }

    [Fact]
    public void A_player_that_sets_itself_back_to_the_monitor_is_corrected_again()
    {
        var tracker = new ZoneFullscreenTracker();
        tracker.Evaluate(Window, Observe(LeftZone), Enabled);
        tracker.Evaluate(Window, Observe(Fullscreen), Enabled);

        var decision = tracker.Evaluate(Window, Observe(Fullscreen), Enabled);

        Assert.Equal(ZoneFullscreenAction.HoldInZone, decision.Action);
        Assert.Equal(LeftZone, decision.Bounds);
    }

    [Fact]
    public void After_the_allowed_attempts_the_window_keeps_its_monitor_fullscreen()
    {
        var tracker = new ZoneFullscreenTracker();
        tracker.Evaluate(Window, Observe(LeftZone), Enabled);
        for (var attempt = 0; attempt < Enabled.MaximumCorrections; attempt++)
        {
            Assert.Equal(ZoneFullscreenAction.HoldInZone, tracker.Evaluate(Window, Observe(Fullscreen), Enabled).Action);
        }

        var decision = tracker.Evaluate(Window, Observe(Fullscreen), Enabled);

        Assert.Equal(ZoneFullscreenAction.None, decision.Action);
        Assert.NotNull(decision.Reason);
    }

    [Fact]
    public void The_attempt_count_starts_over_after_a_quiet_period()
    {
        var tracker = new ZoneFullscreenTracker();
        tracker.Evaluate(Window, Observe(LeftZone), Enabled);
        for (var attempt = 0; attempt < Enabled.MaximumCorrections; attempt++)
        {
            tracker.Evaluate(Window, Observe(Fullscreen), Enabled);
        }

        var later = Start + Enabled.CorrectionWindow + TimeSpan.FromSeconds(1);
        var decision = tracker.Evaluate(Window, Observe(Fullscreen, later), Enabled);

        Assert.Equal(ZoneFullscreenAction.HoldInZone, decision.Action);
    }

    [Fact]
    public void Leaving_fullscreen_releases_the_window_and_a_later_fullscreen_works_again()
    {
        var tracker = new ZoneFullscreenTracker();
        tracker.Evaluate(Window, Observe(LeftZone), Enabled);
        tracker.Evaluate(Window, Observe(Fullscreen), Enabled);

        // Der Player verlaesst das Vollbild und setzt sich auf ein eigenes Rechteck.
        tracker.Evaluate(Window, Observe(new PixelRect(200, 150, 1000, 700)), Enabled);
        Assert.False(tracker.IsHeld(Window));

        // Wieder in eine Zone gezogen, dann erneut Vollbild.
        tracker.Evaluate(Window, Observe(RightZone), Enabled);
        var decision = tracker.Evaluate(Window, Observe(Fullscreen), Enabled);

        Assert.Equal(ZoneFullscreenAction.HoldInZone, decision.Action);
        Assert.Equal(RightZone, decision.Bounds);
    }

    [Fact]
    public void A_zone_that_already_covers_the_whole_monitor_is_left_alone()
    {
        var full = new[] { new PlacementZoneTarget(Profile, Guid.NewGuid(), "DISPLAY-A", Monitor) };
        var tracker = new ZoneFullscreenTracker();
        tracker.Evaluate(Window, Observe(Fullscreen, zones: full), Enabled);

        var decision = tracker.Evaluate(Window, Observe(Fullscreen, zones: full), Enabled);

        Assert.Equal(ZoneFullscreenAction.None, decision.Action);
        Assert.False(tracker.IsHeld(Window));
    }

    [Fact]
    public void A_minimized_window_keeps_its_remembered_zone()
    {
        var tracker = new ZoneFullscreenTracker();
        tracker.Evaluate(Window, Observe(LeftZone), Enabled);

        // Ein minimiertes Fenster meldet ein Rechteck weit ausserhalb des Monitors.
        var minimized = Observe(new PixelRect(-32000, -32000, 160, 28)) with { IsMinimized = true };
        Assert.Equal(ZoneFullscreenAction.None, tracker.Evaluate(Window, minimized, Enabled).Action);

        var decision = tracker.Evaluate(Window, Observe(Fullscreen), Enabled);

        Assert.Equal(ZoneFullscreenAction.HoldInZone, decision.Action);
        Assert.Equal(LeftZone, decision.Bounds);
    }

    [Fact]
    public void Switching_the_feature_off_forgets_the_window()
    {
        var tracker = new ZoneFullscreenTracker();
        tracker.Evaluate(Window, Observe(LeftZone), Enabled);
        Assert.Equal(1, tracker.TrackedWindows);

        var decision = tracker.Evaluate(Window, Observe(Fullscreen), Enabled with { Enabled = false });

        Assert.Equal(ZoneFullscreenAction.None, decision.Action);
        Assert.Equal(0, tracker.TrackedWindows);
    }

    [Fact]
    public void A_window_outside_every_zone_leaves_no_entry_behind()
    {
        // Die Fensterereignisse kommen von jeder Anwendung der Sitzung, auch von der Shell und von
        // Hinweisfenstern. Ohne dieses Aufraeumen waechst das Gedaechtnis mit jedem gesehenen Fenster.
        var tracker = new ZoneFullscreenTracker();

        tracker.Evaluate(Window, Observe(new PixelRect(300, 200, 800, 600)), Enabled);
        tracker.Evaluate(Window + 1, Observe(Fullscreen), Enabled);

        Assert.Equal(0, tracker.TrackedWindows);
    }

    [Fact]
    public void Clear_drops_every_remembered_window()
    {
        var tracker = new ZoneFullscreenTracker();
        tracker.Evaluate(Window, Observe(LeftZone), Enabled);
        tracker.Evaluate(Window + 1, Observe(RightZone), Enabled);

        tracker.Clear();

        Assert.Equal(0, tracker.TrackedWindows);
        Assert.Equal(ZoneFullscreenAction.None, tracker.Evaluate(Window, Observe(Fullscreen), Enabled).Action);
    }

    [Fact]
    public void A_borderless_window_that_only_partly_falls_back_is_corrected_instead_of_released()
    {
        // Chromium stellt bei einem Aktivierungswechsel nur einen Teil seiner Vollbildgroesse wieder
        // her, hier die Breite. Das Fenster ist weiter im Vollbild (kein Rahmen) und gehoert zurueck in
        // die Zone; die verfaelschte Flaeche darf nicht als neues Einrasten gelesen werden.
        var tracker = new ZoneFullscreenTracker();
        tracker.Evaluate(Window, Observe(LeftZone), Enabled);
        tracker.Evaluate(Window, Observe(Fullscreen) with { IsBorderless = true }, Enabled);

        var widened = new PixelRect(LeftZone.X, LeftZone.Y, Monitor.Width, LeftZone.Height);
        var decision = tracker.Evaluate(Window, Observe(widened) with { IsBorderless = true }, Enabled);

        Assert.Equal(ZoneFullscreenAction.HoldInZone, decision.Action);
        Assert.Equal(LeftZone, decision.Bounds);
        Assert.True(tracker.IsHeld(Window));
    }

    [Fact]
    public void A_window_with_its_frame_back_on_an_own_rectangle_has_left_fullscreen()
    {
        var tracker = new ZoneFullscreenTracker();
        tracker.Evaluate(Window, Observe(LeftZone), Enabled);
        tracker.Evaluate(Window, Observe(Fullscreen) with { IsBorderless = true }, Enabled);

        var decision = tracker.Evaluate(Window, Observe(new PixelRect(200, 150, 1000, 700)), Enabled);

        Assert.Equal(ZoneFullscreenAction.None, decision.Action);
        Assert.False(tracker.IsHeld(Window));
    }

    [Fact]
    public void A_failed_correction_keeps_the_remembered_zone_for_the_next_fallback()
    {
        // Das Setzen hat laut Nachmessen nicht gegriffen. Der Halt faellt weg, die Zone bleibt: beim
        // naechsten Rueckfall auf den Monitor wird das Fenster wieder dorthin geholt.
        var tracker = new ZoneFullscreenTracker();
        tracker.Evaluate(Window, Observe(LeftZone), Enabled);
        tracker.Evaluate(Window, Observe(Fullscreen), Enabled);

        tracker.CorrectionFailed(Window);
        Assert.False(tracker.IsHeld(Window));

        var decision = tracker.Evaluate(Window, Observe(Fullscreen), Enabled);

        Assert.Equal(ZoneFullscreenAction.HoldInZone, decision.Action);
        Assert.Equal(LeftZone, decision.Bounds);
    }

    [Fact]
    public void Failed_corrections_still_count_against_the_budget()
    {
        var tracker = new ZoneFullscreenTracker();
        tracker.Evaluate(Window, Observe(LeftZone), Enabled);
        for (var attempt = 0; attempt < Enabled.MaximumCorrections; attempt++)
        {
            Assert.Equal(ZoneFullscreenAction.HoldInZone, tracker.Evaluate(Window, Observe(Fullscreen), Enabled).Action);
            tracker.CorrectionFailed(Window);
        }

        var decision = tracker.Evaluate(Window, Observe(Fullscreen), Enabled);

        Assert.Equal(ZoneFullscreenAction.None, decision.Action);
        Assert.NotNull(decision.Reason);
    }

    [Fact]
    public void A_borderless_window_outside_every_zone_does_not_overwrite_the_remembered_zone()
    {
        var tracker = new ZoneFullscreenTracker();
        tracker.Evaluate(Window, Observe(LeftZone), Enabled);

        // Auf dem Weg ins Vollbild meldet der Browser ein Zwischenrechteck ohne Rahmen.
        tracker.Evaluate(Window, Observe(new PixelRect(0, 0, 1500, 900)) with { IsBorderless = true }, Enabled);
        var decision = tracker.Evaluate(Window, Observe(Fullscreen) with { IsBorderless = true }, Enabled);

        Assert.Equal(ZoneFullscreenAction.HoldInZone, decision.Action);
        Assert.Equal(LeftZone, decision.Bounds);
    }

    private static ZoneFullscreenObservation Observe(
        PixelRect bounds,
        DateTimeOffset? timestamp = null,
        IReadOnlyList<PlacementZoneTarget>? zones = null) =>
        new(
            bounds,
            Monitor,
            IsMaximized: false,
            IsMinimized: false,
            zones ?? DefaultZones,
            timestamp ?? Start);

    private static readonly PlacementZoneTarget[] DefaultZones =
    [
        new(Profile, Guid.NewGuid(), "DISPLAY-A", LeftZone),
        new(Profile, Guid.NewGuid(), "DISPLAY-A", RightZone)
    ];
}
