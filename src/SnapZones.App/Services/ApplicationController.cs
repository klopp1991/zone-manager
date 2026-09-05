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
using SnapZones.Core.Elevation;
using SnapZones.Windows.Elevation;
using SnapZones.Core.Setup;
using SnapZones.Windows.Displays;
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
    private readonly IMonitorService monitorService;
    private readonly MonitorWatcher monitorWatcher;
    private volatile IReadOnlyList<LiveMonitor> monitors;
    private readonly IStartupService startupService;
    private readonly FileLog log;
    private readonly IWindowMoveHook moveHook;
    private readonly IWindowRuleHook appRuleHook;
    private readonly IWindowLifecycleHook placementHook;
    private readonly AppRuleCoordinator appRuleCoordinator;
    private readonly WindowPlacementSaveCoordinator placementSaveCoordinator;
    private readonly WindowPlacementEngine placementEngine;
    private readonly ZoneFullscreenCoordinator zoneFullscreen;
    private readonly DispatcherTimer zoneFullscreenTimer;
    private readonly IGlobalHotkeyService hotkeys;
    private readonly IWindowService windowService;
    private readonly OverlayManager overlays;
    private readonly OverlayManager previewOverlays = new();
    private readonly WindowsPlacementWindowService placementWindowService;
    private readonly DispatcherTimer overlayDelayTimer;
    private ShowOverlaysAction? pendingOverlayShow;
    private bool dragStartedInZone;
    private bool placedDuringDrag;
    private readonly DispatcherTimer previewTimer;
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
    // volatile: der Platzierungs-Thread liest das Feld ueber BuildPlacementEnvironment, der UI-Thread
    // schreibt es. Ohne Sichtbarkeitsgarantie rechnete die Engine kurzzeitig mit dem alten Zonenbild.
    private volatile SnapConfiguration configuration;
    private bool emergencyStopped;
    private (nint Handle, WindowSnapshot Snapshot)? pendingShiftDrag;
    private nint draggedWindow;
    private WindowSnapshot? draggedSnapshot;
    private DateTimeOffset dragStartedAt;
    private DateTimeOffset? buttonReleasedAt;
    private bool dragStartedWithButton;
    private bool allowClose;
    private bool shuttingDown;
    private readonly UpdateCoordinator updates;
    private readonly ElevationEscalation elevation;
    private readonly SigningCertificateService certificates = new();
    private readonly HelperChannel? helper;
    private readonly string updateStagingDirectory;
    private readonly IReadOnlyList<string> startupArguments;
    private readonly HookRecoveryPolicy hookRecovery = HookRecoveryPolicy.Default;
    private readonly DispatcherTimer hookResumeTimer;
    private readonly object environmentGate = new();
    private PlacementEnvironment? cachedEnvironment;
    private readonly ConfigurationBackupCatalog? backups;
    private SnapConfiguration? cachedEnvironmentConfiguration;
    private IReadOnlyList<LiveMonitor>? cachedEnvironmentMonitors;
    private bool disposed;

    public ApplicationController(
        MainWindow window,
        MainViewModel viewModel,
        IConfigurationRepository repository,
        IWindowPlacementRepository placementRepository,
        WindowPlacementCatalog initialPlacementCatalog,
        IReadOnlyList<LiveMonitor> monitors,
        IMonitorService monitorService,
        IStartupService startupService,
        FileLog log,
        string updateStagingDirectory,
        IReadOnlyList<string> startupArguments,
        ConfigurationBackupCatalog? backupCatalog = null)
    {
        backups = backupCatalog;
        this.window = window;
        this.viewModel = viewModel;
        this.monitors = monitors;
        this.monitorService = monitorService;
        this.startupService = startupService;
        this.log = log;
        this.updateStagingDirectory = updateStagingDirectory ?? throw new ArgumentNullException(nameof(updateStagingDirectory));
        this.startupArguments = startupArguments ?? throw new ArgumentNullException(nameof(startupArguments));
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
        windowService = new WindowsWindowService(message => log.Write("DEBUG", message));
        var synchronizationContext = SynchronizationContext.Current
            ?? new DispatcherSynchronizationContext(window.Dispatcher);
        moveHook = new WindowMoveHook(
            synchronizationContext,
            message => log.Write("DEBUG", message));
        appRuleHook = new WindowRuleHook(synchronizationContext);
        placementHook = new WindowLifecycleHook(synchronizationContext);
        placementWindowService = new WindowsPlacementWindowService(message => log.Write("DEBUG", message));
        placementEngine = new WindowPlacementEngine(
            placementHook,
            placementWindowService,
            placementSaveCoordinator,
            initialPlacementCatalog,
            () => BuildPlacementEnvironment(configuration),
            Environment.ProcessId,
            message => log.Write("DEBUG", message));
        zoneFullscreen = new ZoneFullscreenCoordinator(
            new WindowsFullscreenWindowReader(),
            (handle, bounds) => windowService.Fill(handle, bounds),
            handle => windowService.InspectRuleCandidate(handle, Environment.ProcessId)?.Identity,
            () => BuildPlacementEnvironment(configuration),
            message => log.Write("DEBUG", message),
            notice: (level, message) => log.Write(level, message));
        appRuleCoordinator = new AppRuleCoordinator(
            () => configuration,
            () => this.monitors,
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
        tray = new TrayIconService(window, ActivateLayout, RequestExit, ResumeSnapping);

        elevation = new ElevationEscalation(question => System.Windows.MessageBox.Show(
            question,
            "Administratorrechte erforderlich",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question) == System.Windows.MessageBoxResult.Yes);
        updates = new UpdateCoordinator(
            () => viewModel.ProductVersion,
            () => Environment.ProcessPath,
            () => this.updateStagingDirectory,
            log.Write);
        viewModel.SaveRequested += SaveRequested;
        viewModel.ForgetWindowPositionsRequested += ForgetWindowPositions;
        viewModel.UpdateCheckRequested += () => _ = CheckForUpdatesAsync(announceUpToDate: true);
        viewModel.UpdateInstallRequested += () => _ = InstallUpdateAsync();
        viewModel.InstallRequested += InstallToProgramFiles;
        viewModel.CertificateInstallRequested += InstallSigningCertificate;
        viewModel.CertificateRemoveRequested += RemoveSigningCertificate;
        viewModel.ResumeSnappingRequested += ResumeSnapping;

        // Der Helfer wird nur angelegt, wenn seine Datei ueberhaupt neben dem Programm liegt. Gestartet
        // wird er erst beim ersten Fenster, das ihn braucht.
        var helperPath = HelperChannel.ResolvePath(Environment.ProcessPath ?? string.Empty);
        if (File.Exists(helperPath))
        {
            helper = new HelperChannel(helperPath, log.Write);
            if (windowService is WindowsWindowService concrete)
            {
                concrete.ElevatedPlacement = (handle, bounds) =>
                    helper.TryPlace(handle, bounds.X, bounds.Y, bounds.Width, bounds.Height);
            }
        }

        PublishInstallationStatus();
        PublishCertificateStatus();
        placementEngine.CatalogChanged += PublishRememberedWindowCount;
        viewModel.RememberedWindowCount = placementEngine.Catalog.Entries.Count;
        window.ExportConfigurationRequested += ExportConfigurationAsync;
        window.ImportConfigurationRequested += ImportConfigurationAsync;
        window.IdentifyMonitorsRequested += IdentifyMonitors;
        window.SettingsPageOpened += PublishCertificateStatus;
        viewModel.BackupsRefreshRequested += RefreshBackups;
        viewModel.RestoreBackupRequested += RestoreBackup;
        moveHook.MoveStarted += MoveStarted;
        moveHook.MoveEnded += _ => MoveEnded();
        moveHook.EmergencyStopped += reason => EmergencyStop(reason);
        appRuleHook.RuleEvent += AppRuleHook_RuleEvent;
        appRuleHook.EmergencyStopped += reason => EmergencyStop($"App-Regel-Hook gestoppt: {reason}");
        placementHook.EmergencyStopped += HandlePlacementHookStopped;
        hookResumeTimer = new DispatcherTimer(DispatcherPriority.Normal, window.Dispatcher);
        hookResumeTimer.Tick += (_, _) =>
        {
            hookResumeTimer.Stop();
            if (emergencyStopped && !shuttingDown)
            {
                ResumeSnapping();
            }
        };
        // Das Zonen-Vollbild haengt an denselben Fensterereignissen wie das Positionsgedaechtnis; ein
        // eigener Hook waere ein zweites Abonnement auf dieselben Meldungen.
        placementHook.EventReceived += zoneFullscreen.Handle;
        zoneFullscreenTimer = new DispatcherTimer(DispatcherPriority.Background, window.Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        zoneFullscreenTimer.Tick += (_, _) => zoneFullscreen.Poll();
        zoneFullscreenTimer.Start();
        hotkeys.ZoneHotkeyPressed += HandleZoneHotkey;
        window.PreviewActiveLayoutsRequested += PreviewActiveLayouts;
        previewTimer = new DispatcherTimer(DispatcherPriority.Normal, window.Dispatcher)
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        previewTimer.Tick += (_, _) =>
        {
            previewTimer.Stop();
            previewOverlays.HideAll();
        };
        overlayDelayTimer = new DispatcherTimer(DispatcherPriority.Normal, window.Dispatcher);
        overlayDelayTimer.Tick += (_, _) =>
        {
            overlayDelayTimer.Stop();
            if (pendingOverlayShow is { } pending && coordinator?.State == DragState.Tracking)
            {
                ShowOverlaysNow(pending);
            }

            pendingOverlayShow = null;
        };

        // Der Not-Aus ist ein Umschalter: einmal gedrueckt haelt er an, erneut gedrueckt laeuft es weiter.
        hotkeys.EmergencyStopRequested += () =>
        {
            if (emergencyStopped)
            {
                ResumeSnapping();
            }
            else
            {
                EmergencyStop("Not-Aus ausgelöst: Einrasten pausiert. Ctrl + Alt + Shift + F12 schaltet es wieder ein.");
            }
        };
        window.Closing += Window_Closing;
        monitorWatcher = new MonitorWatcher(synchronizationContext);
        monitorWatcher.Changed += HandleMonitorsChanged;

        // Beim Start einmal abgleichen: umgesteckte Monitore uebernehmen, verwaiste Namen entfernen,
        // die fuer diese Monitorkombination gemerkten Layouts aktivieren.
        ReconcileMonitors(announce: false);
        Reconfigure(configuration);

        // Eine vom letzten Update beiseitegeschobene Programmdatei laesst sich erst loeschen, wenn der
        // Prozess, der sie belegte, geendet hat. Der naechste Start ist der erste Zeitpunkt dafuer.
        if (Environment.ProcessPath is { Length: > 0 } processPath)
        {
            var removed = UpdateInstaller.RemoveSupersededFiles(processPath)
                + UpdateInstaller.RemoveSupersededFiles(UpdateInstaller.BuildHelperPath(processPath));
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
        viewModel.PauseReason = reason;
        viewModel.SnappingState = SnappingState.Paused;
        configuration = viewModel.Configuration;
        // Der Hotkey bleibt registriert, damit er das Einrasten auch wieder einschalten kann.
        _ = hotkeys.Configure(emergencyStopEnabled: true);
        tray.SetSnappingState(viewModel.SnappingStateLabel, paused: true);
        tray.Update(configuration);
        log.Write("WARN", reason);
    }

    /// <summary>
    /// Hebt Not-Aus und Sicherheitsstopp auf. Frueher gab es diesen Weg nicht: nach einem Stopp blieb
    /// das Einrasten bis zum Programmneustart abgeschaltet, ohne sichtbaren Hinweis.
    /// </summary>
    public void ResumeSnapping()
    {
        if (!emergencyStopped || shuttingDown)
        {
            return;
        }

        emergencyStopped = false;
        placementEngine.ResetEmergencyStop();
        viewModel.PauseReason = null;
        Reconfigure(configuration);
        viewModel.StatusMessage = SnapActivationPolicy.ShouldEnable(configuration)
            ? "Einrasten wieder aktiv"
            : "Einrasten bereit – sobald ein Layout aktiv ist";
        log.Write("INFO", "Einrasten nach Not-Aus oder Sicherheitsstopp wieder aktiviert.");
    }

    /// <summary>
    /// Der Fensterplatzierungs-Hook hat sich abgeschaltet. Nach der Ereignisgrenze laeuft er nach kurzer
    /// Ruhe von selbst wieder an; nach einem Fehler oder bei gehaeuften Stopps bleibt es beim
    /// Sicherheitsstopp, den der Anwender von Hand aufhebt.
    /// </summary>
    private void HandlePlacementHookStopped(string reason)
    {
        var resumeAfter = hookRecovery.Decide(reason, DateTimeOffset.UtcNow);
        if (resumeAfter is { } delay)
        {
            EmergencyStop(
                $"Fensterplatzierungs-Hook gestoppt: {reason} Das Einrasten läuft in {delay.TotalSeconds:0} Sekunden von selbst weiter.");
            hookResumeTimer.Interval = delay;
            hookResumeTimer.Start();
            return;
        }

        EmergencyStop($"Fensterplatzierungs-Hook gestoppt: {reason}");
    }

    /// <summary>
    /// Legt vor einem Neustart in eine ersetzte Programmdatei alles still und speichert. Wirft nicht:
    /// was sich nicht speichern liess, steht im Protokoll, der Neustart findet trotzdem statt.
    /// </summary>
    public async Task PrepareForExecutableChangeAsync()
    {
        shuttingDown = true;
        hookResumeTimer.Stop();
        QuiesceForShutdown();
        var result = await exitSaveCoordinator.TryPrepareForShutdownAsync(
            viewModel.Save,
            ShutdownSaveTimeout,
            placementEngine.FlushAsync);
        if (!result.IsSaved)
        {
            log.Write(
                "ERROR",
                $"Vor dem Neustart konnte nicht vollständig gespeichert werden: {result.Describe()}",
                result.Failure);
        }
    }

    /// <summary>
    /// Startet einen Nachfolger aus der genannten Programmdatei und beendet danach den eigenen Prozess.
    /// Der Nachfolger wartet auf das Ende dieses Prozesses, bevor er die Einzelinstanz beansprucht.
    /// </summary>
    private void StartSuccessorAndExit(string executablePath)
    {
        viewModel.Save();
        var arguments = StartupArguments.ForSuccessor(startupArguments, Environment.ProcessId, hidden: !window.IsVisible);
        if (ProcessRestart.TryStart(executablePath, arguments, log.Write))
        {
            RequestExit();
            return;
        }

        viewModel.StatusMessage = $"{System.IO.Path.GetFileName(executablePath)} liess sich nicht starten. Einzelheiten stehen im Protokoll.";
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        allowClose = true;
        hookResumeTimer.Stop();
        monitorWatcher.Changed -= HandleMonitorsChanged;
        monitorWatcher.Dispose();
        cursorTimer.Stop();
        identificationTimer.Stop();
        moveHook.Disable();
        appRuleCoordinator.CancelPending();
        appRuleHook.Disable();
        overlays.HideAll();
        tray.Dispose();
        hotkeys.Dispose();
        helper?.Dispose();
        moveHook.Dispose();
        appRuleCoordinator.Dispose();
        appRuleHook.Dispose();
        placementEngine.Stop();
        placementHook.EventReceived -= zoneFullscreen.Handle;
        zoneFullscreenTimer.Stop();
        placementHook.Dispose();
        overlays.Dispose();
        previewTimer.Stop();
        previewOverlays.Dispose();
        window.PreviewActiveLayoutsRequested -= PreviewActiveLayouts;
        monitorIdentification.Dispose();
        toast.Close();
        saveCoordinator.SaveFinished -= SaveFinished;
        placementSaveCoordinator.SaveFinished -= PlacementSaveFinished;
        window.ExportConfigurationRequested -= ExportConfigurationAsync;
        window.ImportConfigurationRequested -= ImportConfigurationAsync;
        window.IdentifyMonitorsRequested -= IdentifyMonitors;
        window.SettingsPageOpened -= PublishCertificateStatus;
        viewModel.BackupsRefreshRequested -= RefreshBackups;
        viewModel.RestoreBackupRequested -= RestoreBackup;
    }

    /// <summary>
    /// Reagiert auf geaenderte Monitore: neu einlesen, Konfiguration abgleichen, Zonen und Overlays
    /// neu aufbauen. Ein laufender Ziehvorgang wird dabei abgebrochen, weil seine Ziele nicht mehr
    /// stimmen.
    /// </summary>
    private void HandleMonitorsChanged()
    {
        if (shuttingDown || disposed)
        {
            return;
        }

        IReadOnlyList<LiveMonitor> fresh;
        try
        {
            fresh = monitorService.GetMonitors();
        }
        catch (Exception exception)
        {
            log.Write("ERROR", "Die Monitore konnten nach einer Änderung nicht gelesen werden.", exception);
            return;
        }

        if (fresh.Count == 0 || SameMonitors(monitors, fresh))
        {
            return;
        }

        log.Write("INFO", $"Monitore geändert: {string.Join(", ", fresh.Select(DescribeMonitor))}");
        monitors = fresh;
        ReconcileMonitors(announce: true);
        Reconfigure(configuration);
        if (!emergencyStopped)
        {
            foreach (var layout in configuration.Layouts.Where(layout => layout.IsActive))
            {
                CollectStrayWindowsIntoMainZone(layout.Id);
            }
        }
    }

    private void ReconcileMonitors(bool announce)
    {
        var result = MonitorReconciliation.Reconcile(configuration, monitors);
        var setKey = MonitorSets.KeyFor(monitors);
        var reconciled = MonitorSets.Apply(result.Configuration, setKey, monitors, out var activated);
        reconciled = MonitorSets.Record(reconciled, setKey, monitors);
        var notices = result.Notices
            .Concat(activated.Select(layout => $"Layout «{layout.Name}» für diese Monitorkombination aktiviert."))
            .ToArray();
        var changed = notices.Length > 0 || !ReferenceEquals(reconciled, configuration);

        viewModel.ReplaceMonitors(monitors, changed ? reconciled : null);
        configuration = viewModel.Configuration;
        foreach (var notice in notices)
        {
            log.Write("INFO", notice);
        }

        if (announce)
        {
            viewModel.StatusMessage = notices.Length > 0
                ? string.Join(" ", notices)
                : $"Monitore neu erkannt: {monitors.Count} verbunden.";
        }
        else if (notices.Length > 0)
        {
            viewModel.StatusMessage = string.Join(" ", notices);
        }

        if (changed)
        {
            saveCoordinator.RequestSave(configuration);
        }
    }

    private static bool SameMonitors(IReadOnlyList<LiveMonitor> first, IReadOnlyList<LiveMonitor> second) =>
        first.Count == second.Count &&
        first.Zip(second).All(pair =>
            string.Equals(pair.First.Identity.StableId, pair.Second.Identity.StableId, StringComparison.OrdinalIgnoreCase) &&
            pair.First.WorkArea == pair.Second.WorkArea &&
            pair.First.MonitorBounds == pair.Second.MonitorBounds &&
            pair.First.DpiX == pair.Second.DpiX &&
            pair.First.IsPrimary == pair.Second.IsPrimary);

    private static string DescribeMonitor(LiveMonitor monitor) =>
        $"{monitor.Identity.FriendlyName} {monitor.WorkArea.Width}×{monitor.WorkArea.Height}@{monitor.DpiX}dpi";

    /// <summary>
    /// Sichert beim Abmelden oder Herunterfahren, was noch aussteht. Windows gibt dafuer nur wenige
    /// Sekunden; laenger wird nicht gewartet.
    /// </summary>
    public void PrepareForSessionEnd()
    {
        try
        {
            QuiesceForShutdown();
            viewModel.Save();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            FlushAsync(timeout.Token).GetAwaiter().GetResult();
            log.Write("INFO", "Einstellungen vor dem Sitzungsende gesichert.");
        }
        catch (Exception exception)
        {
            log.Write("ERROR", "Die Sicherung vor dem Sitzungsende ist unvollständig.", exception);
        }
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
            CollectStrayWindowsIntoMainZone(layoutId);
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

                var metrics = new LayoutMetrics(
                    newConfiguration.Settings.EffectiveOuterMargins,
                    newConfiguration.Settings.ZoneGap);
                foreach (var target in LayoutWindowReflow.Plan(
                    oldLayout, newLayout, monitor.WorkArea, metrics, windows))
                {
                    var outcome = windowService.Snap(target.WindowHandle, target.Bounds);
                    if (outcome.Succeeded)
                    {
                        log.Write("DEBUG", $"Fenster 0x{target.WindowHandle:X} an geänderte Zone angepasst: {target.Bounds}.");
                    }
                    else
                    {
                        log.Write("WARN", $"Fenster 0x{target.WindowHandle:X} konnte nicht an die geänderte Zone angepasst werden: {outcome.Rejection}");
                    }
                }
            }
        }
        catch (Exception exception)
        {
            log.Write("WARN", "Fenster konnten nach einer Layoutänderung nicht vollständig angepasst werden.", exception);
        }
    }

    /// <summary>
    /// Legt nach einem Layoutwechsel die Fenster in die Hauptzone, die <see cref="MainZoneSweep"/> dafuer
    /// bestimmt. Der Entscheid steckt vollstaendig in der Kernfunktion; hier bleiben nur das Einsammeln
    /// der Fenster und das Setzen.
    /// </summary>
    private void CollectStrayWindowsIntoMainZone(Guid activatedLayoutId)
    {
        var activatedLayout = configuration.Layouts.FirstOrDefault(layout => layout.Id == activatedLayoutId);
        var activatedMonitor = activatedLayout is null
            ? null
            : monitors.FirstOrDefault(monitor => LayoutService.BelongsToMonitor(monitor.Identity, activatedLayout.Monitor));
        if (activatedMonitor is null)
        {
            return;
        }

        try
        {
            var planned = MainZoneSweep.Plan(
                configuration,
                BuildPlacementEnvironment(configuration).Zones,
                activatedMonitor.WorkArea,
                windowService
                    .GetMovableTopLevelWindows(Environment.ProcessId)
                    .Select(window => new MainZoneSweepWindow(window.WindowHandle, window.Bounds)),
                handle => windowService.InspectRuleCandidate(handle, Environment.ProcessId)?.Identity);

            foreach (var target in planned)
            {
                var outcome = windowService.Snap(target.WindowHandle, target.Bounds);
                if (outcome.Succeeded)
                {
                    log.Write("DEBUG", $"Fenster 0x{target.WindowHandle:X} in der Hauptzone aufgefangen: {target.Bounds}.");
                }
                else
                {
                    log.Write("WARN", $"Fenster 0x{target.WindowHandle:X} konnte nicht in der Hauptzone aufgefangen werden: {outcome.Rejection}");
                }
            }
        }
        catch (Exception exception)
        {
            log.Write("WARN", "Fenster konnten nach dem Layoutwechsel nicht in der Hauptzone aufgefangen werden.", exception);
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
                viewModel.MarkSaved();
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
            var result = await updates.StageAsync(CancellationToken.None);
            viewModel.UpdateStatus = result.Message;
            if (result.Status != UpdateInstallStatus.Staged)
            {
                return;
            }

            viewModel.IsUpdateAvailable = false;
        }
        finally
        {
            viewModel.IsUpdateBusy = false;
        }

        // Erst speichern, dann die Uebernahme starten, dann selbst enden. Die laufende Programmdatei
        // bleibt bis dahin unangetastet; der Uebernahmeprozess wartet auf das Ende dieses Prozesses.
        viewModel.Save();
        await saveCoordinator.FlushAsync(CancellationToken.None);
        await placementEngine.FlushAsync(CancellationToken.None);
        if (updates.TryLaunchApply())
        {
            RequestExit();
            return;
        }

        viewModel.StatusMessage =
            "Die neue Version liegt bereit, die Übernahme liess sich aber nicht starten. Einzelheiten stehen im Protokoll.";
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

        // Der erhoehte Nachfolger wartet auf das Ende dieses Prozesses; ohne diese Wartezeit fand er die
        // Einzelinstanz noch besetzt, aktivierte sie und beendete sich — der Neustart blieb dann aus.
        var result = elevation.Offer(
            Environment.ProcessPath ?? string.Empty,
            StartupArguments.ForSuccessor(startupArguments, Environment.ProcessId, hidden: !window.IsVisible));
        switch (result.Status)
        {
            case ElevationEscalationStatus.Restarting:
                viewModel.Save();
                RequestExit();
                break;
            case ElevationEscalationStatus.Declined:
                viewModel.StatusMessage =
                    "Fenster höher berechtigter Programme bleiben unberührt. "
                        + "Unter Einstellungen lässt sich das ändern.";
                break;
            case ElevationEscalationStatus.Failed:
                viewModel.StatusMessage = result.Message ?? "Der erhoehte Neustart ist fehlgeschlagen.";
                log.Write("WARN", result.Message ?? "Der erhoehte Neustart ist fehlgeschlagen.");
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// Fuehrt Zertifikats- und Helferstand in den Einstellungen nach. Beide gehoeren zusammen: ein
    /// Zertifikat ohne signierten Helfer nuetzt nichts, ein Helfer ohne Zertifikat startet nicht.
    /// </summary>
    private void PublishCertificateStatus()
    {
        var status = certificates.Read();
        viewModel.IsCertificateInstalled = status.State == CertificateState.Trusted;
        viewModel.CertificateStatus = status.Message;
        if (helper is null)
        {
            viewModel.HelperStatus = "Der Fensterhelfer ist nicht vorhanden. Er entsteht bei der Installation nach «Programme».";
            return;
        }

        if (helper.Status.State == HelperState.Idle && status.State != CertificateState.Trusted)
        {
            viewModel.HelperStatus = "Der Fensterhelfer wartet auf ein eingerichtetes Zertifikat.";
            return;
        }

        // Der Probelauf startet einen Prozess und wartet auf seine Antwort; das gehoert nicht auf den
        // UI-Thread. Frueher fror das Fenster beim Oeffnen der Einstellungen bis zu 15 Sekunden ein.
        viewModel.HelperStatus = "Der Fensterhelfer wird geprüft …";
        var probe = helper;
        _ = Task.Run(() => probe.Probe().Message).ContinueWith(
            completed => window.Dispatcher.InvokeAsync(() =>
                viewModel.HelperStatus = completed.IsCompletedSuccessfully
                    ? completed.Result
                    : "Der Fensterhelfer konnte nicht geprüft werden."),
            TaskScheduler.Default);
    }

    /// <summary>
    /// Zertifikat einrichten oder entfernen schreibt in den Zertifikatspeicher der Maschine und braucht
    /// Administratorrechte. Laeuft das Programm gewoehnlich berechtigt, uebernimmt ein erhoehter
    /// Hilfsprozess derselben Programmdatei die Aktion; Windows fragt dafuer einmal nach. Bis zum
    /// 04.09.2026 lief die Aktion auch dann im eigenen Prozess und scheiterte mit «Zugriff verweigert».
    /// </summary>
    private void InstallSigningCertificate()
    {
        if (ElevationState.IsAdministrator())
        {
            var helperPath = HelperChannel.ResolvePath(Environment.ProcessPath ?? string.Empty);
            _ = RunCertificateActionAsync(
                "Zertifikat wird eingerichtet …",
                () => certificates.Install(helperPath, TimeProvider.System.GetUtcNow()));
            return;
        }

        _ = RunCertificateActionAsync(
            "Zertifikat wird eingerichtet; Windows fragt nach Administratorrechten …",
            () => RunCertificateCommandElevated(
                StartupArguments.InstallCertificate,
                "Das Zertifikat ist eingerichtet und der Fensterhelfer signiert."));
    }

    private void RemoveSigningCertificate()
    {
        if (ElevationState.IsAdministrator())
        {
            _ = RunCertificateActionAsync("Zertifikat wird entfernt …", certificates.Remove);
            return;
        }

        _ = RunCertificateActionAsync(
            "Zertifikat wird entfernt; Windows fragt nach Administratorrechten …",
            () => RunCertificateCommandElevated(
                StartupArguments.RemoveCertificate,
                "Das Zertifikat wurde entfernt."));
    }

    private CertificateActionResult RunCertificateCommandElevated(string argument, string successMessage)
    {
        var result = new ElevatedSelfInvocation(Environment.ProcessPath ?? string.Empty)
            .Run([argument], TimeSpan.FromMinutes(3));
        if (result.Succeeded)
        {
            return new CertificateActionResult(true, successMessage);
        }

        return new CertificateActionResult(
            false,
            result.Status == ElevatedRunStatus.Completed
                ? "Die Zertifikatsaktion ist im erhöhten Hilfsprozess fehlgeschlagen. Einzelheiten stehen im Protokoll."
                : result.Message ?? "Die Zertifikatsaktion ist fehlgeschlagen.");
    }

    /// <summary>
    /// Fuehrt eine Zertifikatsaktion im Hintergrund aus. Beide rufen PowerShell auf und warten bis zu
    /// zwei Minuten; die Oberflaeche bleibt bedienbar, nur die Schaltflaeche ist gesperrt.
    /// </summary>
    private async Task RunCertificateActionAsync(string pendingMessage, Func<CertificateActionResult> action)
    {
        if (viewModel.IsCertificateBusy)
        {
            return;
        }

        viewModel.IsCertificateBusy = true;
        viewModel.StatusMessage = pendingMessage;
        try
        {
            var result = await Task.Run(action);
            viewModel.StatusMessage = result.Message;
            log.Write(result.Successful ? "INFO" : "ERROR", result.Message);
        }
        catch (Exception exception)
        {
            log.Write("ERROR", "Die Zertifikatsaktion ist fehlgeschlagen.", exception);
            viewModel.StatusMessage = $"Zertifikatsaktion fehlgeschlagen: {exception.Message}";
        }
        finally
        {
            viewModel.IsCertificateBusy = false;
            PublishCertificateStatus();
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

    /// <summary>
    /// Die Installation schreibt nach «Programme» und in HKEY_LOCAL_MACHINE. Laeuft das Programm
    /// gewoehnlich berechtigt, erledigt das ein erhoehter Hilfsprozess derselben Programmdatei; das
    /// installierte Programm startet danach dieser Prozess, damit es nicht die Administratorrechte des
    /// Hilfsprozesses erbt. Bis zum 04.09.2026 lief die Installation auch ohne Rechte im eigenen Prozess
    /// und scheiterte mit «Zugriff verweigert».
    /// </summary>
    private void InstallToProgramFiles()
    {
        if (ElevationState.IsAdministrator())
        {
            InstallToProgramFilesInProcess();
            return;
        }

        _ = InstallToProgramFilesElevatedAsync();
    }

    private void InstallToProgramFilesInProcess()
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
        StartSuccessorAndExit(startPath);
    }

    private async Task InstallToProgramFilesElevatedAsync()
    {
        if (!viewModel.CanInstall)
        {
            return;
        }

        var processPath = Environment.ProcessPath ?? string.Empty;
        viewModel.CanInstall = false;
        viewModel.StatusMessage = "Installation läuft; Windows fragt nach Administratorrechten …";
        ElevatedRunResult result;
        try
        {
            result = await new ElevatedSelfInvocation(processPath).RunAsync(
                [SetupRunner.InstallArgument, SetupRunner.SilentArgument, StartupArguments.NoLaunch],
                TimeSpan.FromMinutes(5));
        }
        finally
        {
            PublishInstallationStatus();
        }

        if (!result.Succeeded)
        {
            var message = result.Status == ElevatedRunStatus.Completed
                ? "Die Installation ist fehlgeschlagen. Einzelheiten stehen im Protokoll."
                : result.Message ?? "Die Installation ist fehlgeschlagen.";
            viewModel.StatusMessage = message;
            log.Write("WARN", message);
            return;
        }

        var installedPath = InstallationService.CreatePlan(processPath).TargetPath;
        var success = $"Installiert nach {System.IO.Path.GetDirectoryName(installedPath)}. Das Programm wird von dort gestartet.";
        viewModel.StatusMessage = success;
        log.Write("INFO", success);
        StartSuccessorAndExit(installedPath);
    }

    private void ForgetWindowPositions()
    {
        var previous = placementEngine.Catalog;
        placementEngine.ForgetAll();
        viewModel.StatusMessage = "Gemerkte Fensterpositionen verworfen";
        viewModel.ShowToast(
            previous.Entries.Count == 1 ? "Eine gemerkte Fensterposition verworfen." : $"{previous.Entries.Count} gemerkte Fensterpositionen verworfen.",
            () =>
            {
                placementEngine.ReplaceCatalog(previous);
                viewModel.StatusMessage = "Gemerkte Fensterpositionen wiederhergestellt";
            });
    }

    private void RefreshBackups()
    {
        if (backups is null)
        {
            return;
        }

        try
        {
            viewModel.ReplaceBackups(backups.List(configuration));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            log.Write("WARN", $"Die Sicherungen liessen sich nicht lesen: {exception.Message}");
        }
    }

    private void RestoreBackup(ConfigurationBackup backup) => _ = RestoreBackupAsync(backup);

    /// <summary>
    /// Stellt einen frueheren Stand wieder her. Das anschliessende Speichern legt den bisherigen Stand
    /// selbst als juengste Sicherung ab; der Toast fuehrt ausserdem direkt zurueck.
    /// </summary>
    private async Task RestoreBackupAsync(ConfigurationBackup backup)
    {
        if (backups is null)
        {
            return;
        }

        SnapConfiguration restored;
        try
        {
            restored = await backups.LoadAsync(backup.Path, CancellationToken.None);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or System.Text.Json.JsonException or UnauthorizedAccessException)
        {
            viewModel.StatusMessage = $"Der Stand liess sich nicht lesen: {exception.Message}";
            return;
        }

        var previous = viewModel.Configuration;
        var when = BackupListItem.Describe(backup.SavedAt, DateTimeOffset.Now);
        viewModel.ReplaceConfiguration(restored);
        viewModel.StatusMessage = $"Stand {when} wiederhergestellt";
        viewModel.Save();
        viewModel.ShowToast($"Stand von {when} wiederhergestellt.", () =>
        {
            viewModel.ReplaceConfiguration(previous);
            viewModel.StatusMessage = "Wiederherstellung zurückgenommen";
            viewModel.Save();
        });
        RefreshBackups();
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
        // Der Koordinator merkt sich Flaechen in Bildschirmkoordinaten; nach geaenderten Zonen zeigen
        // die auf Stellen, die es so nicht mehr gibt.
        zoneFullscreen.Reset();
        var targets = BuildTargets(newConfiguration);
        overlays.UpdateTargets(targets);
        ApplyFineTuning(newConfiguration.Settings);
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
        viewModel.SnappingState = emergencyStopped
            ? SnappingState.Paused
            : snappingEnabled ? SnappingState.Active : SnappingState.NoActiveLayout;
        tray.SetSnappingState(viewModel.SnappingStateLabel, paused: emergencyStopped);
        // Zonenkuerzel nur, solange das Einrasten laeuft; der Not-Aus bleibt auch im Stopp erreichbar.
        var hotkeyResult = hotkeys.Configure(
            snappingEnabled || emergencyStopped,
            snappingEnabled && newConfiguration.Settings.ZoneHotkeysEnabled,
            newConfiguration.Settings.ZoneHotkeyModifiers);
        if (hotkeyResult.Errors.Count > 0)
        {
            viewModel.StatusMessage = string.Join(" ", hotkeyResult.Errors);
            log.Write("WARN", string.Join(" ", hotkeyResult.Errors));
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

    /// <summary>
    /// Reicht die Feinabstimmung an die Dienste weiter, die sie brauchen. Alles hier hat einen sicheren
    /// Standard; die Werte kommen aus den erweiterten Einstellungen.
    /// </summary>
    private void ApplyFineTuning(AppSettings settings)
    {
        if (windowService is WindowsWindowService concrete)
        {
            concrete.ActivateAfterPlacement = settings.ActivateWindowAfterSnap;
            concrete.FixedSizePlacement = settings.FixedSizeWindowPlacement;
            concrete.TolerancePixels = settings.PlacementTolerancePixels;
        }

        placementWindowService.TolerancePixels = settings.PlacementTolerancePixels;
        moveHook.SetEventLimit(settings.MoveHookEventLimit);
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

    /// <summary>
    /// Die Platzierungsumgebung wird je Konfigurations- und Monitorstand nur einmal gebaut. Sie wird bei
    /// jedem Fensterereignis abgefragt — beim Ziehen Dutzende Male je Sekunde —, und bis zum 05.09.2026
    /// wurden dabei jedes Mal alle Zonen neu in Pixel umgerechnet. Konfiguration und Monitorliste sind
    /// unveraenderlich; eine neue Instanz bedeutet eine Aenderung.
    /// </summary>
    private PlacementEnvironment BuildPlacementEnvironment(SnapConfiguration currentConfiguration)
    {
        var currentMonitors = monitors;
        lock (environmentGate)
        {
            if (cachedEnvironment is { } cached &&
                ReferenceEquals(cachedEnvironmentConfiguration, currentConfiguration) &&
                ReferenceEquals(cachedEnvironmentMonitors, currentMonitors))
            {
                return cached;
            }
        }

        var built = BuildPlacementEnvironmentCore(currentConfiguration, currentMonitors);
        lock (environmentGate)
        {
            cachedEnvironment = built;
            cachedEnvironmentConfiguration = currentConfiguration;
            cachedEnvironmentMonitors = currentMonitors;
        }

        return built;
    }

    private static PlacementEnvironment BuildPlacementEnvironmentCore(
        SnapConfiguration currentConfiguration,
        IReadOnlyList<LiveMonitor> monitors)
    {
        var metrics = new LayoutMetrics(
            currentConfiguration.Settings.EffectiveOuterMargins,
            currentConfiguration.Settings.ZoneGap);
        var placementMonitors = monitors
            .Select(monitor => new PlacementMonitorTarget(
                string.IsNullOrWhiteSpace(monitor.Identity.StableId)
                    ? monitor.Identity.DeviceName
                    : monitor.Identity.StableId,
                monitor.WorkArea,
                monitor.IsPrimary,
                monitor.MonitorBounds))
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
                    ZoneGeometry.ToPixels(zone.Bounds, monitor.WorkArea, metrics)))))
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

        var snapshot = windowService.Inspect(windowHandle, cursor, Environment.ProcessId);
        if (snapshot is null)
        {
            log.Write("DEBUG", $"Verschiebestart verworfen: Fenster 0x{windowHandle:X} ist nicht lesbar.");
            return;
        }

        log.Write("DEBUG", $"Verschiebestart hwnd=0x{windowHandle:X} cursor={cursor.X},{cursor.Y} status={snapshot}");
        if (configuration.Settings.TriggerMode == TriggerMode.ShiftKey && !windowService.IsShiftPressed())
        {
            // Umschalt darf auch erst waehrend des Ziehens gedrueckt werden: der Zeitgeber wartet darauf.
            // Frueher musste die Taste schon beim Anfassen des Fensters unten sein.
            pendingShiftDrag = (windowHandle, snapshot);
            cursorTimer.Start();
            return;
        }

        StartTracking(windowHandle, snapshot, cursor);
    }

    private void StartTracking(nint windowHandle, WindowSnapshot snapshot, PointInt cursor)
    {
        if (coordinator is null)
        {
            return;
        }

        placedDuringDrag = false;
        dragStartedInZone = configuration.Settings.RestoreSizeWhenLeavingZone && IsWindowInAnyZone(windowHandle);
        coordinator.Start(windowHandle, snapshot, cursor);
        log.Write("DEBUG", $"Koordinatorstatus nach Start: {coordinator.State}");
        if (coordinator.State == DragState.Tracking)
        {
            draggedWindow = windowHandle;
            draggedSnapshot = snapshot;
            dragStartedAt = DateTimeOffset.UtcNow;
            dragStartedWithButton = windowService.IsLeftButtonPressed();
            buttonReleasedAt = null;
            cursorTimer.Start();
        }
    }

    private void MoveEnded()
    {
        cursorTimer.Stop();
        pendingShiftDrag = null;
        log.Write("DEBUG", $"Verschiebeende bei Koordinatorstatus {coordinator?.State}.");
        overlayDelayTimer.Stop();
        pendingOverlayShow = null;
        var endedWindow = draggedWindow;
        if (windowService.TryGetCursorPosition(out var cursor))
        {
            coordinator?.End(cursor, windowService.IsControlPressed());
        }
        else
        {
            coordinator?.End();
        }

        if (dragStartedInZone && !placedDuringDrag && endedWindow != 0)
        {
            RestoreSizeAfterLeavingZone(endedWindow);
        }

        dragStartedInZone = false;
        draggedWindow = 0;
        draggedSnapshot = null;
    }

    /// <summary>Ob das Fenster gerade auf einer Zone des aktiven Layouts eingerastet liegt.</summary>
    private bool IsWindowInAnyZone(nint windowHandle)
    {
        var current = windowService.Capture(windowHandle);
        if (current is null)
        {
            return false;
        }

        var metrics = new LayoutMetrics(configuration.Settings.EffectiveOuterMargins, configuration.Settings.ZoneGap);
        return BuildTargets(configuration).Any(target => target.PartMonitors.Any(zone =>
            current.NormalPosition.IsWithinTolerance(
                ZoneGeometry.ToPixels(zone.Bounds, target.Monitor.WorkArea, metrics),
                configuration.Settings.SnappedTolerancePixels)));
    }

    /// <summary>
    /// Ein Fenster, das aus einer Zone herausgezogen und nirgends abgelegt wurde, bekommt die Groesse
    /// zurueck, die es vor dem Einrasten hatte; die neue Position bleibt. Nur auf Wunsch
    /// (RestoreSizeWhenLeavingZone), weil manche Anwender die Zonengroesse bewusst mitnehmen.
    /// </summary>
    private void RestoreSizeAfterLeavingZone(nint windowHandle)
    {
        var current = windowService.Capture(windowHandle);
        if (current is null || !placementHistory.TryPeek(current.Identity, out var previous))
        {
            return;
        }

        var size = previous.NormalPosition;
        if (size.Width < 1 || size.Height < 1 ||
            current.NormalPosition.IsWithinTolerance(previous.NormalPosition, 4))
        {
            return;
        }

        var target = new PixelRect(current.NormalPosition.X, current.NormalPosition.Y, size.Width, size.Height);
        var outcome = windowService.Snap(windowHandle, target);
        log.Write("DEBUG", $"Grösse nach Verlassen der Zone wiederhergestellt: {target}, Ergebnis {outcome.Succeeded}.");
        if (outcome.Succeeded)
        {
            placementHistory.DiscardTop(current.Identity);
        }
    }

    /// <summary>Wie lange die Maustaste losgelassen sein darf, bevor ein Ziehen als beendet gilt.</summary>
    private static readonly TimeSpan ButtonReleaseGrace = TimeSpan.FromMilliseconds(1000);


    private void CursorTimer_Tick(object? sender, EventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        if (pendingShiftDrag is { } pending)
        {
            if (windowService.IsShiftPressed() && windowService.TryGetCursorPosition(out var startCursor))
            {
                pendingShiftDrag = null;
                StartTracking(pending.Handle, pending.Snapshot, startCursor);
            }
            else if (!windowService.IsWindowAlive(pending.Handle))
            {
                pendingShiftDrag = null;
                cursorTimer.Stop();
            }

            return;
        }

        if (coordinator is null || coordinator.State != DragState.Tracking)
        {
            cursorTimer.Stop();
            return;
        }

        if (windowService.IsEscapePressed())
        {
            cursorTimer.Stop();
            coordinator.Cancel();
            return;
        }

        if (configuration.Settings.TriggerMode == TriggerMode.ShiftKey && !windowService.IsShiftPressed())
        {
            // Umschalt losgelassen: Zonen weg, aber weiter darauf warten, dass sie wieder gedrueckt wird.
            coordinator.Cancel();
            if (draggedSnapshot is { } snapshot)
            {
                pendingShiftDrag = (draggedWindow, snapshot);
            }

            return;
        }

        if (IsDragOrphaned())
        {
            cursorTimer.Stop();
            coordinator.Cancel();
            return;
        }

        if (windowService.TryGetCursorPosition(out var cursor))
        {
            coordinator.Update(cursor, windowService.IsControlPressed());
        }
    }

    /// <summary>
    /// Wachhund fuer ein Ziehen, dessen Endereignis nie ankommt: Fenster zerstoert, Sitzung gewechselt,
    /// Hook gedrosselt. Ohne ihn blieben die Overlays dauerhaft ueber allem stehen.
    /// </summary>
    private bool IsDragOrphaned()
    {
        var now = DateTimeOffset.UtcNow;
        if (!windowService.IsWindowAlive(draggedWindow))
        {
            log.Write("WARN", "Ziehvorgang abgebrochen: das Fenster ist verschwunden.");
            return true;
        }

        var maximumDuration = TimeSpan.FromSeconds(Math.Clamp(configuration.Settings.DragWatchdogSeconds, 5, 600));
        if (now - dragStartedAt > maximumDuration)
        {
            log.Write("WARN", $"Ziehvorgang abgebrochen: kein Endereignis innerhalb von {maximumDuration.TotalSeconds:0} Sekunden.");
            return true;
        }

        if (!dragStartedWithButton)
        {
            return false;
        }

        if (windowService.IsLeftButtonPressed())
        {
            buttonReleasedAt = null;
            return false;
        }

        buttonReleasedAt ??= now;
        if (now - buttonReleasedAt.Value <= ButtonReleaseGrace)
        {
            return false;
        }

        log.Write("WARN", "Ziehvorgang abgebrochen: Maustaste losgelassen, aber kein Endereignis von Windows.");
        return true;
    }

    private void HandleDragAction(SnapZones.Core.Drag.DragAction action)
    {
        switch (action)
        {
            case ShowOverlaysAction show:
                // Eine Anzeigeverzoegerung unterdrueckt das Aufblitzen der Zonen bei kurzen Zuegen.
                var delayMilliseconds = configuration.Settings.OverlayShowDelayMilliseconds;
                if (delayMilliseconds > 0 && pendingOverlayShow is null && !overlays.IsAnyVisible)
                {
                    pendingOverlayShow = show;
                    overlayDelayTimer.Interval = TimeSpan.FromMilliseconds(delayMilliseconds);
                    overlayDelayTimer.Start();
                }
                else
                {
                    ShowOverlaysNow(show);
                }

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
                ReportPlacement(fillSpan.WindowHandle, spanResult, $"Fenster wurde über {fillSpan.PartMonitorIds.Count} Zonen gelegt.");
                break;
            case HideOverlaysAction:
                overlayDelayTimer.Stop();
                pendingOverlayShow = null;
                overlays.HideAll();
                break;
            case FillPartMonitorAction fill:
                var result = partMonitorCommands?.Execute(new FillPartMonitorCommand(
                    fill.WindowHandle,
                    fill.MonitorId,
                    fill.PartMonitorId));
                ReportPlacement(fill.WindowHandle, result, "Fenster wurde in einen Teilmonitor eingerastet.");
                break;
        }
    }

    private void ShowOverlaysNow(ShowOverlaysAction show)
    {
        overlays.Show(
            show.MonitorIds,
            new LayoutMetrics(configuration.Settings.EffectiveOuterMargins, configuration.Settings.ZoneGap),
            configuration.Settings.OverlayColor,
            configuration.Settings.OverlayOpacity,
            configuration.Settings.ShowZoneNames,
            OverlayStyle.From(configuration.Settings));
    }

    /// <summary>
    /// Meldet das gemessene Ergebnis einer Platzierung. Ein Fenster, das sich gar nicht bewegen liess,
    /// kann einem hoeher berechtigten Programm gehoeren; ein Fenster, das sich nur nicht auf die
    /// Zonengroesse bringen liess, nicht. Nur im ersten Fall wird nach Administratorrechten gefragt.
    /// </summary>
    private void ReportPlacement(nint windowHandle, PartMonitorCommandResult? result, string successMessage)
    {
        if (result?.Status == PartMonitorCommandStatus.Successful)
        {
            placedDuringDrag = true;
            log.Write("DEBUG", successMessage);
            return;
        }

        var reason = result?.Reason ?? result?.Status switch
        {
            PartMonitorCommandStatus.TargetMissing => "Die Zielzone gibt es nicht mehr.",
            PartMonitorCommandStatus.NotEligible => "Das Fenster lässt sich nicht lesen oder wurde geschlossen.",
            null => "Die Snap-Funktion ist nicht bereit.",
            _ => "Windows hat die Platzierung abgelehnt."
        };
        log.Write("WARN", $"Platzierung nicht gelungen: {reason}");
        viewModel.StatusMessage = $"Fenster nicht in die Zone gesetzt: {reason}";
        if (result?.Outcome?.WindowMoved != true)
        {
            OfferElevationIfWindowIsOutOfReach(windowHandle);
        }
    }

    /// <summary>
    /// Fuehrt ein Zonenkuerzel fuer das Vordergrundfenster aus. Die Zielrechnung liegt in
    /// <see cref="ZoneHotkeyNavigator"/>, das Setzen im selben Befehlsdienst wie beim Ziehen.
    /// </summary>
    private void HandleZoneHotkey(ZoneHotkey hotkey)
    {
        log.Write("DEBUG", $"Tastenkürzel {hotkey.Action} {hotkey.ZoneNumber}");
        if (coordinator is null || partMonitorCommands is null || emergencyStopped)
        {
            return;
        }

        if (windowService.GetForegroundWindow() is not { } foreground)
        {
            log.Write("DEBUG", "Tastenkürzel ohne geeignetes Vordergrundfenster.");
            viewModel.StatusMessage = "Kein geeignetes Fenster im Vordergrund.";
            return;
        }

        log.Write("DEBUG", $"Tastenkürzel für Vordergrundfenster 0x{foreground.Handle:X} bei {foreground.Bounds}");

        var metrics = new LayoutMetrics(configuration.Settings.EffectiveOuterMargins, configuration.Settings.ZoneGap);
        var command = ZoneHotkeyNavigator.Plan(hotkey, foreground.Handle, foreground.Bounds, BuildTargets(configuration), metrics);
        if (command is null)
        {
            viewModel.StatusMessage = hotkey.Action == ZoneHotkeyAction.ZoneByNumber
                ? $"Auf diesem Monitor gibt es keine Zone {hotkey.ZoneNumber}."
                : "Für dieses Fenster gibt es kein Ziel.";
            return;
        }

        var result = partMonitorCommands.Execute(command);
        log.Write("DEBUG", $"Tastenkürzel-Befehl {command.GetType().Name} für 0x{foreground.Handle:X} bei {foreground.Bounds}: {result.Status}, Ziel {result.Placement?.Bounds}, gemessen {result.Outcome?.ActualBounds}");
        if (result.Status == PartMonitorCommandStatus.NoPreviousPlacement)
        {
            viewModel.StatusMessage = "Für dieses Fenster ist keine vorherige Position bekannt.";
            return;
        }

        ReportPlacement(foreground.Handle, result, $"Tastenkürzel: Fenster in Zone gesetzt ({hotkey.Action}).");
        if (result.Status == PartMonitorCommandStatus.Successful && result.Placement is { } placement)
        {
            viewModel.StatusMessage = $"Fenster in Zone {ZoneNumberOf(placement)} gesetzt.";
        }
    }

    private string ZoneNumberOf(PartMonitorPlacement placement)
    {
        var target = BuildTargets(configuration).FirstOrDefault(candidate =>
            string.Equals(candidate.Monitor.Identity.StableId, placement.MonitorId, StringComparison.OrdinalIgnoreCase));
        var index = target?.PartMonitors.ToList().FindIndex(zone => zone.Id == placement.PartMonitorId) ?? -1;
        var name = target?.PartMonitors.ElementAtOrDefault(index)?.Name;
        return index < 0 ? "?" : name is null ? $"{index + 1}" : $"{index + 1} · {name}";
    }

    /// <summary>Zeigt den Entwurf aus dem Editor drei Sekunden lang auf dem echten Monitor.</summary>
    /// <summary>
    /// Zeigt die aktiven Layouts aller angeschlossenen Monitore drei Sekunden lang als Overlay, genau
    /// so, wie das Overlay sie beim Ziehen zeigen wird. Waehrend auf dem Monitor gezeichnet wird,
    /// bleibt die Vorschau aus; der Editor zeigt die Zonen dann ohnehin in echter Groesse.
    /// </summary>
    private void PreviewActiveLayouts()
    {
        if (window.IsFullscreenEditorOpen)
        {
            return;
        }

        previewTimer.Stop();
        var targets = BuildTargets(configuration);
        previewOverlays.UpdateTargets(targets);
        previewOverlays.Show(
            targets.Select(target => target.Monitor.Identity.StableId).ToArray(),
            new LayoutMetrics(configuration.Settings.EffectiveOuterMargins, configuration.Settings.ZoneGap),
            configuration.Settings.OverlayColor,
            configuration.Settings.OverlayOpacity,
            configuration.Settings.ShowZoneNames,
            OverlayStyle.From(configuration.Settings));
        previewTimer.Start();
        viewModel.StatusMessage = "Zonen werden drei Sekunden lang eingeblendet.";
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
            viewModel.StatusMessage = "Zone Manager läuft im Infobereich weiter";
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
        try
        {
            await ExitApplicationCoreAsync();
        }
        catch (Exception exception)
        {
            // Ohne diesen Fang blieb das Beenden-Tor nach einem Fehler dauerhaft geschlossen, und
            // «Beenden» im Infobereich reagierte nie wieder.
            log.Write("ERROR", "Das Beenden ist fehlgeschlagen; das Programm läuft weiter.", exception);
            shuttingDown = false;
            window.IsEnabled = true;
            exitRequestGate.Reset();
            viewModel.StatusMessage = $"Beenden fehlgeschlagen: {exception.Message}";
            try
            {
                Reconfigure(configuration);
            }
            catch (Exception reconfigureException)
            {
                log.Write("ERROR", "Die Snap-Funktion konnte nach dem gescheiterten Beenden nicht wieder aktiviert werden.", reconfigureException);
            }
        }
    }

    private async Task ExitApplicationCoreAsync()
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
        if (System.Windows.Application.Current is App application)
        {
            application.ShutdownSafely();
            return;
        }

        System.Windows.Application.Current.Shutdown();
    }
}
