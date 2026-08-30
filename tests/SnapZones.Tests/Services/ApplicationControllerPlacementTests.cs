using SnapZones.App.Services;
using SnapZones.App.ViewModels;
using SnapZones.App.Views;
using SnapZones.Core.Drag;
using SnapZones.Core.Geometry;
using SnapZones.Core.Models;
using SnapZones.Core.Monitors;
using SnapZones.Core.Persistence;
using SnapZones.Core.Placement;
using SnapZones.Core.Profiles;
using SnapZones.Tests.Theme;
using SnapZones.Windows.Hooks;
using SnapZones.Windows.Hotkeys;
using SnapZones.Windows.Startup;
using SnapZones.Windows.Windows;
using Xunit;

namespace SnapZones.Tests.Services;

public sealed class ApplicationControllerPlacementTests
{
    [Fact]
    public void Placement_page_actions_delegate_to_engine_and_follow_catalog_updates()
    {
        Run(fixture =>
        {
            var entry = fixture.CreatePlacementEntry();
            fixture.PlacementEngine.RaiseCatalog(new(WindowPlacementCatalog.CurrentSchemaVersion, [entry]));
            fixture.ViewModel.WindowPlacement.SelectedItem = Assert.Single(fixture.ViewModel.WindowPlacement.Items);

            fixture.ViewModel.WindowPlacement.ApplySelectedNow();
            fixture.ViewModel.WindowPlacement.ForgetSelected();

            Assert.Equal(entry.Identity, fixture.PlacementEngine.AppliedIdentity);
            Assert.Equal(entry.Identity, fixture.PlacementEngine.ForgottenIdentity);
        });
    }

    [Fact]
    public void Window_selection_selects_the_learned_identity_and_reactivates_the_main_window()
    {
        Run(fixture =>
        {
            var entry = fixture.CreatePlacementEntry();
            fixture.PlacementEngine.RaiseCatalog(new(WindowPlacementCatalog.CurrentSchemaVersion, [entry]));
            fixture.WindowSelectionService.Result = 42;
            fixture.PlacementWindowService.Snapshot = new PlacementWindowSnapshot(
                42,
                entry.Identity,
                "Editor",
                entry.NormalBoundsPixels,
                entry.NormalBoundsPixels,
                false,
                false);

            fixture.ViewModel.WindowPlacement.RequestWindowSelection();

            Assert.Equal(Environment.ProcessId, fixture.WindowSelectionService.OwnProcessId);
            Assert.Equal(TimeSpan.FromSeconds(10), fixture.WindowSelectionService.Timeout);
            Assert.Equal(entry.Identity, fixture.ViewModel.WindowPlacement.SelectedItem!.Identity);
            Assert.Contains("Hauptfenster", fixture.ViewModel.StatusMessage, StringComparison.Ordinal);
            Assert.Equal(1, fixture.MainWindowActivator.Calls);
        });
    }

