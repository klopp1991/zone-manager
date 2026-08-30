using SnapZones.App.Services;
using SnapZones.Core.Geometry;
using SnapZones.Core.Models;
using SnapZones.Core.Persistence;
using SnapZones.Core.Placement;
using SnapZones.Windows.Hooks;
using SnapZones.Windows.Windows;
using Xunit;

namespace SnapZones.Tests.Services;

public sealed class WindowPlacementEngineTests
{
    [Fact]
    public async Task Shown_restores_a_remembered_window_once_and_does_not_hold_it_afterwards()
    {
        var fixture = EngineFixture.WithRememberedWindow(maximized: true);

        fixture.Engine.Start();
        fixture.Hook.Raise(42, WindowLifecycleEventKind.Shown);
        fixture.Hook.Raise(42, WindowLifecycleEventKind.Shown);
        await fixture.DrainAsync();

        var placement = Assert.Single(fixture.Windows.Placements);
        Assert.Equal((nint)42, placement.WindowHandle);
        Assert.True(placement.Maximize);
        Assert.Equal([TimeSpan.FromMilliseconds(100)], fixture.Delay.Delays);
    }

    [Fact]
    public async Task Shown_fails_closed_when_the_handle_identity_changes_before_placement()
    {
        var fixture = EngineFixture.WithRememberedWindow(maximized: false);
        var original = fixture.Windows.CurrentSnapshot!;
        fixture.Windows.InspectionResults.Enqueue(original);
        fixture.Windows.InspectionResults.Enqueue(original with
        {
            Identity = new WindowIdentity("C:\\Apps\\replacement.exe", "Replacement", WindowKind.MainWindow)
        });
        fixture.Engine.Start();

        fixture.Hook.Raise(42, WindowLifecycleEventKind.Shown);
        await fixture.DrainAsync();

        Assert.Empty(fixture.Windows.Placements);
    }

    [Fact]
    public async Task Exclusion_neither_places_nor_learns_the_window()
    {
        var fixture = EngineFixture.WithRule(WindowPlacementMode.Exclude);

        fixture.Engine.Start();
        fixture.Hook.Raise(42, WindowLifecycleEventKind.Shown);
        fixture.Hook.Raise(42, WindowLifecycleEventKind.MoveSizeEnded);
        await fixture.DrainAsync();

        Assert.Empty(fixture.Windows.Placements);
        Assert.Empty(fixture.Engine.Catalog.Entries);
    }

    [Fact]
    public async Task Disabled_configuration_neither_places_nor_learns_windows()
    {
        var restoreFixture = EngineFixture.WithRememberedWindow(maximized: false);
        restoreFixture.DisablePlacement();
        restoreFixture.Engine.Start();
        restoreFixture.Hook.Raise(42, WindowLifecycleEventKind.Shown);
        await restoreFixture.DrainAsync();

        var learnFixture = EngineFixture.WithCurrentWindow();
        learnFixture.DisablePlacement();
        learnFixture.Engine.Start();
        learnFixture.Hook.Raise(42, WindowLifecycleEventKind.MoveSizeEnded);
        await learnFixture.DrainAsync();

        Assert.Empty(restoreFixture.Windows.Placements);
        Assert.Empty(learnFixture.Engine.Catalog.Entries);
        Assert.Empty(learnFixture.Repository.Saved);
    }

