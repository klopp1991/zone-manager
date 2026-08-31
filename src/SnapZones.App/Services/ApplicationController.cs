using System.ComponentModel;
using System.IO;
using System.Windows.Threading;
using SnapZones.App.Overlays;
using SnapZones.App.ViewModels;
using SnapZones.App.Views;
using SnapZones.Core.Drag;
using SnapZones.Core.Layouts;
using SnapZones.Core.Models;
using SnapZones.Core.Monitors;
using SnapZones.Core.Persistence;
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
    private readonly IGlobalHotkeyService hotkeys;
    private readonly IWindowService windowService;
    private readonly OverlayManager overlays;
    private readonly MonitorIdentificationOverlay monitorIdentification;
    private readonly TrayIconService tray;
    private readonly LayoutChangedToast toast = new();
    private readonly DispatcherTimer cursorTimer;
    private readonly DispatcherTimer identificationTimer;
    private readonly ConfigurationSaveCoordinator saveCoordinator;
    private readonly ConfigurationTransferService transferService = new();
    private WindowDragCoordinator? coordinator;
    private SnapConfiguration configuration;
    private int exitRequested;
    private bool emergencyStopped;
    private bool allowClose;
    private bool disposed;

    public ApplicationController(
        MainWindow window,
        MainViewModel viewModel,
        IConfigurationRepository repository,
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
        saveCoordinator.SaveFinished += SaveFinished;
        configuration = viewModel.Configuration;
        overlays = new OverlayManager();
        monitorIdentification = new MonitorIdentificationOverlay();
        windowService = new WindowsWindowService();
        moveHook = new WindowMoveHook(
            SynchronizationContext.Current ?? new DispatcherSynchronizationContext(window.Dispatcher),
            message => log.Write("DEBUG", message));
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
        overlays.HideAll();
        tray.Dispose();
        hotkeys.Dispose();
        moveHook.Dispose();
        overlays.Dispose();
        monitorIdentification.Dispose();
        toast.Close();
        saveCoordinator.SaveFinished -= SaveFinished;
        window.ExportConfigurationRequested -= ExportConfigurationAsync;
        window.ImportConfigurationRequested -= ImportConfigurationAsync;
        window.IdentifyMonitorsRequested -= IdentifyMonitors;
    }

    private void SaveRequested(SnapConfiguration newConfiguration)
    {
        configuration = newConfiguration;
        Reconfigure(configuration);
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

    public Task FlushAsync(CancellationToken cancellationToken) =>
        saveCoordinator.FlushAsync(cancellationToken);

    public void RequestExit()
    {
        if (Interlocked.Exchange(ref exitRequested, 1) != 0)
        {
            return;
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

    private void Reconfigure(SnapConfiguration newConfiguration)
    {
        cursorTimer.Stop();
        coordinator?.Cancel();
        moveHook.Disable();
        var targets = BuildTargets(newConfiguration);
        overlays.UpdateTargets(targets);
        coordinator = new WindowDragCoordinator(
            targets,
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
            }
            catch (Exception exception)
            {
                EmergencyStop($"Hook-Aktivierung fehlgeschlagen: {exception.Message}");
                return;
            }
        }

        tray.Update(newConfiguration);
    }

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

    private IReadOnlyList<DragMonitorTarget> BuildTargets(SnapConfiguration currentConfiguration)
    {
        var result = new List<DragMonitorTarget>();
        foreach (var monitor in monitors)
        {
            var layout = currentConfiguration.Layouts.FirstOrDefault(saved =>
                saved.IsActive && LayoutService.BelongsToMonitor(saved.Monitor, monitor.Identity));
            var zones = layout?.Zones ?? [new ZoneDefinition(Guid.NewGuid(), "Voll", NormalizedRect.Full)];
            result.Add(new DragMonitorTarget(monitor, zones));
        }

        return result;
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
            coordinator?.End(cursor, windowService.IsSpanModifierPressed());
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
            coordinator?.Update(cursor, windowService.IsSpanModifierPressed());
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
                overlays.Highlight(highlight.MonitorId, highlight.ZoneIds);
                break;
            case HideOverlaysAction:
                overlays.HideAll();
                break;
            case SnapWindowAction snap:
                if (windowService.TrySnap(snap.WindowHandle, snap.Bounds))
                {
                    log.Write("DEBUG", $"Fenster 0x{snap.WindowHandle:X} eingerastet: {snap.Bounds}.");
                }
                else
                {
                    log.Write("WARN", "Ein Fenster konnte nicht positioniert werden.");
                }
                break;
        }
    }

    private void ActivateLayout(Guid layoutId)
    {
        var layout = configuration.Layouts.Single(candidate => candidate.Id == layoutId);
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
            viewModel.Save();
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await saveCoordinator.FlushAsync(cancellation.Token);
        }
        catch (Exception exception)
        {
            log.Write("ERROR", "Die Applikation wird ohne abschliessend bestätigte Speicherung beendet.", exception);
        }

        allowClose = true;
        System.Windows.Application.Current.Shutdown();
    }
}