    [Fact]
    public void Window_selection_timeout_returns_to_the_main_window_without_selecting_an_item()
    {
        Run(fixture =>
        {
            fixture.WindowSelectionService.Result = 0;

            fixture.ViewModel.WindowPlacement.RequestWindowSelection();

            Assert.Null(fixture.ViewModel.WindowPlacement.SelectedItem);
            Assert.Contains("kein Zielfenster", fixture.ViewModel.StatusMessage, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(1, fixture.MainWindowActivator.Calls);
        });
    }

    [Fact]
    public async Task Repeated_window_selection_is_single_flight_until_the_first_request_finishes()
    {
        ControllerFixture? fixture = null;
        WpfThemeHost.Invoke(() => fixture = ControllerFixture.Create());
        using (fixture!)
        {
            fixture!.WindowSelectionService.Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

            fixture.ViewModel.WindowPlacement.RequestWindowSelection();
            fixture.ViewModel.WindowPlacement.RequestWindowSelection();

            Assert.Equal(1, fixture.WindowSelectionService.Calls);
            fixture.WindowSelectionService.Completion.SetResult(0);
            await fixture.MainWindowActivator.Activated.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    [Fact]
    public async Task Exit_cancels_selection_and_ignores_its_late_result_without_reactivating_the_window()
    {
        ControllerFixture? fixture = null;
        WpfThemeHost.Invoke(() => fixture = ControllerFixture.Create());
        using (fixture!)
        {
            var entry = fixture!.CreatePlacementEntry();
            fixture.PlacementEngine.RaiseCatalog(new(WindowPlacementCatalog.CurrentSchemaVersion, [entry]));
            fixture.PlacementWindowService.Snapshot = new PlacementWindowSnapshot(
                42, entry.Identity, "Editor", entry.NormalBoundsPixels, entry.NormalBoundsPixels, false, false);
            fixture.WindowSelectionService.Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture.ViewModel.WindowPlacement.RequestWindowSelection();

            fixture.Controller.RequestExit();

            Assert.True(fixture.WindowSelectionService.CancellationToken.IsCancellationRequested);
            fixture.WindowSelectionService.Completion.SetResult(42);
            await fixture.ShutdownRequested.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(0, fixture.PlacementWindowService.InspectCalls);
            Assert.Equal(0, fixture.MainWindowActivator.Calls);
        }
    }

    [Fact]
    public async Task Exit_cancels_apply_and_late_completion_cannot_publish_a_success_status()
    {
        ControllerFixture? fixture = null;
        WpfThemeHost.Invoke(() => fixture = ControllerFixture.Create());
        using (fixture!)
        {
            var entry = fixture!.CreatePlacementEntry();
            fixture.PlacementEngine.ApplyCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            WpfThemeHost.Invoke(() =>
            {
                fixture.PlacementEngine.RaiseCatalog(new(WindowPlacementCatalog.CurrentSchemaVersion, [entry]));
                fixture.ViewModel.WindowPlacement.SelectedItem = fixture.ViewModel.WindowPlacement.Items[0];
                fixture.ViewModel.WindowPlacement.ApplySelectedNow();
            });
            await fixture.PlacementEngine.ApplyStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));

            fixture.Controller.RequestExit();

            Assert.True(fixture.PlacementEngine.ApplyCancellationToken.IsCancellationRequested);
            fixture.PlacementEngine.ApplyCompletion.SetResult();
            await fixture.ShutdownRequested.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.NotEqual("Fensterplatzierung angewendet", fixture.ViewModel.StatusMessage);
        }
    }

    [Fact]
    public void Reconfigure_starts_placement_engine_when_initialized_automation_is_enabled()
    {
        Run(fixture =>
        {
            fixture.Controller.InitializeWindowPlacements(new(WindowPlacementCatalog.Empty, false));

            Assert.Equal(1, fixture.PlacementEngine.StartCalls);
        });
    }

    [Fact]
    public void Reconfigure_keeps_placement_engine_stopped_before_catalog_initialization()
    {
        Run(fixture =>
        {
            fixture.Controller.Reconfigure();

            Assert.Equal(0, fixture.PlacementEngine.StartCalls);
            Assert.True(fixture.PlacementEngine.StopCalls >= 1);
            Assert.False(fixture.MoveHook.IsEnabled);
        });
    }

    [Fact]
    public void Disabled_automation_does_not_start_after_catalog_initialization()
    {
        Run(fixture =>
        {
            fixture.ViewModel.Settings.RestoreWindowPlacementEnabled = false;
            fixture.Controller.InitializeWindowPlacements(new(WindowPlacementCatalog.Empty, false));

            Assert.Equal(0, fixture.PlacementEngine.StartCalls);
            Assert.True(fixture.PlacementEngine.StopCalls >= 1);
        });
    }

    [Fact]
    public void Successful_manual_snap_records_profile_monitor_and_zone()
    {
        Run(fixture =>
        {
            var zoneId = Guid.NewGuid();
            fixture.Controller.InitializeWindowPlacements(new(WindowPlacementCatalog.Empty, false));

            fixture.DragCoordinator.Raise(new SnapWindowAction(
                42,
                new PixelRect(0, 0, 800, 600),
                "DISPLAY-1",
                zoneId));

            Assert.Equal((42, fixture.ActiveProfileId, "DISPLAY-1", zoneId), fixture.PlacementEngine.LastExplicitZone);
        });
    }

    [Fact]
    public void Failed_manual_snap_does_not_record_an_explicit_zone()
    {
        Run(fixture =>
        {
            fixture.WindowService.SnapSucceeds = false;
            fixture.Controller.InitializeWindowPlacements(new(WindowPlacementCatalog.Empty, false));

            fixture.DragCoordinator.Raise(new SnapWindowAction(
                42,
                new PixelRect(0, 0, 800, 600),
                "DISPLAY-1",
                Guid.NewGuid()));

            Assert.Null(fixture.PlacementEngine.LastExplicitZone);
        });
    }

    [Fact]
    public void Emergency_stop_disables_snapping_and_window_placement_in_saved_configuration()
    {
        Run(fixture =>
        {
            fixture.Controller.EmergencyStop("Test");

            Assert.False(fixture.ViewModel.Settings.RestoreWindowPlacementEnabled);
            Assert.False(fixture.ViewModel.Settings.SnappingEnabled);
            Assert.Equal(1, fixture.PlacementEngine.EmergencyStopCalls);
            Assert.False(fixture.ConfigurationSaveCoordinator.LastRequested!.Settings.RestoreWindowPlacementEnabled);
            Assert.False(fixture.ConfigurationSaveCoordinator.LastRequested.Settings.SnappingEnabled);
        });
    }

    [Fact]
    public void Emergency_stop_never_reenables_the_move_hook_or_saves_an_intermediate_state()
    {
        Run(fixture =>
        {
            fixture.Controller.InitializeWindowPlacements(new(WindowPlacementCatalog.Empty, false));
            fixture.MoveHook.Operations.Clear();
            fixture.ConfigurationSaveCoordinator.RequestedConfigurations.Clear();

            fixture.Controller.EmergencyStop("Test");

            Assert.DoesNotContain("Enable", fixture.MoveHook.Operations);
            Assert.NotEmpty(fixture.ConfigurationSaveCoordinator.RequestedConfigurations);
            Assert.All(fixture.ConfigurationSaveCoordinator.RequestedConfigurations, saved =>
            {
                Assert.False(saved.Settings.RestoreWindowPlacementEnabled);
                Assert.False(saved.Settings.SnappingEnabled);
            });
        });
    }

    [Fact]
    public void Queued_drag_action_after_emergency_stop_never_reaches_TrySnap()
    {
        Run(fixture =>
        {
            fixture.Controller.InitializeWindowPlacements(new(WindowPlacementCatalog.Empty, false));
            fixture.Controller.EmergencyStop("Test");

            fixture.DragCoordinator.Raise(new SnapWindowAction(
                42,
                new PixelRect(0, 0, 800, 600),
                "DISPLAY-1",
                Guid.NewGuid()));

            Assert.Equal(0, fixture.WindowService.TrySnapCalls);
            Assert.Null(fixture.PlacementEngine.LastExplicitZone);
        });
    }

    [Fact]
    public void Queued_drag_action_after_snapping_is_disabled_never_reaches_TrySnap()
    {
        Run(fixture =>
        {
            fixture.Controller.InitializeWindowPlacements(new(WindowPlacementCatalog.Empty, false));
            fixture.ViewModel.Settings.SnappingEnabled = false;

            fixture.DragCoordinator.Raise(new SnapWindowAction(
                42,
                new PixelRect(0, 0, 800, 600),
                "DISPLAY-1",
                Guid.NewGuid()));

            Assert.Equal(0, fixture.WindowService.TrySnapCalls);
        });
    }

    [Fact]
    public void Captured_drag_callback_after_dispose_never_reaches_TrySnap()
    {
        WpfThemeHost.Invoke(() =>
        {
            var fixture = ControllerFixture.Create();
            fixture.Controller.InitializeWindowPlacements(new(WindowPlacementCatalog.Empty, false));
            var queuedCallback = fixture.DragCoordinator.Capture(new SnapWindowAction(
                42,
                new PixelRect(0, 0, 800, 600),
                "DISPLAY-1",
                Guid.NewGuid()));

            fixture.Controller.Dispose();
            queuedCallback();

            Assert.Equal(0, fixture.WindowService.TrySnapCalls);
        });
    }

    [Fact]
    public async Task Flush_flushes_configuration_and_placement_catalog()
    {
        ControllerFixture? fixture = null;
        WpfThemeHost.Invoke(() => fixture = ControllerFixture.Create());
        using (fixture!)
        {
            await fixture!.Controller.FlushAsync(CancellationToken.None);

            Assert.Equal(1, fixture.ConfigurationSaveCoordinator.FlushCalls);
            Assert.Equal(1, fixture.PlacementEngine.FlushCalls);
        }
    }

    [Fact]
    public async Task Flush_starts_both_flushes_before_waiting_for_either_one()
    {
        ControllerFixture? fixture = null;
        WpfThemeHost.Invoke(() => fixture = ControllerFixture.Create());
        using (fixture!)
        {
            var blockedConfigurationFlush = new TaskCompletionSource(
                TaskCreationOptions.RunContinuationsAsynchronously);
            fixture!.ConfigurationSaveCoordinator.FlushCompletion = blockedConfigurationFlush.Task;

            var flush = fixture.Controller.FlushAsync(CancellationToken.None);

            Assert.Equal(1, fixture.ConfigurationSaveCoordinator.FlushCalls);
            Assert.Equal(1, fixture.PlacementEngine.FlushCalls);
            blockedConfigurationFlush.SetResult();
            await flush;
        }
    }

    [Fact]
    public void Dispose_stops_placement_resources_only_once()
    {
        WpfThemeHost.Invoke(() =>
        {
            var fixture = ControllerFixture.Create();

            fixture.Controller.Dispose();
            var stopCalls = fixture.PlacementEngine.StopCalls;
            fixture.Controller.Dispose();

            Assert.Equal(stopCalls, fixture.PlacementEngine.StopCalls);
        });
    }

    [Fact]
    public async Task Exit_during_placement_load_cancels_and_rejects_the_late_load_continuation()
    {
        ControllerFixture? fixture = null;
        WpfThemeHost.Invoke(() => fixture = ControllerFixture.Create());
        using (fixture!)
        {
            var repository = new LateCompletingPlacementRepository();
            var load = WindowPlacementStartupLoad.Start(repository, fixture!.StartupCancellation.Token);
            await repository.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

            fixture!.Controller.RequestExit();
            await fixture.ShutdownRequested.Task.WaitAsync(TimeSpan.FromSeconds(2));
            var saveCallsAfterExit = fixture.ConfigurationSaveCoordinator.RequestedConfigurations.Count;
            repository.Result.TrySetResult(new(WindowPlacementCatalog.Empty, false));
            var lateResult = await load;

            fixture.Controller.InitializeWindowPlacements(lateResult);

            Assert.True(fixture.StartupCancellation.IsCancellationRequested);
            Assert.Equal(0, fixture.PlacementEngine.StartCalls);
            Assert.False(fixture.MoveHook.IsEnabled);
            Assert.Equal(saveCallsAfterExit, fixture.ConfigurationSaveCoordinator.RequestedConfigurations.Count);
        }
    }

    [Fact]
    public async Task Exit_waits_for_configuration_and_placement_flush_before_shutdown()
    {
        ControllerFixture? fixture = null;
        WpfThemeHost.Invoke(() => fixture = ControllerFixture.Create());
        using (fixture!)
        {
            var configurationGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var placementGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            fixture!.ConfigurationSaveCoordinator.FlushCompletion = configurationGate.Task;
            fixture.PlacementEngine.FlushCompletion = placementGate.Task;

            fixture.Controller.RequestExit();
            await Task.WhenAll(
                fixture.ConfigurationSaveCoordinator.FlushStarted.Task,
                fixture.PlacementEngine.FlushStarted.Task).WaitAsync(TimeSpan.FromSeconds(10));

            Assert.False(fixture.ShutdownRequested.Task.IsCompleted);
            configurationGate.TrySetResult();
            Assert.False(fixture.ShutdownRequested.Task.IsCompleted);
            placementGate.TrySetResult();
            await fixture.ShutdownRequested.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    [Fact]
    public void Recovered_placement_catalog_is_applied_while_stopped_and_reported()
    {
        Run(fixture =>
        {
            var catalog = new WindowPlacementCatalog(WindowPlacementCatalog.CurrentSchemaVersion, []);

            fixture.Controller.InitializeWindowPlacements(new(catalog, true, "Wiederhergestellt"));

            Assert.Same(catalog, fixture.PlacementEngine.ReplacedCatalog);
            Assert.Equal("Wiederhergestellt", fixture.ViewModel.StatusMessage);
            Assert.Contains(fixture.LogMessages, message => message.Contains("Wiederhergestellt", StringComparison.Ordinal));
            Assert.Equal(0, fixture.PlacementEngine.RunningReplaceCalls);
        });
    }

    private static void Run(Action<ControllerFixture> action)
    {
        WpfThemeHost.Invoke(() =>
        {
            using var fixture = ControllerFixture.Create();
            action(fixture);
        });
    }

    private sealed class ControllerFixture : IDisposable
    {
        private ControllerFixture(
            ApplicationController controller,
            MainViewModel viewModel,
            RecordingWindowPlacementEngine placementEngine,
            RecordingConfigurationSaveCoordinator configurationSaveCoordinator,
            RecordingWindowService windowService,
            RecordingMoveHook moveHook,
            RecordingDragCoordinator dragCoordinator,
            IReadOnlyList<string> logMessages,
            Guid activeProfileId,
            CancellationTokenSource startupCancellation,
            TaskCompletionSource shutdownRequested)
        {
            Controller = controller;
            ViewModel = viewModel;
            PlacementEngine = placementEngine;
            ConfigurationSaveCoordinator = configurationSaveCoordinator;
            WindowService = windowService;
            MoveHook = moveHook;
            DragCoordinator = dragCoordinator;
            LogMessages = logMessages;
            ActiveProfileId = activeProfileId;
            StartupCancellation = startupCancellation;
            ShutdownRequested = shutdownRequested;
        }

        public ApplicationController Controller { get; }
        public MainViewModel ViewModel { get; }
        public RecordingWindowPlacementEngine PlacementEngine { get; }
        public RecordingConfigurationSaveCoordinator ConfigurationSaveCoordinator { get; }
        public RecordingWindowService WindowService { get; }
        public RecordingMoveHook MoveHook { get; }
        public RecordingDragCoordinator DragCoordinator { get; }
        public IReadOnlyList<string> LogMessages { get; }
        public Guid ActiveProfileId { get; }
        public CancellationTokenSource StartupCancellation { get; }
        public TaskCompletionSource ShutdownRequested { get; }
        public RecordingWindowSelectionService WindowSelectionService { get; private set; } = null!;
        public RecordingPlacementWindowService PlacementWindowService { get; private set; } = null!;
        public RecordingMainWindowActivator MainWindowActivator { get; private set; } = null!;

        public static ControllerFixture Create()
        {
            var profileId = Guid.NewGuid();
            var monitorIdentity = new MonitorIdentity("DISPLAY-1", "DISPLAY1", "Monitor");
            var configuration = new SnapConfiguration(
                SnapConfiguration.CurrentSchemaVersion,
                AppSettings.Default(profileId) with
                {
                    SnappingEnabled = true,
                    RestoreWindowPlacementEnabled = true
                },
                [new LayoutProfile(profileId, "Standard", 1, [
                    new MonitorLayout(monitorIdentity, 1920, 1080, [
                        new ZoneDefinition(Guid.NewGuid(), "Voll", NormalizedRect.Full)
                    ])
                ])]);
            var monitors = new[]
            {
                new LiveMonitor(monitorIdentity, new MonitorWorkArea(0, 0, 1920, 1040), 96, 96, true)
            };
            var viewModel = new MainViewModel(configuration, monitors);
            var window = new MainWindow();
            window.AttachViewModel(viewModel);
            var placementEngine = new RecordingWindowPlacementEngine();
            var configurationSaveCoordinator = new RecordingConfigurationSaveCoordinator();
            var windowService = new RecordingWindowService();
            var moveHook = new RecordingMoveHook();
            var dragCoordinator = new RecordingDragCoordinator();
            var dragFactory = new RecordingDragCoordinatorFactory(dragCoordinator);
            var logs = new List<string>();
            var startupCancellation = new CancellationTokenSource();
            var shutdownRequested = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var placementWindowService = new RecordingPlacementWindowService();
            var windowSelectionService = new RecordingWindowSelectionService();
            var mainWindowActivator = new RecordingMainWindowActivator();
            var dependencies = new ApplicationControllerDependencies(
                configurationSaveCoordinator,
                moveHook,
                new RecordingHotkeys(),
                windowService,
                new RecordingOverlayService(),
                new RecordingTrayService(),
                new RecordingToastService(),
                dragFactory,
                placementEngine,
                new RecordingLifecycleHook(),
                startupCancellation.Cancel,
                () => shutdownRequested.TrySetResult(),
                (level, message, exception) => logs.Add($"{level}: {message}"),
                placementWindowService,
                windowSelectionService,
                mainWindowActivator.Activate);
            var controller = new ApplicationController(
                window,
                viewModel,
                monitors,
                new RecordingStartupService(),
                dependencies);

            var fixture = new ControllerFixture(
                controller,
                viewModel,
                placementEngine,
                configurationSaveCoordinator,
                windowService,
                moveHook,
                dragCoordinator,
                logs,
                profileId,
                startupCancellation,
                shutdownRequested);
            fixture.PlacementWindowService = placementWindowService;
            fixture.WindowSelectionService = windowSelectionService;
            fixture.MainWindowActivator = mainWindowActivator;
            return fixture;
        }

        public WindowPlacementEntry CreatePlacementEntry()
        {
            var zone = ViewModel.SelectedMonitor!.Layout.Zones[0];
            return new WindowPlacementEntry(
                new WindowIdentity("editor.exe", "EditorMain", WindowKind.MainWindow),
                ViewModel.SelectedMonitor.Live.Identity.StableId,
                zone.Id,
                ViewModel.SelectedMonitor.Live.WorkArea,
                new PixelRect(0, 0, 800, 600),
                NormalizedRect.Full,
                false,
                DateTimeOffset.UtcNow);
        }

        public void Dispose() => Controller.Dispose();
    }

    private sealed class RecordingConfigurationSaveCoordinator : IConfigurationSaveCoordinator
    {
        public int FlushCalls { get; private set; }
        public SnapConfiguration? LastRequested { get; private set; }
        public List<SnapConfiguration> RequestedConfigurations { get; } = [];
        public Task FlushCompletion { get; set; } = Task.CompletedTask;
        public TaskCompletionSource FlushStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public event Action<Exception?>? SaveFinished { add { } remove { } }
        public void RequestSave(SnapConfiguration configuration)
        {
            LastRequested = configuration;
            RequestedConfigurations.Add(configuration);
        }
        public Task FlushAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FlushCalls++;
            FlushStarted.TrySetResult();
            return FlushCompletion;
        }
        public void Dispose() { }
    }

    private sealed class RecordingWindowPlacementEngine : IWindowPlacementEngine
    {
        private bool running;
        public WindowPlacementCatalog Catalog { get; private set; } = WindowPlacementCatalog.Empty;
        public event Action<WindowPlacementCatalog>? CatalogChanged;
        public int StartCalls { get; private set; }
        public int StopCalls { get; private set; }
        public int EmergencyStopCalls { get; private set; }
        public int FlushCalls { get; private set; }
        public Task FlushCompletion { get; set; } = Task.CompletedTask;
        public TaskCompletionSource FlushStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int RunningReplaceCalls { get; private set; }
        public WindowPlacementCatalog? ReplacedCatalog { get; private set; }
        public (nint WindowHandle, Guid ProfileId, string MonitorStableId, Guid ZoneId)? LastExplicitZone { get; private set; }
        public WindowIdentity? AppliedIdentity { get; private set; }
        public WindowIdentity? ForgottenIdentity { get; private set; }
        public TaskCompletionSource? ApplyCompletion { get; set; }
        public TaskCompletionSource ApplyStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationToken ApplyCancellationToken { get; private set; }
        public void Start() { StartCalls++; running = true; }
        public void Stop() { StopCalls++; running = false; }
        public void EmergencyStop() { EmergencyStopCalls++; running = false; }
        public void ReplaceCatalog(WindowPlacementCatalog catalog)
        {
            if (running) RunningReplaceCalls++;
            Catalog = catalog;
            ReplacedCatalog = catalog;
            CatalogChanged?.Invoke(catalog);
        }
        public async Task ApplyNowAsync(WindowIdentity identity, CancellationToken cancellationToken)
        {
            AppliedIdentity = identity;
            ApplyCancellationToken = cancellationToken;
            ApplyStarted.TrySetResult();
            if (ApplyCompletion is not null)
            {
                await ApplyCompletion.Task;
            }
        }
        public void Forget(WindowIdentity identity) => ForgottenIdentity = identity;
        public void RaiseCatalog(WindowPlacementCatalog catalog)
        {
            Catalog = catalog;
            CatalogChanged?.Invoke(catalog);
        }
        public void RememberExplicitZone(nint windowHandle, Guid profileId, string monitorStableId, Guid zoneId) =>
            LastExplicitZone = (windowHandle, profileId, monitorStableId, zoneId);
        public Task FlushAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FlushCalls++;
            FlushStarted.TrySetResult();
            return FlushCompletion;
        }
    }

    private sealed class RecordingWindowSelectionService : IWindowSelectionService
    {
        public nint Result { get; set; }
        public TaskCompletionSource<nint>? Completion { get; set; }
        public int Calls { get; private set; }
        public int OwnProcessId { get; private set; }
        public TimeSpan Timeout { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public async Task<nint> SelectNextAsync(int ownProcessId, TimeSpan timeout, CancellationToken cancellationToken)
        {
            Calls++;
            OwnProcessId = ownProcessId;
            Timeout = timeout;
            CancellationToken = cancellationToken;
            return Completion is null ? Result : await Completion.Task;
        }
    }

    private sealed class RecordingPlacementWindowService : IPlacementWindowService
    {
        public PlacementWindowSnapshot? Snapshot { get; set; }
        public int InspectCalls { get; private set; }
        public PlacementWindowSnapshot? Inspect(nint windowHandle, int excludedProcessId)
        {
            InspectCalls++;
            return Snapshot is { } snapshot && snapshot.WindowHandle == windowHandle ? snapshot : null;
        }
        public bool TryPlace(nint windowHandle, PixelRect normalBounds, bool maximize) => false;
        public IReadOnlyList<nint> EnumerateEligibleWindows(int excludedProcessId) => [];
        public nint GetForegroundWindow() => 0;
    }

    private sealed class RecordingMainWindowActivator
    {
        public int Calls { get; private set; }
        public TaskCompletionSource Activated { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Activate()
        {
            Calls++;
            Activated.TrySetResult();
        }
    }

    private sealed class RecordingDragCoordinatorFactory(RecordingDragCoordinator coordinator) : IWindowDragCoordinatorFactory
    {
        public IWindowDragCoordinator Create(IReadOnlyList<DragMonitorTarget> targets, OverlayScope overlayScope) => coordinator;
    }

    private sealed class RecordingDragCoordinator : IWindowDragCoordinator
    {
        public event Action<DragAction>? ActionRequested;
        public DragState State => DragState.Idle;
        public void Start(nint windowHandle, WindowSnapshot snapshot, PointInt cursor) { }
        public void Update(PointInt cursor) { }
        public void Cancel() { }
        public void End() { }
        public void End(PointInt finalCursor) { }
        public void Raise(DragAction action) => ActionRequested?.Invoke(action);
        public Action Capture(DragAction action)
        {
            var callback = ActionRequested;
            return () => callback?.Invoke(action);
        }
        public void Dispose() => ActionRequested = null;
    }

    private sealed class RecordingWindowService : IWindowService
    {
        public bool SnapSucceeds { get; set; } = true;
        public int TrySnapCalls { get; private set; }
        public WindowSnapshot? Inspect(nint window, PointInt cursor, int ownProcessId) => null;
        public bool TrySnap(nint window, PixelRect bounds)
        {
            TrySnapCalls++;
            return SnapSucceeds;
        }
        public bool TryGetCursorPosition(out PointInt point) { point = default; return false; }
        public bool IsEscapePressed() => false;
        public bool IsShiftPressed() => false;
    }

    private sealed class RecordingMoveHook : IWindowMoveHook
    {
        public event Action<nint>? MoveStarted { add { } remove { } }
        public event Action<nint>? MoveEnded { add { } remove { } }
        public event Action<string>? EmergencyStopped { add { } remove { } }
        public bool IsEnabled { get; private set; }
        public List<string> Operations { get; } = [];
        public void Enable()
        {
            Operations.Add("Enable");
            IsEnabled = true;
        }
        public void Disable()
        {
            Operations.Add("Disable");
            IsEnabled = false;
        }
        public void Dispose() { }
    }

    private sealed class RecordingHotkeys : IGlobalHotkeyService
    {
        public event Action<Guid>? ProfileRequested { add { } remove { } }
        public event Action? EmergencyStopRequested { add { } remove { } }
        public HotkeyRegistrationResult Configure(QuickSlotRegistrationPlanResult plan, bool emergencyStopEnabled) => new([]);
        public void Dispose() { }
    }

    private sealed class RecordingLifecycleHook : IWindowLifecycleHook
    {
        public event Action<WindowLifecycleEvent>? EventReceived { add { } remove { } }
        public event Action<string>? EmergencyStopped { add { } remove { } }
        public bool IsEnabled { get; private set; }
        public void Enable() => IsEnabled = true;
        public void Disable() => IsEnabled = false;
        public void Dispose() { }
    }

    private sealed class RecordingOverlayService : IApplicationOverlayService
    {
        public void UpdateTargets(IReadOnlyList<DragMonitorTarget> targets) { }
        public void Show(IReadOnlyList<string> monitorIds, LayoutMetrics metrics, string colour, double opacity, bool showZoneNames) { }
        public void Highlight(string? monitorId, Guid? zoneId) { }
        public void HideAll() { }
        public void Dispose() { }
    }

    private sealed class RecordingTrayService : IApplicationTrayService
    {
        public void Update(SnapConfiguration configuration) { }
        public void Dispose() { }
    }

    private sealed class RecordingToastService : IApplicationToastService
    {
        public void ShowProfile(string profileName) { }
        public void Dispose() { }
    }

    private sealed class RecordingStartupService : IStartupService
    {
        public bool IsEnabled { get; private set; }
        public void SetEnabled(bool enabled) => IsEnabled = enabled;
    }

    private sealed class LateCompletingPlacementRepository : IWindowPlacementRepository
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<WindowPlacementLoadResult> Result { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<WindowPlacementLoadResult> LoadAsync(CancellationToken cancellationToken)
        {
            _ = cancellationToken;
            Started.TrySetResult();
            return Result.Task;
        }

        public Task SaveAsync(WindowPlacementCatalog catalog, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