    [Fact]
    public async Task Equal_rule_conflict_neither_places_nor_learns_and_is_logged()
    {
        var fixture = EngineFixture.WithConflictingRules();

        fixture.Engine.Start();
        fixture.Hook.Raise(42, WindowLifecycleEventKind.Shown);
        fixture.Hook.Raise(42, WindowLifecycleEventKind.MoveSizeEnded);
        await fixture.DrainAsync();

        Assert.Empty(fixture.Windows.Placements);
        Assert.Empty(fixture.Engine.Catalog.Entries);
        Assert.Contains(fixture.Log, message => message.Contains("Regelkonflikt", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Not_ready_window_is_inspected_after_exactly_three_bounded_delays()
    {
        var fixture = EngineFixture.WithUnreadableWindow();

        fixture.Engine.Start();
        fixture.Hook.Raise(42, WindowLifecycleEventKind.Shown);
        await fixture.DrainAsync();

        Assert.Equal(3, fixture.Windows.InspectCalls);
        Assert.Equal(
            [TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(300), TimeSpan.FromMilliseconds(700)],
            fixture.Delay.Delays);
    }

    [Fact]
    public async Task Minimized_window_is_not_learned()
    {
        var fixture = EngineFixture.WithCurrentWindow(isMinimized: true);

        fixture.Engine.Start();
        fixture.Hook.Raise(42, WindowLifecycleEventKind.MoveSizeEnded);
        await fixture.DrainAsync();

        Assert.Empty(fixture.Engine.Catalog.Entries);
    }

    [Fact]
    public async Task Minimize_ended_may_learn_visible_state_but_never_places()
    {
        var fixture = EngineFixture.WithCurrentWindow(isMinimized: false);

        fixture.Engine.Start();
        fixture.Hook.Raise(42, WindowLifecycleEventKind.MinimizeEnded);
        await fixture.DrainAsync();

        Assert.Empty(fixture.Windows.Placements);
        Assert.Single(fixture.Engine.Catalog.Entries);
    }

    [Fact]
    public async Task Manual_change_after_restore_becomes_the_next_remembered_normal_size_and_maximized_state()
    {
        var fixture = EngineFixture.WithRememberedWindow(maximized: false);
        fixture.Engine.Start();
        fixture.Hook.Raise(42, WindowLifecycleEventKind.Shown);
        await fixture.DrainAsync();
        fixture.Advance(TimeSpan.FromMilliseconds(751));
        fixture.Windows.CurrentSnapshot = fixture.Windows.CurrentSnapshot! with
        {
            NormalBounds = new PixelRect(300, 200, 1200, 800),
            CurrentBounds = new PixelRect(0, 0, 1920, 1080),
            IsMaximized = true
        };

        fixture.Hook.Raise(42, WindowLifecycleEventKind.MoveSizeEnded);
        await fixture.DrainAsync();

        var learned = Assert.Single(fixture.Engine.Catalog.Entries);
        Assert.Equal(new PixelRect(300, 200, 1200, 800), learned.NormalBoundsPixels);
        Assert.True(learned.WasMaximized);
    }

    [Fact]
    public async Task Movement_inside_restore_suppression_does_not_overwrite_remembered_state()
    {
        var fixture = EngineFixture.WithRememberedWindow(maximized: false);
        var original = Assert.Single(fixture.Engine.Catalog.Entries);
        fixture.Engine.Start();
        fixture.Hook.Raise(42, WindowLifecycleEventKind.Shown);
        await fixture.DrainAsync();
        fixture.Advance(TimeSpan.FromMilliseconds(749));
        fixture.Windows.CurrentSnapshot = fixture.Windows.CurrentSnapshot! with
        {
            NormalBounds = new PixelRect(300, 200, 1200, 800),
            CurrentBounds = new PixelRect(300, 200, 1200, 800)
        };

        fixture.Hook.Raise(42, WindowLifecycleEventKind.MoveSizeEnded);
        await fixture.DrainAsync();

        Assert.Same(original, Assert.Single(fixture.Engine.Catalog.Entries));
    }

    [Fact]
    public async Task Move_size_end_during_pending_shown_does_not_replace_the_restore_target()
    {
        var controlledDelay = new ControlledDelay();
        var fixture = EngineFixture.WithRememberedWindow(maximized: false, delay: controlledDelay);
        var rememberedBounds = Assert.Single(fixture.Engine.Catalog.Entries).NormalBoundsPixels;
        fixture.Engine.Start();
        fixture.Hook.Raise(42, WindowLifecycleEventKind.Shown);
        await controlledDelay.WaitForCallsAsync(1);

        fixture.Hook.Raise(42, WindowLifecycleEventKind.MoveSizeEnded);

        Assert.Equal(rememberedBounds, Assert.Single(fixture.Engine.Catalog.Entries).NormalBoundsPixels);
        controlledDelay.CompleteAll();
        await fixture.DrainAsync();
        Assert.Equal(rememberedBounds, Assert.Single(fixture.Windows.Placements).Bounds);
    }

    [Fact]
    public async Task Missing_fixed_zone_does_not_move_or_fallback_to_a_remembered_placement()
    {
        var fixture = EngineFixture.WithMissingFixedZoneAndRememberedWindow();

        fixture.Engine.Start();
        fixture.Hook.Raise(42, WindowLifecycleEventKind.Shown);
        await fixture.DrainAsync();

        Assert.Empty(fixture.Windows.Placements);
        Assert.Contains(fixture.Log, message => message.Contains("Zielzone", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Fixed_zone_places_to_the_exact_configured_target_instead_of_remembered_bounds()
    {
        var fixture = EngineFixture.WithFixedZoneAndRememberedWindow();

        fixture.Engine.Start();
        fixture.Hook.Raise(42, WindowLifecycleEventKind.Shown);
        await fixture.DrainAsync();

        var placement = Assert.Single(fixture.Windows.Placements);
        Assert.Equal(fixture.RightZone.Bounds, placement.Bounds);
        Assert.False(placement.Maximize);
    }

    [Fact]
    public async Task Location_changes_replace_the_pending_capture_per_handle()
    {
        var controlledDelay = new ControlledDelay();
        var fixture = EngineFixture.WithCurrentWindow(delay: controlledDelay);
        fixture.Engine.Start();

        fixture.Hook.Raise(42, WindowLifecycleEventKind.LocationChanged);
        await controlledDelay.WaitForCallsAsync(1);
        fixture.Hook.Raise(42, WindowLifecycleEventKind.LocationChanged);
        await controlledDelay.WaitForCallsAsync(2);
        controlledDelay.CompleteAll();
        await fixture.DrainAsync();

        Assert.Equal(2, controlledDelay.Calls.Count);
        Assert.True(controlledDelay.Calls[0].WasCancelled);
        Assert.Single(fixture.Engine.Catalog.Entries);
    }

    [Fact]
    public async Task Destroyed_uses_the_last_readable_cached_state()
    {
        var fixture = EngineFixture.WithCurrentWindow();
        fixture.Engine.Start();
        fixture.Hook.Raise(42, WindowLifecycleEventKind.MoveSizeEnded);
        await fixture.DrainAsync();
        fixture.Windows.CurrentSnapshot = fixture.Windows.CurrentSnapshot! with
        {
            NormalBounds = new PixelRect(410, 220, 900, 650),
            CurrentBounds = new PixelRect(410, 220, 900, 650)
        };
        fixture.Hook.Raise(42, WindowLifecycleEventKind.MoveSizeEnded);
        await fixture.DrainAsync();
        fixture.Windows.CurrentSnapshot = null;

        fixture.Hook.Raise(42, WindowLifecycleEventKind.Destroyed);
        await fixture.DrainAsync();

        Assert.Equal(new PixelRect(410, 220, 900, 650), Assert.Single(fixture.Engine.Catalog.Entries).NormalBoundsPixels);
    }

    [Fact]
    public async Task Minimized_snapshot_does_not_replace_the_last_visible_cache_used_by_destroyed()
    {
        var fixture = EngineFixture.WithCurrentWindow();
        var visibleBounds = fixture.Windows.CurrentSnapshot!.NormalBounds;
        fixture.Engine.Start();
        fixture.Hook.Raise(42, WindowLifecycleEventKind.Shown);
        await fixture.DrainAsync();
        fixture.Windows.CurrentSnapshot = fixture.Windows.CurrentSnapshot with
        {
            NormalBounds = new PixelRect(-32000, -32000, 160, 120),
            IsMinimized = true
        };
        fixture.Hook.Raise(42, WindowLifecycleEventKind.MoveSizeEnded);
        await fixture.DrainAsync();
        fixture.Windows.CurrentSnapshot = null;

        fixture.Hook.Raise(42, WindowLifecycleEventKind.Destroyed);
        await fixture.DrainAsync();

        Assert.Equal(visibleBounds, Assert.Single(fixture.Engine.Catalog.Entries).NormalBoundsPixels);
    }

    [Fact]
    public async Task Stop_is_idempotent_unhooks_and_cancels_pending_window_work()
    {
        var controlledDelay = new ControlledDelay();
        var fixture = EngineFixture.WithRememberedWindow(maximized: false, delay: controlledDelay);
        fixture.Engine.Start();
        fixture.Engine.Start();
        fixture.Hook.Raise(42, WindowLifecycleEventKind.Shown);
        await controlledDelay.WaitForCallsAsync(1);

        fixture.Engine.Stop();
        fixture.Engine.Stop();
        controlledDelay.CompleteAll();
        await fixture.DrainAsync();

        Assert.Equal(1, fixture.Hook.EnableCalls);
        Assert.Equal(1, fixture.Hook.DisableCalls);
        Assert.Equal(0, fixture.Hook.EventSubscriberCount);
        Assert.Equal(0, fixture.Hook.EmergencySubscriberCount);
        Assert.Empty(fixture.Windows.Placements);
    }

    [Fact]
    public async Task Flush_observes_an_operation_before_its_delegate_reaches_the_first_await()
    {
        var blockingDelay = new BlockingStartDelay();
        var fixture = EngineFixture.WithRememberedWindow(maximized: false, delay: blockingDelay);
        fixture.Engine.Start();
        var raise = Task.Run(() => fixture.Hook.Raise(42, WindowLifecycleEventKind.Shown));
        await blockingDelay.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var flush = fixture.Engine.FlushAsync(CancellationToken.None);
        var flushCompletedBeforeOperationWasReleased = flush.IsCompleted;
        blockingDelay.AllowReturn.TrySetResult();
        blockingDelay.Complete.TrySetResult();
        await raise.WaitAsync(TimeSpan.FromSeconds(2));
        await flush.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.False(flushCompletedBeforeOperationWasReleased);
    }

    [Fact]
    public void Emergency_stop_is_idempotent_and_prevents_restarting_the_hook()
    {
        var fixture = EngineFixture.WithCurrentWindow();
        fixture.Engine.Start();

        fixture.Engine.EmergencyStop();
        fixture.Engine.EmergencyStop();
        fixture.Engine.Start();

        Assert.Equal(1, fixture.Hook.EnableCalls);
        Assert.Equal(1, fixture.Hook.DisableCalls);
        Assert.Equal(0, fixture.Hook.EventSubscriberCount);
        Assert.Equal(0, fixture.Hook.EmergencySubscriberCount);
    }

    [Fact]
    public async Task Stop_is_serialized_behind_an_in_flight_start()
    {
        var fixture = EngineFixture.WithCurrentWindow();
        fixture.Hook.BlockEnable();
        var start = Task.Run(fixture.Engine.Start);
        await fixture.Hook.EnableEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var stop = Task.Run(fixture.Engine.Stop);
        var stopCompletedBeforeEnableWasReleased = stop.IsCompleted;
        fixture.Hook.AllowEnable.TrySetResult();
        await start.WaitAsync(TimeSpan.FromSeconds(2));
        await stop.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.False(stopCompletedBeforeEnableWasReleased);
        Assert.False(fixture.Hook.IsEnabled);
        Assert.Equal(0, fixture.Hook.EventSubscriberCount);
    }

    [Fact]
    public async Task Restart_is_serialized_after_an_in_flight_stop_and_remains_enabled()
    {
        var fixture = EngineFixture.WithCurrentWindow();
        fixture.Engine.Start();
        fixture.Hook.BlockDisable();
        var stop = Task.Run(fixture.Engine.Stop);
        await fixture.Hook.DisableEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var restart = Task.Run(fixture.Engine.Start);
        var restartCompletedBeforeDisableWasReleased = restart.IsCompleted;
        fixture.Hook.AllowDisable.TrySetResult();
        await stop.WaitAsync(TimeSpan.FromSeconds(2));
        await restart.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.False(restartCompletedBeforeDisableWasReleased);
        Assert.True(fixture.Hook.IsEnabled);
        Assert.Equal(1, fixture.Hook.EventSubscriberCount);
    }

    [Fact]
    public async Task Emergency_stop_is_serialized_behind_an_in_flight_start_and_stays_disabled()
    {
        var fixture = EngineFixture.WithCurrentWindow();
        fixture.Hook.BlockEnable();
        var start = Task.Run(fixture.Engine.Start);
        await fixture.Hook.EnableEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var emergency = Task.Run(fixture.Engine.EmergencyStop);
        var emergencyCompletedBeforeEnableWasReleased = emergency.IsCompleted;
        fixture.Hook.AllowEnable.TrySetResult();
        await start.WaitAsync(TimeSpan.FromSeconds(2));
        await emergency.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.False(emergencyCompletedBeforeEnableWasReleased);
        Assert.False(fixture.Hook.IsEnabled);
        Assert.Equal(0, fixture.Hook.EventSubscriberCount);
    }

    [Fact]
    public async Task Hook_emergency_signal_stops_the_engine_without_leaving_subscriptions()
    {
        var fixture = EngineFixture.WithCurrentWindow();
        fixture.Engine.Start();

        fixture.Hook.RaiseEmergency("Hook-Schutzschalter");
        await fixture.DrainAsync();

        Assert.Equal(1, fixture.Hook.DisableCalls);
        Assert.Equal(0, fixture.Hook.EventSubscriberCount);
        Assert.Contains("Hook-Schutzschalter", fixture.Log);
    }

    [Fact]
    public async Task Stop_invalidates_apply_now_while_its_window_inspection_is_in_flight()
    {
        var fixture = EngineFixture.WithRememberedWindow(maximized: false);
        fixture.Windows.EligibleWindows.Add(42);
        fixture.Windows.BlockInspection(1);
        fixture.Engine.Start();
        var apply = Task.Run(() => fixture.Engine.ApplyNowAsync(
            fixture.Windows.CurrentSnapshot!.Identity,
            CancellationToken.None));
        await fixture.Windows.InspectionEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        fixture.Engine.Stop();
        fixture.Windows.AllowInspection.TrySetResult();
        await apply.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Empty(fixture.Windows.Placements);
    }

    [Fact]
    public async Task Destroyed_invalidates_apply_now_for_the_previous_handle_epoch()
    {
        var fixture = EngineFixture.WithRememberedWindow(maximized: false);
        fixture.Windows.EligibleWindows.Add(42);
        fixture.Windows.BlockInspection(1);
        fixture.Engine.Start();
        var apply = Task.Run(() => fixture.Engine.ApplyNowAsync(
            fixture.Windows.CurrentSnapshot!.Identity,
            CancellationToken.None));
        await fixture.Windows.InspectionEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        fixture.Hook.Raise(42, WindowLifecycleEventKind.Destroyed);
        fixture.Windows.AllowInspection.TrySetResult();
        await apply.WaitAsync(TimeSpan.FromSeconds(2));
        await fixture.DrainAsync();

        Assert.Empty(fixture.Windows.Placements);
    }

    [Fact]
    public async Task Replace_catalog_invalidates_apply_now_from_the_previous_engine_generation()
    {
        var fixture = EngineFixture.WithRememberedWindow(maximized: false);
        fixture.Windows.EligibleWindows.Add(42);
        fixture.Windows.BlockInspection(1);
        var apply = Task.Run(() => fixture.Engine.ApplyNowAsync(
            fixture.Windows.CurrentSnapshot!.Identity,
            CancellationToken.None));
        await fixture.Windows.InspectionEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        fixture.Engine.ReplaceCatalog(WindowPlacementCatalog.Empty);
        fixture.Windows.AllowInspection.TrySetResult();
        await apply.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Empty(fixture.Windows.Placements);
    }

    [Fact]
    public async Task Stop_invalidates_a_capture_after_its_entry_was_built()
    {
        var fixture = EngineFixture.WithCurrentWindow();
        fixture.Clock.BlockRead(2);
        fixture.Engine.Start();
        var capture = Task.Run(() => fixture.Hook.Raise(42, WindowLifecycleEventKind.MoveSizeEnded));
        await fixture.Clock.ReadEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        fixture.Engine.Stop();
        fixture.Clock.AllowRead.TrySetResult();
        await capture.WaitAsync(TimeSpan.FromSeconds(2));
        await fixture.DrainAsync();

        Assert.Empty(fixture.Engine.Catalog.Entries);
    }

    [Fact]
    public async Task Destroyed_rejects_an_older_capture_and_keeps_its_newer_visible_state()
    {
        var fixture = EngineFixture.WithCurrentWindow();
        fixture.Clock.BlockRead(2);
        fixture.Engine.Start();
        var oldCapture = Task.Run(() => fixture.Hook.Raise(42, WindowLifecycleEventKind.MoveSizeEnded));
        await fixture.Clock.ReadEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        fixture.Windows.CurrentSnapshot = fixture.Windows.CurrentSnapshot! with
        {
            NormalBounds = new PixelRect(420, 260, 900, 640),
            CurrentBounds = new PixelRect(420, 260, 900, 640)
        };

        fixture.Hook.Raise(42, WindowLifecycleEventKind.Destroyed);
        fixture.Clock.AllowRead.TrySetResult();
        await oldCapture.WaitAsync(TimeSpan.FromSeconds(2));
        await fixture.DrainAsync();

        Assert.Equal(
            new PixelRect(420, 260, 900, 640),
            Assert.Single(fixture.Engine.Catalog.Entries).NormalBoundsPixels);
    }

    [Fact]
    public async Task New_shown_invalidates_an_older_capture_for_the_same_handle()
    {
        var fixture = EngineFixture.WithCurrentWindow();
        fixture.Clock.BlockRead(2);
        fixture.Engine.Start();
        var oldCapture = Task.Run(() => fixture.Hook.Raise(42, WindowLifecycleEventKind.MoveSizeEnded));
        await fixture.Clock.ReadEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));

        fixture.Hook.Raise(42, WindowLifecycleEventKind.Shown);
        fixture.Clock.AllowRead.TrySetResult();
        await oldCapture.WaitAsync(TimeSpan.FromSeconds(2));
        await fixture.DrainAsync();

        Assert.Empty(fixture.Engine.Catalog.Entries);
    }

    [Fact]
    public void Replace_catalog_is_only_allowed_while_stopped_and_does_not_request_a_save()
    {
        var fixture = EngineFixture.WithCurrentWindow();
        var replacement = new WindowPlacementCatalog(1, [fixture.CreateEntry("replacement", fixture.Clock.GetUtcNow())]);
        var changes = new List<WindowPlacementCatalog>();
        fixture.Engine.CatalogChanged += changes.Add;

        fixture.Engine.ReplaceCatalog(replacement);

        Assert.Same(replacement, fixture.Engine.Catalog);
        Assert.Same(replacement, Assert.Single(changes));
        Assert.Empty(fixture.Repository.Saved);
        fixture.Engine.Start();
        Assert.Throws<InvalidOperationException>(() => fixture.Engine.ReplaceCatalog(WindowPlacementCatalog.Empty));
    }

    [Fact]
    public async Task Apply_now_places_at_most_the_first_currently_matching_window()
    {
        var fixture = EngineFixture.WithRememberedWindow(maximized: false);
        fixture.Windows.EligibleWindows.AddRange([42, 43, 44]);
        fixture.Windows.Snapshots[43] = fixture.Windows.CurrentSnapshot! with { WindowHandle = 43 };
        fixture.Windows.Snapshots[44] = fixture.Windows.CurrentSnapshot! with { WindowHandle = 44 };

        await fixture.Engine.ApplyNowAsync(fixture.Windows.CurrentSnapshot!.Identity, CancellationToken.None);

        var placement = Assert.Single(fixture.Windows.Placements);
        Assert.Equal((nint)42, placement.WindowHandle);
    }

    [Fact]
    public async Task Apply_now_fails_closed_when_the_first_match_reuses_its_handle_before_placement()
    {
        var fixture = EngineFixture.WithRememberedWindow(maximized: false);
        var original = fixture.Windows.CurrentSnapshot!;
        fixture.Windows.EligibleWindows.Add(42);
        fixture.Windows.InspectionResults.Enqueue(original);
        fixture.Windows.InspectionResults.Enqueue(original with
        {
            Identity = new WindowIdentity("C:\\Apps\\replacement.exe", "Replacement", WindowKind.MainWindow)
        });

        await fixture.Engine.ApplyNowAsync(original.Identity, CancellationToken.None);

        Assert.Empty(fixture.Windows.Placements);
    }

    [Fact]
    public async Task Learning_deduplicates_sorts_and_caps_the_catalog_at_500_entries()
    {
        var fixture = EngineFixture.WithCatalogEntries(500);
        var changes = new List<WindowPlacementCatalog>();
        fixture.Engine.CatalogChanged += changes.Add;
        fixture.Engine.Start();

        fixture.Hook.Raise(42, WindowLifecycleEventKind.MoveSizeEnded);
        await fixture.DrainAsync();

        Assert.Equal(500, fixture.Engine.Catalog.Entries.Count);
        Assert.Equal(fixture.Windows.CurrentSnapshot!.Identity, fixture.Engine.Catalog.Entries[0].Identity);
        Assert.Equal(
            fixture.Engine.Catalog.Entries.OrderByDescending(entry => entry.LastUpdatedUtc),
            fixture.Engine.Catalog.Entries);
        Assert.Single(changes);
        Assert.Single(fixture.Repository.Saved);
    }

    [Fact]
    public async Task Learning_uses_the_primary_monitor_when_the_window_overlaps_no_current_monitor()
    {
        var fixture = EngineFixture.WithCurrentWindow();
        fixture.Environment = fixture.Environment with
        {
            Monitors =
            [
                new PlacementMonitorTarget("DISPLAY-A", new MonitorWorkArea(0, 0, 1920, 1080), false),
                new PlacementMonitorTarget("DISPLAY-B", new MonitorWorkArea(1920, 0, 1920, 1080), true)
            ],
            Zones = []
        };
        fixture.Windows.CurrentSnapshot = fixture.Windows.CurrentSnapshot! with
        {
            CurrentBounds = new PixelRect(-4000, 100, 800, 600),
            NormalBounds = new PixelRect(-4000, 100, 800, 600)
        };
        fixture.Engine.Start();

        fixture.Hook.Raise(42, WindowLifecycleEventKind.MoveSizeEnded);
        await fixture.DrainAsync();

        Assert.Equal("DISPLAY-B", Assert.Single(fixture.Engine.Catalog.Entries).MonitorStableId);
    }

    [Fact]
    public async Task Forget_removes_only_the_matching_identity_and_requests_persistence()
    {
        var fixture = EngineFixture.WithCatalogEntries(2);
        var forgotten = fixture.Engine.Catalog.Entries[0].Identity;

        fixture.Engine.Forget(forgotten);
        await fixture.DrainAsync();

        Assert.DoesNotContain(fixture.Engine.Catalog.Entries, entry => entry.Identity == forgotten);
        Assert.Single(fixture.Engine.Catalog.Entries);
        Assert.Single(fixture.Repository.Saved);
    }

    [Fact]
    public async Task Reentrant_catalog_change_publishes_and_saves_the_newest_catalog_last()
    {
        var fixture = EngineFixture.WithCatalogEntries(2);
        var firstIdentity = fixture.Engine.Catalog.Entries[0].Identity;
        var secondIdentity = fixture.Engine.Catalog.Entries[1].Identity;
        var callbackCount = 0;
        fixture.Engine.CatalogChanged += _ =>
        {
            if (Interlocked.Increment(ref callbackCount) == 1)
            {
                fixture.Engine.Forget(secondIdentity);
            }
        };

        fixture.Engine.Forget(firstIdentity);
        await fixture.DrainAsync();

        Assert.Empty(fixture.Engine.Catalog.Entries);
        Assert.Empty(fixture.Repository.Saved[^1].Entries);
    }

    [Fact]
    public async Task Parallel_catalog_change_publishes_and_saves_the_newest_catalog_last()
    {
        var fixture = EngineFixture.WithCatalogEntries(2);
        var firstIdentity = fixture.Engine.Catalog.Entries[0].Identity;
        var secondIdentity = fixture.Engine.Catalog.Entries[1].Identity;
        var firstCallbackEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstCallback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackCount = 0;
        fixture.Engine.CatalogChanged += _ =>
        {
            if (Interlocked.Increment(ref callbackCount) == 1)
            {
                firstCallbackEntered.TrySetResult();
                releaseFirstCallback.Task.GetAwaiter().GetResult();
            }
        };

        var firstMutation = Task.Run(() => fixture.Engine.Forget(firstIdentity));
        await firstCallbackEntered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var secondMutation = Task.Run(() => fixture.Engine.Forget(secondIdentity));
        try
        {
            await secondMutation.WaitAsync(TimeSpan.FromSeconds(2));
        }
        finally
        {
            releaseFirstCallback.TrySetResult();
        }

        await firstMutation.WaitAsync(TimeSpan.FromSeconds(2));
        await fixture.DrainAsync();

        Assert.Empty(fixture.Engine.Catalog.Entries);
        Assert.Empty(fixture.Repository.Saved[^1].Entries);
    }

    [Fact]
    public async Task Remember_explicit_zone_records_the_requested_zone_with_canonical_snapshot_state()
    {
        var fixture = EngineFixture.WithCurrentWindow();
        fixture.Engine.Start();
        fixture.Windows.CurrentSnapshot = fixture.Windows.CurrentSnapshot! with
        {
            NormalBounds = new PixelRect(270, 140, 950, 700),
            IsMaximized = true
        };

        fixture.Engine.RememberExplicitZone(42, fixture.ProfileId, "DISPLAY-A", fixture.RightZone.ZoneId);
        await fixture.DrainAsync();

        var entry = Assert.Single(fixture.Engine.Catalog.Entries);
        Assert.Equal(fixture.RightZone.ZoneId, entry.ZoneId);
        Assert.Equal(new PixelRect(270, 140, 950, 700), entry.NormalBoundsPixels);
        Assert.True(entry.WasMaximized);
    }

    private sealed class EngineFixture
    {
        private static readonly DateTimeOffset StartTime = DateTimeOffset.Parse("2026-08-30T12:00:00Z");

        private EngineFixture(
            WindowPlacementCatalog catalog,
            IReadOnlyList<WindowPlacementRule> rules,
            PlacementWindowSnapshot? snapshot,
            IEngineDelay? delay = null)
        {
            ProfileId = Guid.Parse("10000000-0000-0000-0000-000000000001");
            LeftZone = new PlacementZoneTarget(
                ProfileId,
                Guid.Parse("20000000-0000-0000-0000-000000000001"),
                "DISPLAY-A",
                new PixelRect(0, 0, 960, 1080));
            RightZone = new PlacementZoneTarget(
                ProfileId,
                Guid.Parse("20000000-0000-0000-0000-000000000002"),
                "DISPLAY-A",
                new PixelRect(960, 0, 960, 1080));
            var settings = AppSettings.Default(ProfileId) with { WindowPlacementRules = rules };
            Environment = new PlacementEnvironment(
                new SnapConfiguration(SnapConfiguration.CurrentSchemaVersion, settings, [new LayoutProfile(ProfileId, "Standard", 1, [])]),
                [new PlacementMonitorTarget("DISPLAY-A", new MonitorWorkArea(0, 0, 1920, 1080), true)],
                [LeftZone, RightZone]);
            Windows.CurrentSnapshot = snapshot;
            Repository = new RecordingPlacementRepository();
            Clock = new AdjustableTimeProvider(StartTime);
            Delay = delay ?? new ImmediateDelay();
            Engine = new WindowPlacementEngine(
                Hook,
                Windows,
                new WindowPlacementSaveCoordinator(Repository, TimeSpan.Zero),
                catalog,
                () => Environment,
                ownProcessId: 9001,
                Log.Add,
                Delay.WaitAsync,
                Clock);
        }

        public WindowPlacementEngine Engine { get; }
        public FakeLifecycleHook Hook { get; } = new();
        public FakePlacementWindowService Windows { get; } = new();
        public RecordingPlacementRepository Repository { get; }
        public AdjustableTimeProvider Clock { get; }
        public IEngineDelay Delay { get; }
        public List<string> Log { get; } = [];
        public PlacementEnvironment Environment { get; set; }
        public Guid ProfileId { get; }
        public PlacementZoneTarget LeftZone { get; }
        public PlacementZoneTarget RightZone { get; }

        public static EngineFixture WithRememberedWindow(bool maximized, IEngineDelay? delay = null)
        {
            var snapshot = CreateSnapshot();
            var entry = CreateEntry(snapshot.Identity, "remembered", StartTime.AddMinutes(-1), maximized);
            return new EngineFixture(new WindowPlacementCatalog(1, [entry]), [], snapshot, delay);
        }

        public static EngineFixture WithRule(WindowPlacementMode mode)
        {
            var snapshot = CreateSnapshot();
            return new EngineFixture(WindowPlacementCatalog.Empty, [CreateRule(snapshot.Identity, mode)], snapshot);
        }

        public static EngineFixture WithConflictingRules()
        {
            var snapshot = CreateSnapshot();
            return new EngineFixture(
                WindowPlacementCatalog.Empty,
                [CreateRule(snapshot.Identity, WindowPlacementMode.Exclude), CreateRule(snapshot.Identity, WindowPlacementMode.RememberLast)],
                snapshot);
        }

        public static EngineFixture WithUnreadableWindow() =>
            new(WindowPlacementCatalog.Empty, [], null);

        public static EngineFixture WithCurrentWindow(bool isMinimized = false, IEngineDelay? delay = null) =>
            new(WindowPlacementCatalog.Empty, [], CreateSnapshot() with { IsMinimized = isMinimized }, delay);

        public static EngineFixture WithMissingFixedZoneAndRememberedWindow()
        {
            var snapshot = CreateSnapshot();
            var missingZoneRule = CreateRule(snapshot.Identity, WindowPlacementMode.FixedZone) with
            {
                ProfileId = Guid.Parse("30000000-0000-0000-0000-000000000001"),
                MonitorStableId = "MISSING",
                ZoneId = Guid.Parse("30000000-0000-0000-0000-000000000002")
            };
            return new EngineFixture(
                new WindowPlacementCatalog(1, [CreateEntry(snapshot.Identity, "remembered", StartTime.AddMinutes(-1), false)]),
                [missingZoneRule],
                snapshot);
        }

        public static EngineFixture WithFixedZoneAndRememberedWindow()
        {
            var snapshot = CreateSnapshot();
            var fixture = WithRememberedWindow(maximized: true);
            var rule = CreateRule(snapshot.Identity, WindowPlacementMode.FixedZone) with
            {
                ProfileId = fixture.ProfileId,
                MonitorStableId = "DISPLAY-A",
                ZoneId = fixture.RightZone.ZoneId
            };
            return new EngineFixture(fixture.Engine.Catalog, [rule], snapshot);
        }

        public static EngineFixture WithCatalogEntries(int count)
        {
            var entries = Enumerable.Range(0, count)
                .Select(index => CreateEntry(
                    new WindowIdentity($"app-{index:D3}", "Main", WindowKind.MainWindow),
                    $"entry-{index:D3}",
                    StartTime.AddMinutes(-index - 1),
                    false))
                .ToArray();
            return new EngineFixture(new WindowPlacementCatalog(1, entries), [], CreateSnapshot());
        }

        public void Advance(TimeSpan duration) => Clock.Advance(duration);

        public void DisablePlacement() => Environment = Environment with
        {
            Configuration = Environment.Configuration with
            {
                Settings = Environment.Configuration.Settings with { RestoreWindowPlacementEnabled = false }
            }
        };

        public async Task DrainAsync() => await Engine.FlushAsync(CancellationToken.None);

        public WindowPlacementEntry CreateEntry(string marker, DateTimeOffset updated) =>
            CreateEntry(CreateSnapshot().Identity with { WindowClass = marker }, marker, updated, false);

        private static PlacementWindowSnapshot CreateSnapshot() => new(
            42,
            new WindowIdentity("C:\\Apps\\sample.exe", "SampleMain", WindowKind.MainWindow),
            "Sample",
            new PixelRect(100, 100, 800, 600),
            new PixelRect(100, 100, 800, 600),
            false,
            false);

        private static WindowPlacementEntry CreateEntry(
            WindowIdentity identity,
            string marker,
            DateTimeOffset updated,
            bool maximized)
        {
            var bounds = marker == "remembered"
                ? new PixelRect(140, 90, 1100, 760)
                : new PixelRect(10, 20, 800, 600);
            var workArea = new MonitorWorkArea(0, 0, 1920, 1080);
            return new WindowPlacementEntry(
                identity,
                "DISPLAY-A",
                null,
                workArea,
                bounds,
                PlacementGeometry.Normalize(bounds, workArea),
                maximized,
                updated);
        }

        private static WindowPlacementRule CreateRule(WindowIdentity identity, WindowPlacementMode mode) => new(
            Guid.NewGuid(),
            true,
            identity.ApplicationKey,
            identity.WindowClass,
            identity.Kind,
            null,
            mode,
            null,
            null,
            null);
    }

    private interface IEngineDelay
    {
        IReadOnlyList<TimeSpan> Delays { get; }
        Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken);
    }

    private sealed class ImmediateDelay : IEngineDelay
    {
        public List<TimeSpan> RecordedDelays { get; } = [];
        public IReadOnlyList<TimeSpan> Delays => RecordedDelays;

        public Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RecordedDelays.Add(delay);
            return Task.CompletedTask;
        }
    }

    private sealed class ControlledDelay : IEngineDelay
    {
        private readonly object synchronization = new();
        private readonly List<DelayCall> calls = [];

        public IReadOnlyList<DelayCall> Calls
        {
            get
            {
                lock (synchronization)
                {
                    return calls.ToArray();
                }
            }
        }

        public IReadOnlyList<TimeSpan> Delays => Calls.Select(call => call.Delay).ToArray();

        public Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            var call = new DelayCall(delay, cancellationToken);
            lock (synchronization)
            {
                calls.Add(call);
            }

            return call.Task;
        }

        public async Task WaitForCallsAsync(int expected)
        {
            for (var attempt = 0; attempt < 1000 && Calls.Count < expected; attempt++)
            {
                await Task.Yield();
            }

            Assert.True(Calls.Count >= expected, $"Erwartete {expected} Delay-Aufrufe, erhalten: {Calls.Count}.");
        }

        public void CompleteAll()
        {
            foreach (var call in Calls)
            {
                call.Complete();
            }
        }

        public sealed class DelayCall
        {
            private readonly TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly CancellationTokenRegistration registration;

            public DelayCall(TimeSpan delay, CancellationToken cancellationToken)
            {
                Delay = delay;
                registration = cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
            }

            public TimeSpan Delay { get; }
            public bool WasCancelled => Task.IsCanceled;
            public Task Task => completion.Task;

            public void Complete()
            {
                registration.Dispose();
                completion.TrySetResult();
            }
        }
    }

    private sealed class BlockingStartDelay : IEngineDelay
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource AllowReturn { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Complete { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public IReadOnlyList<TimeSpan> Delays { get; private set; } = [];

        public Task WaitAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            Delays = [delay];
            Entered.TrySetResult();
            AllowReturn.Task.GetAwaiter().GetResult();
            return Complete.Task.WaitAsync(cancellationToken);
        }
    }

    private sealed class FakeLifecycleHook : IWindowLifecycleHook
    {
        private Action<WindowLifecycleEvent>? eventReceived;
        private Action<string>? emergencyStopped;

        public event Action<WindowLifecycleEvent>? EventReceived
        {
            add => eventReceived += value;
            remove => eventReceived -= value;
        }

        public event Action<string>? EmergencyStopped
        {
            add => emergencyStopped += value;
            remove => emergencyStopped -= value;
        }

        public bool IsEnabled { get; private set; }
        public int EnableCalls { get; private set; }
        public int DisableCalls { get; private set; }
        public int EventSubscriberCount => eventReceived?.GetInvocationList().Length ?? 0;
        public int EmergencySubscriberCount => emergencyStopped?.GetInvocationList().Length ?? 0;
        public TaskCompletionSource EnableEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource AllowEnable { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource DisableEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource AllowDisable { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool blockEnable;
        private bool blockDisable;

        public void BlockEnable() => blockEnable = true;

        public void BlockDisable() => blockDisable = true;

        public void Enable()
        {
            EnableCalls++;
            if (blockEnable)
            {
                blockEnable = false;
                EnableEntered.TrySetResult();
                AllowEnable.Task.GetAwaiter().GetResult();
            }

            IsEnabled = true;
        }

        public void Disable()
        {
            if (!IsEnabled)
            {
                return;
            }

            DisableCalls++;
            if (blockDisable)
            {
                blockDisable = false;
                DisableEntered.TrySetResult();
                AllowDisable.Task.GetAwaiter().GetResult();
            }

            IsEnabled = false;
        }

        public void Raise(nint handle, WindowLifecycleEventKind kind) =>
            eventReceived?.Invoke(new WindowLifecycleEvent(handle, kind));

        public void RaiseEmergency(string message) => emergencyStopped?.Invoke(message);

        public void Dispose() => Disable();
    }

    private sealed class FakePlacementWindowService : IPlacementWindowService
    {
        public PlacementWindowSnapshot? CurrentSnapshot { get; set; }
        public Queue<PlacementWindowSnapshot?> InspectionResults { get; } = [];
        public Dictionary<nint, PlacementWindowSnapshot> Snapshots { get; } = [];
        public List<nint> EligibleWindows { get; } = [];
        public List<PlacementCall> Placements { get; } = [];
        public int InspectCalls { get; private set; }
        public TaskCompletionSource InspectionEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource AllowInspection { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int blockedInspectionCall = -1;

        public void BlockInspection(int callNumber) => blockedInspectionCall = callNumber;

        public PlacementWindowSnapshot? Inspect(nint windowHandle, int excludedProcessId)
        {
            InspectCalls++;
            if (InspectCalls == blockedInspectionCall)
            {
                InspectionEntered.TrySetResult();
                AllowInspection.Task.GetAwaiter().GetResult();
            }

            if (InspectionResults.Count != 0)
            {
                return InspectionResults.Dequeue();
            }

            if (Snapshots.TryGetValue(windowHandle, out var snapshot))
            {
                return snapshot;
            }

            return CurrentSnapshot?.WindowHandle == windowHandle ? CurrentSnapshot : null;
        }

        public bool TryPlace(nint windowHandle, PixelRect normalBounds, bool maximize)
        {
            Placements.Add(new PlacementCall(windowHandle, normalBounds, maximize));
            if (CurrentSnapshot?.WindowHandle == windowHandle)
            {
                CurrentSnapshot = CurrentSnapshot with
                {
                    CurrentBounds = normalBounds,
                    NormalBounds = normalBounds,
                    IsMaximized = maximize,
                    IsMinimized = false
                };
            }

            return true;
        }

        public IReadOnlyList<nint> EnumerateEligibleWindows(int excludedProcessId) => EligibleWindows;

        public nint GetForegroundWindow() => 0;
    }

    private sealed record PlacementCall(nint WindowHandle, PixelRect Bounds, bool Maximize);

    private sealed class RecordingPlacementRepository : IWindowPlacementRepository
    {
        private readonly object synchronization = new();
        private readonly List<WindowPlacementCatalog> saved = [];

        public IReadOnlyList<WindowPlacementCatalog> Saved
        {
            get
            {
                lock (synchronization)
                {
                    return saved.ToArray();
                }
            }
        }

        public Task<WindowPlacementLoadResult> LoadAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task SaveAsync(WindowPlacementCatalog catalog, CancellationToken cancellationToken)
        {
            lock (synchronization)
            {
                saved.Add(catalog);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class AdjustableTimeProvider(DateTimeOffset initialTime) : TimeProvider
    {
        private DateTimeOffset currentTime = initialTime;
        private int readCount;
        private int blockedRead = -1;

        public TaskCompletionSource ReadEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource AllowRead { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override DateTimeOffset GetUtcNow()
        {
            if (Interlocked.Increment(ref readCount) == blockedRead)
            {
                ReadEntered.TrySetResult();
                AllowRead.Task.GetAwaiter().GetResult();
            }

            return currentTime;
        }

        public void BlockRead(int readNumber) => blockedRead = readNumber;

        public void Advance(TimeSpan duration) => currentTime += duration;
    }
}
