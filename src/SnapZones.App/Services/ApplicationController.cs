using System.ComponentModel;
using System.IO;
using System.Windows.Threading;
using SnapZones.App.ViewModels;
using SnapZones.App.Views;
using SnapZones.Core.Drag;
using SnapZones.Core.Models;
using SnapZones.Core.Monitors;
using SnapZones.Core.Persistence;
using SnapZones.Core.Profiles;
using SnapZones.Windows.Hooks;
using SnapZones.Windows.Hotkeys;
using SnapZones.Windows.Startup;
using SnapZones.Windows.Windows;

namespace SnapZones.App.Services;

public sealed class ApplicationController : IDisposable
{
    private readonly MainWindow window;
    private readonly MainViewModel viewModel;
    private readonly IReadOnlyList<LiveMonitor> monitors;
    private readonly IStartupService startupService;
    private Action<string, string, Exception?> log = null!;
    private IWindowMoveHook moveHook = null!;
    private IGlobalHotkeyService hotkeys = null!;
    private IWindowService windowService = null!;
    private IApplicationOverlayService overlays = null!;
    private IApplicationTrayService tray = null!;
    private IApplicationToastService toast = null!;
    private IWindowDragCoordinatorFactory dragCoordinatorFactory = null!;
    private IWindowPlacementEngine placementEngine = null!;
    private IWindowLifecycleHook placementLifecycleHook = null!;
    private Action cancelStartup = null!;
    private Action shutdownApplication = null!;
    private DispatcherTimer cursorTimer = null!;
    private IConfigurationSaveCoordinator saveCoordinator = null!;
    private readonly ConfigurationTransferService transferService = new();
    private readonly object lifecycleGate = new();
    private readonly object automationGate = new();
    private IWindowDragCoordinator? coordinator;
    private SnapConfiguration configuration;
    private int exitRequested;
    private bool allowClose;
    private bool disposed;
    private bool placementsInitialized;
    private int safetyStopped;

    public ApplicationController(
        MainWindow window,
        MainViewModel viewModel,
        IConfigurationRepository configurationRepository,
        IWindowPlacementRepository placementRepository,
        IReadOnlyList<LiveMonitor> monitors,
        IStartupService startupService,
        Action cancelStartup,
        FileLog log)
    {
        this.window = window;
        this.viewModel = viewModel;
        this.monitors = monitors;
        this.startupService = startupService;
        configuration = viewModel.Configuration;
        var dependencies = ApplicationControllerDependencies.CreateDefault(
            window,
            configurationRepository,
            placementRepository,
            monitors,
            () => configuration,
            cancelStartup,
            ActivateProfile,
            ToggleSnapping,
            RequestExit,
            log);
        Initialize(dependencies);
    }

    public ApplicationController(
        MainWindow window,
        MainViewModel viewModel,
        IReadOnlyList<LiveMonitor> monitors,
        IStartupService startupService,
        ApplicationControllerDependencies dependencies)
    {
        this.window = window;
        this.viewModel = viewModel;
        this.monitors = monitors;
        this.startupService = startupService;
        configuration = viewModel.Configuration;
        Initialize(dependencies);
    }

