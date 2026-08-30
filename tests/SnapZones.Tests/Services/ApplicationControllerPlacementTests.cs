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
            Guid activeProfileId)
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
                (level, message, exception) => logs.Add($"{level}: {message}"));
            var controller = new ApplicationController(
                window,
                viewModel,
                monitors,
                new RecordingStartupService(),
                dependencies);

            return new ControllerFixture(
                controller,
                viewModel,
                placementEngine,
                configurationSaveCoordinator,
                windowService,
                moveHook,
                dragCoordinator,
                logs,
                profileId);
        }

        public void Dispose() => Controller.Dispose();
    }

    private sealed class RecordingConfigurationSaveCoordinator : IConfigurationSaveCoordinator
    {
        public int FlushCalls { get; private set; }
        public SnapConfiguration? LastRequested { get; private set; }
        public Task FlushCompletion { get; set; } = Task.CompletedTask;
        public event Action<Exception?>? SaveFinished { add { } remove { } }
        public void RequestSave(SnapConfiguration configuration) => LastRequested = configuration;
        public Task FlushAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FlushCalls++;
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
        public int RunningReplaceCalls { get; private set; }
        public WindowPlacementCatalog? ReplacedCatalog { get; private set; }
        public (nint WindowHandle, Guid ProfileId, string MonitorStableId, Guid ZoneId)? LastExplicitZone { get; private set; }
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
        public Task ApplyNowAsync(WindowIdentity identity, CancellationToken cancellationToken) => Task.CompletedTask;
        public void Forget(WindowIdentity identity) { }
        public void RememberExplicitZone(nint windowHandle, Guid profileId, string monitorStableId, Guid zoneId) =>
            LastExplicitZone = (windowHandle, profileId, monitorStableId, zoneId);
        public Task FlushAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FlushCalls++;
            return Task.CompletedTask;
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
        public void Dispose() => ActionRequested = null;
    }

    private sealed class RecordingWindowService : IWindowService
    {
        public bool SnapSucceeds { get; set; } = true;
        public WindowSnapshot? Inspect(nint window, PointInt cursor, int ownProcessId) => null;
        public bool TrySnap(nint window, PixelRect bounds) => SnapSucceeds;
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
        public void Enable() => IsEnabled = true;
        public void Disable() => IsEnabled = false;
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
}
