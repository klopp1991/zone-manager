using System.ComponentModel;
using System.IO;
using System.Windows.Threading;
using SnapZones.App.Overlays;
using SnapZones.App.ViewModels;
using SnapZones.App.Views;
using SnapZones.Core.Drag;
using SnapZones.Core.Geometry;
using SnapZones.Core.AppRules;
using SnapZones.Core.Layouts;
using SnapZones.Core.Models;
using SnapZones.Core.Monitors;
using SnapZones.Core.PartMonitors;
using SnapZones.Core.Persistence;
using SnapZones.Core.Placement;
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
    private readonly FileLog log;
    private readonly IWindowMoveHook moveHook;
    private readonly IWindowRuleHook appRuleHook;
    private readonly IWindowLifecycleHook placementHook;
    private readonly AppRuleCoordinator appRuleCoordinator;
    private readonly WindowPlacementSaveCoordinator placementSaveCoordinator;
    private readonly WindowPlacementEngine placementEngine;
    private readonly IGlobalHotkeyService hotkeys;
    private readonly IWindowService windowService;
    private readonly OverlayManager overlays;
    private readonly MonitorIdentificationOverlay monitorIdentification;
    private readonly TrayIconService tray;
    private readonly LayoutChangedToast toast = new();
    private readonly DispatcherTimer cursorTimer;
    private readonly DispatcherTimer identificationTimer;
    private readonly ConfigurationSaveCoordinator saveCoordinator;
    private readonly ExitSaveCoordinator exitSaveCoordinator;
    private readonly ConfigurationTransferService transferService = new();
    private readonly PlacementHistory placementHistory = new();
    private readonly ExitRequestGate exitRequestGate = new();
    private PartMonitorCommandService? partMonitorCommands;
    private WindowDragCoordinator? coordinator;
    private SnapConfiguration configuration;
    private bool emergencyStopped;
    private bool allowClose;
    private bool disposed;

    public ApplicationController(
        MainWindow window,
        MainViewModel viewModel,
        IConfigurationRepository repository,
        IWindowPlacementRepository placementRepository,
        WindowPlacementCatalog initialPlacementCatalog,
        IReadOnlyList<LiveMonitor> monitors,
        IStartupService startupService,
        FileLog log)
    {
        this.window = window;
        this.viewModel = viewModel;
        this.monitors = monitors;
        this.startupService = startupService;
        this.log = log;
        saveCoordinator = new ConfigurationSaveCoordinator(repository);
        exitSaveCoordinator = new ExitSaveCoordinator(saveCoordinator);
        saveCoordinator.SaveFinished += SaveFinished;
        placementSaveCoordinator = new WindowPlacementSaveCoordinator(
            placementRepository,
            TimeSpan.FromMilliseconds(500));
        placementSaveCoordinator.SaveFinished += PlacementSaveFinished;
        configuration = viewModel.Configuration;
        overlays = new OverlayManager();
        monitorIdentification = new MonitorIdentificationOverlay();
        windowService = new WindowsWindowService();
        var synchronizationContext = SynchronizationContext.Current
            ?? new DispatcherSynchronizationContext(window.Dispatcher);
        moveHook = new WindowMoveHook(
            synchronizationContext,
            message => log.Write("DEBUG", message));
        appRuleHook = new WindowRuleHook(synchronizationContext);
        placementHook = new WindowLifecycleHook(synchronizationContext);
        placementEngine = new WindowPlacementEngine(
            placementHook,
            new WindowsPlacementWindowService(),
            placementSaveCoordinator,
            initialPlacementCatalog,
            () => BuildPlacementEnvironment(configuration),
            Environment.ProcessId,
            message => log.Write("DEBUG", message));
        appRuleCoordinator = new AppRuleCoordinator(
            () => configuration,
            monitors,
            new WindowServiceAppRuleGateway(windowService, Environment.ProcessId),
            reportStatus: message => _ = window.Dispatcher.InvokeAsync(() => viewModel.StatusMessage = message));
        hotkeys = new GlobalHotkeyService();
        cursorTimer = new DispatcherTimer(DispatcherPriority.Input, window.Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(33)
        };
        cursorTimer.Tick += CursorTimer_Tick;
        identificationTimer = new DispatcherTimer(DispatcherPriority.Normal, window.Dispatcher)
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        identificationTimer.Tick += IdentificationTimer_Tick;
        tray = new TrayIconService(window, ActivateLayout, RequestExit);

        viewModel.SaveRequested += SaveRequested;
        window.ExportConfigurationRequested += ExportConfigurationAsync;
        window.ImportConfigurationRequested += ImportConfigurationAsync;
        window.IdentifyMonitorsRequested += IdentifyMonitors;
        moveHook.MoveStarted += MoveStarted;
        moveHook.MoveEnded += _ => MoveEnded();
        moveHook.EmergencyStopped += reason => EmergencyStop(reason);
        appRuleHook.RuleEvent += AppRuleHook_RuleEvent;
        appRuleHook.EmergencyStopped += reason => EmergencyStop($"App-Regel-Hook gestoppt: {reason}");
        placementHook.EmergencyStopped += reason => EmergencyStop($"Fensterplatzierungs-Hook gestoppt: {reason}");
        hotkeys.EmergencyStopRequested += () => EmergencyStop("Not-Aus ausgelöst: Snap-Funktion deaktiviert");
        window.Closing += Window_Closing;

        Reconfigure(configuration);
    }

    public void EmergencyStop(string reason)
    {
        emergencyStopped = true;
        cursorTimer.Stop();
        identificationTimer.Stop();
        coordinator?.Cancel();
        overlays.HideAll();
        monitorIdentification.HideAll();
        moveHook.Disable();
        appRuleCoordinator.CancelPending();
        appRuleHook.Disable();
        placementEngine.EmergencyStop();
        viewModel.StatusMessage = reason;
        configuration = viewModel.Configuration;
        _ = hotkeys.Configure(emergencyStopEnabled: false);
        tray.Update(configuration);
        log.Write("WARN", reason);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        allowClose = true;
        cursorTimer.Stop();
        identificationTimer.Stop();
        moveHook.Disable();
        appRuleCoordinator.CancelPending();
        appRuleHook.Disable();
        overlays.HideAll();
        tray.Dispose();
        hotkeys.Dispose();
        moveHook.Dispose();
        appRuleCoordinator.Dispose();
        appRuleHook.Dispose();
        placementEngine.Stop();
        placementHook.Dispose();
        overlays.Dispose();
        monitorIdentification.Dispose();
        toast.Close();
        saveCoordinator.SaveFinished -= SaveFinished;
        placementSaveCoordinator.SaveFinished -= PlacementSaveFinished;
        window.ExportConfigurationRequested -= ExportConfigurationAsync;
        window.ImportConfigurationRequested -= ImportConfigurationAsync;
        window.IdentifyMonitorsRequested -= IdentifyMonitors;
    }

    private void SaveRequested(SnapConfiguration newConfiguration)
    {
        var previousConfiguration = configuration;
        configuration = newConfiguration;
        var activatedLayoutIds = FindNewlyActivatedLayoutIds(previousConfiguration, newConfiguration);
        ReflowWindowsForChangedActiveLayouts(previousConfiguration, newConfiguration);
        Reconfigure(configuration);
        foreach (var layoutId in activatedLayoutIds)
        {
            _ = ApplyLayoutRulesAsync(layoutId);
        }
        try
        {
            if (startupService.IsEnabled != newConfiguration.Settings.StartWithWindows)
            {
                startupService.SetEnabled(newConfiguration.Settings.StartWithWindows);
            }
        }
        catch (Exception exception)
        {
            log.Write("ERROR", "Die Autostart-Einstellung konnte nicht übernommen werden.", exception);
            viewModel.StatusMessage = $"Autostart konnte nicht geändert werden: {exception.Message}";
        }

        saveCoordinator.RequestSave(newConfiguration);
    }

    private void ReflowWindowsForChangedActiveLayouts(
        SnapConfiguration previousConfiguration,
        SnapConfiguration newConfiguration)
    {
        try
        {
            var windows = windowService.GetMovableTopLevelWindows(Environment.ProcessId);
            foreach (var monitor in monitors)
            {
                var oldLayout = previousConfiguration.Layouts.FirstOrDefault(layout =>
                    layout.IsActive && LayoutService.BelongsToMonitor(layout.Monitor, monitor.Identity));
                var newLayout = newConfiguration.Layouts.FirstOrDefault(layout =>
                    layout.IsActive && layout.Id == oldLayout?.Id);
                if (oldLayout is null || newLayout is null)
                {
                    continue;
                }

                foreach (var target in LayoutWindowReflow.Plan(
                    oldLayout, newLayout, monitor.WorkArea, windows))
                {
                    if (windowService.TrySnap(target.WindowHandle, target.Bounds))
                    {
                        log.Write("DEBUG", $"Fenster 0x{target.WindowHandle:X} an geänderte Zone angepasst: {target.Bounds}.");
                    }
                    else
                    {
                        log.Write("WARN", $"Fenster 0x{target.WindowHandle:X} konnte nicht an die geänderte Zone angepasst werden.");
                    }
                }
            }
        }
        catch (Exception exception)
        {
            log.Write("WARN", "Fenster konnten nach einer Layoutänderung nicht vollständig angepasst werden.", exception);
        }
    }

    public async Task FlushAsync(CancellationToken cancellationToken)
    {
        await saveCoordinator.FlushAsync(cancellationToken);
        await placementEngine.FlushAsync(cancellationToken);
    }

    public void RequestExit()
    {
        exitRequestGate.Request(() =>
        {
            _ = window.Dispatcher.InvokeAsync(
                new Action(() => _ = ExitApplicationAsync()),
                DispatcherPriority.Send);
        });
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
        var monitorCount = imported.Layouts
            .Select(layout => string.IsNullOrWhiteSpace(layout.Monitor.StableId)
                ? layout.Monitor.DeviceName
                : layout.Monitor.StableId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Count();
        var zoneCount = imported.Layouts.Sum(layout => layout.Zones.Count);
        var impact =
            $"Der Import ersetzt sämtliche aktuellen Einstellungen und Layouts.\n\n" +
            $"Importdatei: {monitorCount} Monitore, {imported.Layouts.Count} Layouts, {zoneCount} Zonen.\n" +
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

        Reconfigure(configuration);
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

            log.Write("ERROR", "Konfiguration konnte nicht gespeichert werden.", exception);
            viewModel.StatusMessage = $"Speichern fehlgeschlagen: {exception.Message}";
        });
    }

    private void PlacementSaveFinished(Exception? exception)
    {
        if (exception is not null)
        {
            log.Write("ERROR", "Fensterplatzierungen konnten nicht gespeichert werden.", exception);
        }
    }

    private void Reconfigure(SnapConfiguration newConfiguration)
    {
        cursorTimer.Stop();
        coordinator?.Cancel();
        moveHook.Disable();
        appRuleCoordinator.CancelPending();
        appRuleHook.Disable();
        placementEngine.Stop();
        var targets = BuildTargets(newConfiguration);
        overlays.UpdateTargets(targets);
        var metrics = new LayoutMetrics(
            newConfiguration.Settings.EffectiveOuterMargins,
            newConfiguration.Settings.ZoneGap);
        partMonitorCommands = new PartMonitorCommandService(
            new PartMonitorResolver(targets, metrics),
            placementHistory,
            windowService);
        coordinator = new WindowDragCoordinator(
            targets,
            metrics,
            newConfiguration.Settings.OverlayScope);
        coordinator.ActionRequested += HandleDragAction;

        var snappingEnabled = SnapActivationPolicy.ShouldEnable(newConfiguration) && !emergencyStopped;
        var hotkeyResult = hotkeys.Configure(snappingEnabled);
        if (hotkeyResult.Errors.Count > 0)
        {
            viewModel.StatusMessage = string.Join(" ", hotkeyResult.Errors);
        }

        if (snappingEnabled)
        {
            try
            {
                moveHook.Enable();
                placementEngine.Start();
            }
            catch (Exception exception)
            {
                EmergencyStop($"Hook-Aktivierung fehlgeschlagen: {exception.Message}");
                return;
            }
        }

        if (!emergencyStopped && newConfiguration.AppRules.Any(rule => rule.IsEnabled))
        {
            try
            {
                appRuleHook.Enable();
            }
            catch (Exception exception)
            {
                EmergencyStop($"App-Regel-Hook konnte nicht aktiviert werden: {exception.Message}");
                return;
            }
        }

        tray.Update(newConfiguration);
    }

    private void AppRuleHook_RuleEvent(AppRuleEvent eventType, nint windowHandle) =>
        _ = ApplyWindowRuleAsync(eventType, windowHandle);

    private async Task ApplyWindowRuleAsync(AppRuleEvent eventType, nint windowHandle)
    {
        try
        {
            await appRuleCoordinator.HandleAsync(eventType, windowHandle);
        }
        catch (Exception exception)
        {
            log.Write("ERROR", "Eine App-Regel konnte nicht ausgeführt werden.", exception);
            viewModel.StatusMessage = $"App-Regel fehlgeschlagen: {exception.Message}";
        }
    }

    private async Task ApplyLayoutRulesAsync(Guid layoutId)
    {
        try
        {
            await appRuleCoordinator.HandleLayoutActivatedAsync(layoutId);
        }
        catch (Exception exception)
        {
            log.Write("ERROR", "Die App-Regeln des aktivierten Layouts konnten nicht ausgeführt werden.", exception);
            viewModel.StatusMessage = $"Layout-Regeln fehlgeschlagen: {exception.Message}";
        }
    }

    public static IReadOnlyList<Guid> FindNewlyActivatedLayoutIds(
        SnapConfiguration previousConfiguration,
        SnapConfiguration newConfiguration) =>
        newConfiguration.Layouts
            .Where(layout => layout.IsActive &&
                previousConfiguration.Layouts.FirstOrDefault(previous => previous.Id == layout.Id)?.IsActive != true)
            .Select(layout => layout.Id)
            .ToArray();

    private void IdentifyMonitors()
    {
        identificationTimer.Stop();
        monitorIdentification.Show(viewModel.Monitors.Select(monitor =>
            new MonitorIdentificationTarget(monitor.Live, monitor.UserFacingName)));
        identificationTimer.Start();
        viewModel.StatusMessage = "Monitorbezeichnungen werden angezeigt";
    }

    private void IdentificationTimer_Tick(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        identificationTimer.Stop();
        monitorIdentification.HideAll();
    }

    private IReadOnlyList<PartMonitorTarget> BuildTargets(SnapConfiguration currentConfiguration)
    {
        var result = new List<PartMonitorTarget>();
        foreach (var monitor in monitors)
        {
            var layout = currentConfiguration.Layouts.FirstOrDefault(saved =>
                saved.IsActive && LayoutService.BelongsToMonitor(saved.Monitor, monitor.Identity));
            var zones = layout?.Zones ?? [new ZoneDefinition(Guid.NewGuid(), "Voll", NormalizedRect.Full)];
            result.Add(new PartMonitorTarget(monitor, zones));
        }

        return result;
    }

    private PlacementEnvironment BuildPlacementEnvironment(SnapConfiguration currentConfiguration)
    {
        var placementMonitors = monitors
            .Select(monitor => new PlacementMonitorTarget(
                string.IsNullOrWhiteSpace(monitor.Identity.StableId)
                    ? monitor.Identity.DeviceName
                    : monitor.Identity.StableId,
                monitor.WorkArea,
                monitor.IsPrimary))
            .ToArray();
        var placementZones = currentConfiguration.Layouts
            .Where(layout => layout.IsActive)
            .SelectMany(layout => monitors
                .Where(monitor => LayoutService.BelongsToMonitor(monitor.Identity, layout.Monitor))
                .SelectMany(monitor => layout.Zones.Select(zone => new PlacementZoneTarget(
                    layout.Id,
                    zone.Id,
                    string.IsNullOrWhiteSpace(monitor.Identity.StableId)
                        ? monitor.Identity.DeviceName
                        : monitor.Identity.StableId,
                    ZoneGeometry.ToPixels(zone.Bounds, monitor.WorkArea)))))
            .ToArray();
        return new PlacementEnvironment(currentConfiguration, placementMonitors, placementZones);
    }

    private void MoveStarted(nint windowHandle)
    {
        if (coordinator is null || !windowService.TryGetCursorPosition(out var cursor))
        {
            log.Write("DEBUG", "Verschiebestart verworfen: Koordinator oder Cursor fehlt.");
            return;
        }

        if (configuration.Settings.TriggerMode == TriggerMode.ShiftKey && !windowService.IsShiftPressed())
        {
            return;
        }

        var snapshot = windowService.Inspect(windowHandle, cursor, Environment.ProcessId);
        if (snapshot is null)
        {
            log.Write("DEBUG", $"Verschiebestart verworfen: Fenster 0x{windowHandle:X} ist nicht lesbar.");
            return;
        }

        log.Write("DEBUG", $"Verschiebestart hwnd=0x{windowHandle:X} cursor={cursor.X},{cursor.Y} status={snapshot}");
        coordinator.Start(windowHandle, snapshot, cursor);
        log.Write("DEBUG", $"Koordinatorstatus nach Start: {coordinator.State}");
        if (coordinator.State == DragState.Tracking)
        {
            cursorTimer.Start();
        }
    }

    private void MoveEnded()
    {
        cursorTimer.Stop();
        log.Write("DEBUG", $"Verschiebeende bei Koordinatorstatus {coordinator?.State}.");
        if (windowService.TryGetCursorPosition(out var cursor))
        {
            coordinator?.End(cursor);
        }
        else
        {
            coordinator?.End();
        }
    }

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
            case FillPartMonitorAction fill:
                var result = partMonitorCommands?.Execute(new FillPartMonitorCommand(
                    fill.WindowHandle,
                    fill.MonitorId,
                    fill.PartMonitorId));
                if (result?.Status == PartMonitorCommandStatus.Successful)
                {
                    log.Write("DEBUG", "Fenster wurde in einen Teilmonitor eingerastet.");
                }
                else
                {
                    log.Write(
                        "WARN",
                        "Teilmonitor-Platzierung abgelehnt: " +
                        (result?.Status.ToString() ?? "Komponente nicht bereit"));
                }
                break;
        }
    }

    private void ActivateLayout(Guid layoutId)
    {
        var layout = configuration.Layouts.First(candidate => candidate.Id == layoutId);
        viewModel.ActivateLayout(layoutId);
        toast.ShowLayout($"{viewModel.GetMonitorDisplayName(layout.Monitor)}: {layout.Name}");
    }

    private void Window_Closing(object? sender, CancelEventArgs eventArgs)
    {
        _ = sender;
        if (!allowClose)
        {
            eventArgs.Cancel = true;
            window.Hide();
            viewModel.StatusMessage = "Sascha’s Zone Manager läuft im Infobereich weiter";
        }
    }

    private async Task ExitApplicationAsync()
    {
        window.IsEnabled = false;
        try
        {
            await exitSaveCoordinator.PrepareForShutdownAsync(viewModel.Save);
            await placementEngine.FlushAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            log.Write("ERROR", "Die Applikation bleibt geöffnet, weil Einstellungen oder Fensterplatzierungen nicht gespeichert werden konnten.", exception);
            window.IsEnabled = true;
            viewModel.StatusMessage = "Beenden abgebrochen: Einstellungen konnten nicht gespeichert werden.";
            exitRequestGate.Reset();
            return;
        }

        allowClose = true;
        System.Windows.Application.Current.Shutdown();
    }
}