    private void Initialize(ApplicationControllerDependencies dependencies)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        log = dependencies.Log;
        saveCoordinator = dependencies.ConfigurationSaveCoordinator;
        saveCoordinator.SaveFinished += SaveFinished;
        overlays = dependencies.Overlays;
        windowService = dependencies.WindowService;
        moveHook = dependencies.MoveHook;
        hotkeys = dependencies.Hotkeys;
        tray = dependencies.Tray;
        toast = dependencies.Toast;
        dragCoordinatorFactory = dependencies.DragCoordinatorFactory;
        placementEngine = dependencies.PlacementEngine;
        placementLifecycleHook = dependencies.PlacementLifecycleHook;
        cancelStartup = dependencies.CancelStartup;
        shutdownApplication = dependencies.ShutdownApplication;
        cursorTimer = new DispatcherTimer(DispatcherPriority.Input, window.Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(33)
        };
        cursorTimer.Tick += CursorTimer_Tick;
        viewModel.SaveRequested += SaveRequested;
        window.ExportConfigurationRequested += ExportConfigurationAsync;
        window.ImportConfigurationRequested += ImportConfigurationAsync;
        moveHook.MoveStarted += MoveStarted;
        moveHook.MoveEnded += MoveHook_MoveEnded;
        moveHook.EmergencyStopped += MoveHook_EmergencyStopped;
        hotkeys.ProfileRequested += ActivateProfile;
        hotkeys.EmergencyStopRequested += Hotkeys_EmergencyStopRequested;
        placementLifecycleHook.EmergencyStopped += PlacementLifecycleHook_EmergencyStopped;
        window.Closing += Window_Closing;

