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
using SnapZones.Core.Updates;
using SnapZones.Core.Setup;
using SnapZones.Windows.Hooks;
using SnapZones.Windows.Hotkeys;
using SnapZones.Windows.Startup;
using SnapZones.Windows.Setup;
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
    private bool shuttingDown;
    private readonly UpdateCoordinator updates;
    private readonly ElevationEscalation elevation;
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

        elevation = new ElevationEscalation(question => System.Windows.MessageBox.Show(
            question,
            "Administratorrechte erforderlich",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question) == System.Windows.MessageBoxResult.Yes);
        updates = new UpdateCoordinator(
            () => viewModel.ProductVersion,
            () => Environment.ProcessPath,
            log.Write);
        viewModel.SaveRequested += SaveRequested;
        viewModel.ForgetWindowPositionsRequested += ForgetWindowPositions;
        viewModel.UpdateCheckRequested += () => _ = CheckForUpdatesAsync(announceUpToDate: true);
        viewModel.UpdateInstallRequested += () => _ = InstallUpdateAsync();
        viewModel.InstallRequested += InstallToProgramFiles;
        PublishInstallationStatus();
        placementEngine.CatalogChanged += PublishRememberedWindowCount;
        viewModel.RememberedWindowCount = placementEngine.Catalog.Entries.Count;
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

        // Eine vom letzten Update beiseitegeschobene Programmdatei laesst sich erst loeschen, wenn der
        // Prozess, der sie belegte, geendet hat. Der naechste Start ist der erste Zeitpunkt dafuer.
        if (Environment.ProcessPath is { Length: > 0 } processPath)
        {
            var removed = UpdateInstaller.RemoveSupersededFiles(processPath);
            if (removed > 0)
            {
                log.Write("INFO", $"{removed} Vorgaengerdatei(en) nach einem Update entfernt.");
            }
        }

        if (configuration.Settings.CheckForUpdatesOnStart)
        {
            _ = CheckForUpdatesAsync(announceUpToDate: false);
        }
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
        // Waehrend des Beendens nur noch persistieren. Frueher lief hier der volle Reconfigure-Pfad,
        // der Hooks und Platzierungs-Engine direkt vor dem abschliessenden Flush wieder scharf schaltete.
        if (shuttingDown)
        {
            configuration = newConfiguration;
            saveCoordinator.RequestSave(newConfiguration);
            return;
        }

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
            $"Der Import ersetzt sämtliche aktuellen Einstellungen, Layouts, Regeln und Ausschlüsse.\n\n" +
            $"Importdatei: {monitorCount} Monitore, {imported.Layouts.Count} Layouts, {zoneCount} Zonen, " +
            $"{imported.AppRules.Count} Regeln, {imported.AppExclusions.Count} Ausschlüsse.\n" +
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

    /// <summary>
    /// Sieht nach einer neueren Veroeffentlichung. Beim Start geschieht das still: nur ein gefundenes
    /// Update wird gemeldet, ein erfolgloser Blick bleibt im Protokoll.
    /// </summary>
    private async Task CheckForUpdatesAsync(bool announceUpToDate)
    {
        if (viewModel.IsUpdateBusy)
        {
            return;
        }

        viewModel.IsUpdateBusy = true;
        viewModel.UpdateStatus = "Suche nach Updates \u2026";
        try
        {
            var result = await updates.CheckAsync(CancellationToken.None);
            viewModel.IsUpdateAvailable = result.Availability == UpdateAvailability.UpdateAvailable;
            if (result.Availability != UpdateAvailability.UpToDate || announceUpToDate)
            {
                viewModel.UpdateStatus = result.Message;
            }
        }
        finally
        {
            viewModel.IsUpdateBusy = false;
        }
    }

    private async Task InstallUpdateAsync()
    {
        if (viewModel.IsUpdateBusy || !viewModel.IsUpdateAvailable)
        {
            return;
        }

        viewModel.IsUpdateBusy = true;
        viewModel.UpdateStatus = "Update wird geladen \u2026";
        try
        {
            var result = await updates.InstallAsync(CancellationToken.None);
            viewModel.UpdateStatus = result.Message;
            if (result.Status != UpdateInstallStatus.Applied)
            {
                return;
            }

            viewModel.IsUpdateAvailable = false;
        }
        finally
        {
            viewModel.IsUpdateBusy = false;
        }

        // Erst speichern, dann den neuen Stand starten, dann selbst enden. Zwei Staende duerfen nie
        // gleichzeitig laufen.
        viewModel.Save();
        await saveCoordinator.FlushAsync(CancellationToken.None);
        await placementEngine.FlushAsync(CancellationToken.None);
        if (updates.TryRestart())
        {
            RequestExit();
            return;
        }

        viewModel.StatusMessage =
            "Die neue Version liegt bereit, liess sich aber nicht starten. Beim naechsten Start wird sie verwendet.";
    }

    /// <summary>
    /// Prueft nach einer abgelehnten Platzierung, ob das Fenster einem hoeher berechtigten Programm
    /// gehoert, und bietet in diesem Fall einmalig den erhoehten Neustart an. Andere Gruende — ein
    /// inzwischen geschlossenes Fenster, eine feste Mindestgroesse — fuehren zu keiner Nachfrage.
    /// </summary>
    private void OfferElevationIfWindowIsOutOfReach(nint windowHandle)
    {
        if (elevation.HasOffered ||
            configuration.Settings.ElevationMode == ElevationMode.Always ||
            !windowService.RequiresElevation(windowHandle))
        {
            return;
        }

        var result = elevation.Offer(
            Environment.ProcessPath ?? string.Empty,
            Environment.GetCommandLineArgs().Skip(1).ToArray());
        switch (result.Status)
        {
            case ElevationEscalationStatus.Restarting:
                viewModel.Save();
                RequestExit();
                break;
            case ElevationEscalationStatus.Declined:
                viewModel.StatusMessage =
                    "Fenster hoeher berechtigter Programme bleiben unberuehrt. "
                        + "Unter Einstellungen laesst sich das aendern.";
                break;
            case ElevationEscalationStatus.Failed:
                viewModel.StatusMessage = result.Message ?? "Der erhoehte Neustart ist fehlgeschlagen.";
                log.Write("WARN", result.Message ?? "Der erhoehte Neustart ist fehlgeschlagen.");
                break;
            default:
                break;
        }
    }

    private void PublishInstallationStatus()
    {
        var installed = InstallationService.InstalledPath;
        var plan = InstallationService.CreatePlan(Environment.ProcessPath ?? string.Empty);
        viewModel.CanInstall = plan.State != InstallationState.AlreadyInstalled;
        viewModel.InstallationStatus = plan.State switch
        {
            InstallationState.AlreadyInstalled =>
                $"Installiert unter {plan.TargetDirectory}.",
            InstallationState.UpgradeInPlace =>
                $"Es liegt bereits eine Installation unter {plan.TargetDirectory}; sie wird ersetzt. "
                    + $"Dieser Stand läuft aus {System.IO.Path.GetDirectoryName(plan.SourcePath)}.",
            _ when installed is { Length: > 0 } =>
                $"Registriert ist eine Installation unter {installed}, dieser Stand läuft aber aus "
                    + $"{System.IO.Path.GetDirectoryName(plan.SourcePath)}.",
            _ =>
                $"Nicht installiert. Dieser Stand läuft aus {System.IO.Path.GetDirectoryName(plan.SourcePath)}."
        };
    }

    private void InstallToProgramFiles()
    {
        var (exitCode, message, startPath) = SetupRunner.Run(
            SetupRunner.Mode.Install,
            Environment.ProcessPath ?? string.Empty,
            viewModel.ProductVersion,
            new InstallationService());
        viewModel.StatusMessage = message;
        log.Write(exitCode == 0 ? "INFO" : "ERROR", message);
        PublishInstallationStatus();

        if (exitCode != 0 || startPath is not { Length: > 0 })
        {
            return;
        }

        // Der installierte Stand uebernimmt; zwei Staende duerfen nie gleichzeitig laufen.
        viewModel.Save();
        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = startPath,
                WorkingDirectory = System.IO.Path.GetDirectoryName(startPath) ?? AppContext.BaseDirectory,
                UseShellExecute = true
            });
            if (process is not null)
            {
                RequestExit();
            }
        }
        catch (Exception exception)
        {
            log.Write("WARN", "Der Start aus dem Installationsverzeichnis ist fehlgeschlagen.", exception);
        }
    }

    private void ForgetWindowPositions()
    {
        placementEngine.ForgetAll();
        viewModel.StatusMessage = "Gemerkte Fensterpositionen verworfen";
    }

    private void PublishRememberedWindowCount(WindowPlacementCatalog catalog) =>
        _ = window.Dispatcher.InvokeAsync(() => viewModel.RememberedWindowCount = catalog.Entries.Count);

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
            newConfiguration.Settings.OverlayScope,
            newConfiguration.AppExclusions);
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
            coordinator?.End(cursor, windowService.IsControlPressed());
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
            coordinator?.Update(cursor, windowService.IsControlPressed());
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
            case HighlightZoneSpanAction span:
                overlays.Highlight(span.MonitorId, span.ZoneIds);
                break;
            case FillPartMonitorSpanAction fillSpan:
                var spanResult = partMonitorCommands?.Execute(new FillPartMonitorSpanCommand(
                    fillSpan.WindowHandle,
                    fillSpan.MonitorId,
                    fillSpan.PartMonitorIds));
                if (spanResult?.Status == PartMonitorCommandStatus.Successful)
                {
                    log.Write("DEBUG", $"Fenster wurde über {fillSpan.PartMonitorIds.Count} Zonen gelegt.");
                }
                else
                {
                    log.Write(
                        "WARN",
                        "Zonenübergreifende Platzierung abgelehnt: " +
                        (spanResult?.Status.ToString() ?? "Komponente nicht bereit"));
                    OfferElevationIfWindowIsOutOfReach(fillSpan.WindowHandle);
                }

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
                    OfferElevationIfWindowIsOutOfReach(fill.WindowHandle);
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

    /// <summary>
    /// Obergrenze für das Speichern beim Beenden. Ohne Grenze konnte der Vorgang unbegrenzt warten,
    /// und die Anwendung liess sich nicht mehr schliessen.
    /// </summary>
    private static readonly TimeSpan ShutdownSaveTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Legt alle Quellen neuer Arbeit still. Ohne diesen Schritt lieferten Hooks, Zeitgeber und die
    /// Platzierungs-Engine während des Beendens laufend neue Aufträge nach, sodass der abschliessende
    /// Flush nie zur Ruhe kam.
    /// </summary>
    private void QuiesceForShutdown()
    {
        try
        {
            cursorTimer.Stop();
            identificationTimer.Stop();
            coordinator?.Cancel();
            moveHook.Disable();
            appRuleCoordinator.CancelPending();
            appRuleHook.Disable();
            placementEngine.Stop();
            overlays.HideAll();
        }
        catch (Exception exception)
        {
            // Ein Fehler beim Stilllegen darf das Beenden nicht verhindern.
            log.Write("WARN", "Beim Stilllegen der Hooks vor dem Beenden ist ein Fehler aufgetreten.", exception);
        }
    }

    private async Task ExitApplicationAsync()
    {
        shuttingDown = true;
        window.IsEnabled = false;
        QuiesceForShutdown();

        var result = await exitSaveCoordinator.TryPrepareForShutdownAsync(
            viewModel.Save,
            ShutdownSaveTimeout,
            placementEngine.FlushAsync);

        if (!result.IsSaved)
        {
            log.Write(
                "ERROR",
                $"Beim Beenden konnte nicht vollständig gespeichert werden: {result.Describe()}",
                result.Failure);

            // Sichtbar melden statt still im Infobereich weiterzulaufen: der Anwender hat das Beenden
            // ausgelöst und muss erfahren, warum es hakt, und entscheiden können.
            var proceed = System.Windows.MessageBox.Show(
                $"{result.Describe()}\n\nTrotzdem beenden?",
                $"{ProductInfo.Name} beenden",
                System.Windows.MessageBoxButton.OKCancel,
                System.Windows.MessageBoxImage.Warning,
                System.Windows.MessageBoxResult.OK) == System.Windows.MessageBoxResult.OK;
            if (!proceed)
            {
                shuttingDown = false;
                window.IsEnabled = true;
                viewModel.StatusMessage = "Beenden abgebrochen: Einstellungen konnten nicht gespeichert werden.";
                exitRequestGate.Reset();
                Reconfigure(configuration);
                return;
            }
        }

        allowClose = true;
        System.Windows.Application.Current.Shutdown();
    }
}