        Reconfigure();
    }

    public void EmergencyStop(string reason)
    {
        lock (automationGate)
        {
            if (disposed)
            {
                return;
            }

            Volatile.Write(ref safetyStopped, 1);
            cursorTimer.Stop();
            coordinator?.Cancel();
            overlays.HideAll();
            moveHook.Disable();
            placementEngine.EmergencyStop();
        }

        viewModel.DisableSnappingForSafety(reason);
        configuration = viewModel.Configuration;
        _ = hotkeys.Configure(QuickSlotRegistrationPlan.Build(configuration), emergencyStopEnabled: false);
        tray.Update(configuration);
        log("WARN", reason, null);
        saveCoordinator.RequestSave(configuration);
    }

    public void InitializeWindowPlacements(WindowPlacementLoadResult loadResult)
    {
        ArgumentNullException.ThrowIfNull(loadResult);
        lock (lifecycleGate)
        {
            if (disposed || Volatile.Read(ref exitRequested) != 0)
            {
                return;
            }

            placementEngine.Stop();
            placementEngine.ReplaceCatalog(loadResult.Catalog);
            placementsInitialized = true;
            if (loadResult.RecoveredFromError)
            {
                var message = loadResult.ErrorMessage ?? "Die Fensterplatzierungen wurden zurückgesetzt.";
                viewModel.StatusMessage = message;
                log("WARN", message, null);
            }

            ReconfigureCore();
        }
    }

    public void Dispose()
    {
        lock (automationGate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            Volatile.Write(ref safetyStopped, 1);
        }
        allowClose = true;
        cursorTimer.Stop();
        cursorTimer.Tick -= CursorTimer_Tick;
        lock (lifecycleGate)
        {
            placementEngine.Stop();
            moveHook.Disable();
        }
        placementLifecycleHook.EmergencyStopped -= PlacementLifecycleHook_EmergencyStopped;
        moveHook.MoveStarted -= MoveStarted;
        moveHook.MoveEnded -= MoveHook_MoveEnded;
        moveHook.EmergencyStopped -= MoveHook_EmergencyStopped;
        hotkeys.ProfileRequested -= ActivateProfile;
        hotkeys.EmergencyStopRequested -= Hotkeys_EmergencyStopRequested;
        viewModel.SaveRequested -= SaveRequested;
        window.Closing -= Window_Closing;
        overlays.HideAll();
        coordinator?.Dispose();
        coordinator = null;
        tray.Dispose();
        hotkeys.Dispose();
        moveHook.Dispose();
        placementLifecycleHook.Dispose();
        overlays.Dispose();
        toast.Dispose();
        saveCoordinator.SaveFinished -= SaveFinished;
        saveCoordinator.Dispose();
        window.ExportConfigurationRequested -= ExportConfigurationAsync;
        window.ImportConfigurationRequested -= ImportConfigurationAsync;
    }

    private void SaveRequested(SnapConfiguration newConfiguration)
    {
        configuration = newConfiguration;
        Reconfigure();
        try
        {
            if (startupService.IsEnabled != newConfiguration.Settings.StartWithWindows)
            {
                startupService.SetEnabled(newConfiguration.Settings.StartWithWindows);
            }
        }
        catch (Exception exception)
        {
            log("ERROR", "Die Autostart-Einstellung konnte nicht übernommen werden.", exception);
            viewModel.StatusMessage = $"Autostart konnte nicht geändert werden: {exception.Message}";
        }

        saveCoordinator.RequestSave(newConfiguration);
    }

    public async Task FlushAsync(CancellationToken cancellationToken)
    {
        var configurationFlush = saveCoordinator.FlushAsync(cancellationToken);
        var placementFlush = placementEngine.FlushAsync(cancellationToken);
        await Task.WhenAll(configurationFlush, placementFlush);
    }

    public void RequestExit()
    {
        if (Interlocked.Exchange(ref exitRequested, 1) != 0)
        {
            return;
        }

        cancelStartup();
        lock (lifecycleGate)
        {
            moveHook.Disable();
            placementEngine.Stop();
        }

        _ = window.Dispatcher.InvokeAsync(
            new Action(() => _ = ExitApplicationAsync()),
            DispatcherPriority.ContextIdle);
    }

    private async Task ExportConfigurationAsync(string filePath)
    {
        viewModel.Save();
        await saveCoordinator.FlushAsync(CancellationToken.None);
        var productVersion = typeof(ApplicationController).Assembly.GetName().Version?.ToString() ?? "0.0.0";
        await transferService.ExportAsync(
            filePath,
            viewModel.Configuration,
            productVersion,
            CancellationToken.None);
        viewModel.StatusMessage = $"Vollbackup exportiert: {Path.GetFileName(filePath)}";
    }

    private async Task ImportConfigurationAsync(string filePath)
    {
        viewModel.Save();
        await saveCoordinator.FlushAsync(CancellationToken.None);
        var imported = await transferService.ImportAsync(filePath, CancellationToken.None);
        var monitorCount = imported.Profiles.Sum(profile => profile.Monitors.Count);
        var zoneCount = imported.Profiles.Sum(profile => profile.Monitors.Sum(monitor => monitor.Zones.Count));
        var impact =
            $"Der Import ersetzt sämtliche aktuellen Einstellungen, Profile und Layouts.\n\n" +
            $"Importdatei: {imported.Profiles.Count} Profile, {monitorCount} Monitorlayouts, {zoneCount} Zonen.\n" +
            "Der bisherige Zustand wird unmittelbar davor automatisch gesichert.";
        if (System.Windows.MessageBox.Show(
                impact,
                "Vollständige Konfiguration importieren",
                System.Windows.MessageBoxButton.OKCancel,
                System.Windows.MessageBoxImage.Warning) != System.Windows.MessageBoxResult.OK)
        {
            viewModel.StatusMessage = "Import abgebrochen";
            return;
        }

        viewModel.Save();
        await saveCoordinator.FlushAsync(CancellationToken.None);
        saveCoordinator.RequestSave(imported);
        await saveCoordinator.FlushAsync(CancellationToken.None);
        viewModel.ReplaceConfiguration(imported);
        configuration = viewModel.Configuration;
        if (System.Windows.Application.Current is App application)
        {
            application.ApplyTheme(configuration.Settings.ThemeMode);
        }

        if (startupService.IsEnabled != configuration.Settings.StartWithWindows)
        {
            startupService.SetEnabled(configuration.Settings.StartWithWindows);
        }

        Reconfigure();
        viewModel.StatusMessage = $"Vollbackup importiert: {Path.GetFileName(filePath)}";
    }

    private void SaveFinished(Exception? exception)
    {
        _ = window.Dispatcher.InvokeAsync(() =>
        {
            if (exception is null)
            {
                viewModel.StatusMessage = "✓ Gespeichert";
                return;
            }

            log("ERROR", "Konfiguration konnte nicht gespeichert werden.", exception);
            viewModel.StatusMessage = $"Speichern fehlgeschlagen: {exception.Message}";
        });
    }

    public void Reconfigure()
    {
        lock (lifecycleGate)
        {
            ReconfigureCore();
        }
    }

    private void ReconfigureCore()
    {
        if (disposed || Volatile.Read(ref exitRequested) != 0)
        {
            moveHook.Disable();
            placementEngine.Stop();
            return;
        }

        configuration = viewModel.Configuration;
        cursorTimer.Stop();
        coordinator?.Cancel();
        coordinator?.Dispose();
        coordinator = null;
        moveHook.Disable();
        placementEngine.Stop();
        var active = configuration.Profiles.Single(profile => profile.Id == configuration.Settings.ActiveProfileId);
        var targets = BuildTargets(active);
        overlays.UpdateTargets(targets);
        coordinator = dragCoordinatorFactory.Create(
            targets,
            configuration.Settings.OverlayScope);
        coordinator.ActionRequested += HandleDragAction;

        var hotkeyResult = hotkeys.Configure(
            QuickSlotRegistrationPlan.Build(configuration),
            configuration.Settings.SnappingEnabled);
        if (hotkeyResult.Errors.Count > 0)
        {
            viewModel.StatusMessage = string.Join(" ", hotkeyResult.Errors);
        }

        if (placementsInitialized &&
            configuration.Settings.SnappingEnabled &&
            Volatile.Read(ref safetyStopped) == 0)
        {
            try
            {
                moveHook.Enable();
            }
            catch (Exception exception)
            {
                EmergencyStop($"Hook-Aktivierung fehlgeschlagen: {exception.Message}");
                return;
            }
        }

        if (placementsInitialized &&
            configuration.Settings.RestoreWindowPlacementEnabled &&
            Volatile.Read(ref safetyStopped) == 0)
        {
            try
            {
                placementEngine.Start();
            }
            catch (Exception exception)
            {
                EmergencyStop($"Fensterplatzierung konnte nicht aktiviert werden: {exception.Message}");
                return;
            }
        }
        else
        {
            placementEngine.Stop();
        }

        tray.Update(configuration);
    }

    private IReadOnlyList<DragMonitorTarget> BuildTargets(LayoutProfile activeProfile)
    {
        var result = new List<DragMonitorTarget>();
        foreach (var monitor in monitors)
        {
            var layout = activeProfile.Monitors.FirstOrDefault(saved =>
                string.Equals(saved.Monitor.StableId, monitor.Identity.StableId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(saved.Monitor.DeviceName, monitor.Identity.DeviceName, StringComparison.OrdinalIgnoreCase));
            var zones = layout?.Zones ?? [new ZoneDefinition(Guid.NewGuid(), "Voll", NormalizedRect.Full)];
            result.Add(new DragMonitorTarget(monitor, zones));
        }

        return result;
    }

    private void MoveStarted(nint windowHandle)
    {
        if (coordinator is null || !windowService.TryGetCursorPosition(out var cursor))
        {
            log("DEBUG", "Verschiebestart verworfen: Koordinator oder Cursor fehlt.", null);
            return;
        }

        if (configuration.Settings.TriggerMode == TriggerMode.ShiftKey && !windowService.IsShiftPressed())
        {
            return;
        }

        var snapshot = windowService.Inspect(windowHandle, cursor, Environment.ProcessId);
        if (snapshot is null)
        {
            log("DEBUG", $"Verschiebestart verworfen: Fenster 0x{windowHandle:X} ist nicht lesbar.", null);
            return;
        }

        log("DEBUG", $"Verschiebestart hwnd=0x{windowHandle:X} cursor={cursor.X},{cursor.Y} status={snapshot}", null);
        coordinator.Start(windowHandle, snapshot, cursor);
        log("DEBUG", $"Koordinatorstatus nach Start: {coordinator.State}", null);
        if (coordinator.State == DragState.Tracking)
        {
            cursorTimer.Start();
        }
    }

    private void MoveEnded()
    {
        cursorTimer.Stop();
        log("DEBUG", $"Verschiebeende bei Koordinatorstatus {coordinator?.State}.", null);
        if (windowService.TryGetCursorPosition(out var cursor))
        {
            coordinator?.End(cursor);
        }
        else
        {
            coordinator?.End();
        }
    }

    private void MoveHook_MoveEnded(nint windowHandle)
    {
        _ = windowHandle;
        MoveEnded();
    }

    private void MoveHook_EmergencyStopped(string reason) => EmergencyStop(reason);

    private void Hotkeys_EmergencyStopRequested() =>
        EmergencyStop("Not-Aus ausgelöst: Automatik deaktiviert");

    private void PlacementLifecycleHook_EmergencyStopped(string reason) =>
        EmergencyStop($"Fensterplatzierung gestoppt: {reason}");

    private void CursorTimer_Tick(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        if (windowService.IsEscapePressed())
        {
            cursorTimer.Stop();
            coordinator?.Cancel();
        }
        else if (windowService.TryGetCursorPosition(out var cursor))
        {
            coordinator?.Update(cursor);
        }
    }

    private void HandleDragAction(SnapZones.Core.Drag.DragAction action)
    {
        switch (action)
        {
            case ShowOverlaysAction show:
                overlays.Show(
                    show.MonitorIds,
                    new LayoutMetrics(configuration.Settings.EffectiveOuterMargins, configuration.Settings.ZoneGap),
                    configuration.Settings.OverlayColor,
                    configuration.Settings.OverlayOpacity,
                    configuration.Settings.ShowZoneNames);
                break;
            case HighlightZoneAction highlight:
                overlays.Highlight(highlight.MonitorId, highlight.ZoneId);
                break;
            case HideOverlaysAction:
                overlays.HideAll();
                break;
            case SnapWindowAction snap:
                lock (automationGate)
                {
                    if (disposed ||
                        Volatile.Read(ref exitRequested) != 0 ||
                        Volatile.Read(ref safetyStopped) != 0 ||
                        !placementsInitialized ||
                        !configuration.Settings.SnappingEnabled)
                    {
                        log("DEBUG", "Fensteraktion bei deaktivierter Automatik verworfen.", null);
                    }
                    else if (windowService.TrySnap(snap.WindowHandle, snap.Bounds))
                    {
                        placementEngine.RememberExplicitZone(
                            snap.WindowHandle,
                            configuration.Settings.ActiveProfileId,
                            snap.MonitorStableId,
                            snap.ZoneId);
                        log("DEBUG", $"Fenster 0x{snap.WindowHandle:X} eingerastet: {snap.Bounds}.", null);
                    }
                    else
                    {
                        log("WARN", "Ein Fenster konnte nicht positioniert werden.", null);
                    }
                }
                break;
        }
    }

    private void ActivateProfile(Guid profileId)
    {
        viewModel.ActivateProfile(profileId);
        toast.ShowProfile(viewModel.SelectedProfile.Name);
    }

    private void ToggleSnapping(bool enabled)
    {
        viewModel.Settings.SnappingEnabled = enabled;
    }

    private void Window_Closing(object? sender, CancelEventArgs eventArgs)
    {
        _ = sender;
        if (!allowClose)
        {
            eventArgs.Cancel = true;
            window.Hide();
            viewModel.StatusMessage = "Sascha Window Zones läuft im Infobereich weiter";
        }
    }

    private async Task ExitApplicationAsync()
    {
        window.IsEnabled = false;
        try
        {
            lock (lifecycleGate)
            {
                moveHook.Disable();
                placementEngine.Stop();
            }

            viewModel.Save();
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await FlushAsync(cancellation.Token);
        }
        catch (Exception exception)
        {
            log("ERROR", "Die Applikation wird ohne abschliessend bestätigte Speicherung beendet.", exception);
        }

        allowClose = true;
        shutdownApplication();
    }
}
