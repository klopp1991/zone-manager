using System.ComponentModel;
using System.Windows.Threading;
using SnapZones.App.Overlays;
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
    private readonly IConfigurationRepository repository;
    private readonly IReadOnlyList<LiveMonitor> monitors;
    private readonly IStartupService startupService;
    private readonly FileLog log;
    private readonly IWindowMoveHook moveHook;
    private readonly IGlobalHotkeyService hotkeys;
    private readonly IWindowService windowService;
    private readonly OverlayManager overlays;
    private readonly TrayIconService tray;
    private readonly ProfileChangedToast toast = new();
    private readonly DispatcherTimer cursorTimer;
    private readonly SemaphoreSlim saveGate = new(1, 1);
    private WindowDragCoordinator? coordinator;
    private SnapConfiguration configuration;
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
        this.repository = repository;
        this.monitors = monitors;
        this.startupService = startupService;
        this.log = log;
        configuration = viewModel.Configuration;
        overlays = new OverlayManager();
        windowService = new WindowsWindowService();
        moveHook = new WindowMoveHook(SynchronizationContext.Current ?? new DispatcherSynchronizationContext(window.Dispatcher));
        hotkeys = new GlobalHotkeyService();
        cursorTimer = new DispatcherTimer(DispatcherPriority.Input, window.Dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(33)
        };
        cursorTimer.Tick += CursorTimer_Tick;
        tray = new TrayIconService(window, ActivateProfile, ToggleSnapping, ExitApplication);

        viewModel.SaveRequested += SaveRequested;
        moveHook.MoveStarted += MoveStarted;
        moveHook.MoveEnded += _ => MoveEnded();
        moveHook.EmergencyStopped += reason => EmergencyStop(reason);
        hotkeys.ProfileRequested += ActivateProfile;
        hotkeys.EmergencyStopRequested += () => EmergencyStop("Not-Aus ausgelöst: Snap-Funktion deaktiviert");
        window.Closing += Window_Closing;

        Reconfigure(configuration);
    }

    public void EmergencyStop(string reason)
    {
        cursorTimer.Stop();
        coordinator?.Cancel();
        overlays.HideAll();
        moveHook.Disable();
        viewModel.DisableSnappingForSafety(reason);
        configuration = viewModel.Configuration;
        _ = hotkeys.Configure(QuickSlotRegistrationPlan.Build(configuration), emergencyStopEnabled: false);
        tray.Update(configuration);
        log.Write("WARN", reason);
        _ = PersistAsync(configuration, updateStartup: false);
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
        moveHook.Disable();
        overlays.HideAll();
        tray.Dispose();
        hotkeys.Dispose();
        moveHook.Dispose();
        overlays.Dispose();
        toast.Close();
        saveGate.Dispose();
    }

    private void SaveRequested(SnapConfiguration newConfiguration)
    {
        configuration = newConfiguration;
        Reconfigure(configuration);
        _ = PersistAsync(configuration, updateStartup: true);
    }

    private async Task PersistAsync(SnapConfiguration newConfiguration, bool updateStartup)
    {
        await saveGate.WaitAsync();
        try
        {
            if (updateStartup && startupService.IsEnabled != newConfiguration.Settings.StartWithWindows)
            {
                startupService.SetEnabled(newConfiguration.Settings.StartWithWindows);
            }

            await repository.SaveAsync(newConfiguration, CancellationToken.None);
        }
        catch (Exception exception)
        {
            log.Write("ERROR", "Konfiguration konnte nicht gespeichert werden.", exception);
            viewModel.StatusMessage = $"Speichern fehlgeschlagen: {exception.Message}";
        }
        finally
        {
            saveGate.Release();
        }
    }

    private void Reconfigure(SnapConfiguration newConfiguration)
    {
        cursorTimer.Stop();
        coordinator?.Cancel();
        moveHook.Disable();
        var active = newConfiguration.Profiles.Single(profile => profile.Id == newConfiguration.Settings.ActiveProfileId);
        var targets = BuildTargets(active);
        overlays.UpdateTargets(targets);
        coordinator = new WindowDragCoordinator(
            targets,
            new LayoutMetrics(newConfiguration.Settings.OuterMargin, newConfiguration.Settings.ZoneGap),
            newConfiguration.Settings.OverlayScope);
        coordinator.ActionRequested += HandleDragAction;

        var hotkeyResult = hotkeys.Configure(
            QuickSlotRegistrationPlan.Build(newConfiguration),
            newConfiguration.Settings.SnappingEnabled);
        if (hotkeyResult.Errors.Count > 0)
        {
            viewModel.StatusMessage = string.Join(" ", hotkeyResult.Errors);
        }

        if (newConfiguration.Settings.SnappingEnabled)
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
            return;
        }

        if (configuration.Settings.TriggerMode == TriggerMode.ShiftKey && !windowService.IsShiftPressed())
        {
            return;
        }

        var snapshot = windowService.Inspect(windowHandle, cursor, Environment.ProcessId);
        if (snapshot is null)
        {
            return;
        }

        coordinator.Start(windowHandle, snapshot, cursor);
        if (coordinator.State == DragState.Tracking)
        {
            cursorTimer.Start();
        }
    }

    private void MoveEnded()
    {
        cursorTimer.Stop();
        coordinator?.End();
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
                    new LayoutMetrics(configuration.Settings.OuterMargin, configuration.Settings.ZoneGap),
                    configuration.Settings.OverlayColor,
                    configuration.Settings.OverlayOpacity);
                break;
            case HighlightZoneAction highlight:
                overlays.Highlight(highlight.MonitorId, highlight.ZoneId);
                break;
            case HideOverlaysAction:
                overlays.HideAll();
                break;
            case SnapWindowAction snap:
                if (!windowService.TrySnap(snap.WindowHandle, snap.Bounds))
                {
                    log.Write("WARN", "Ein Fenster konnte nicht positioniert werden.");
                }
                break;
        }
    }

    private void ActivateProfile(Guid profileId)
    {
        viewModel.ActivateProfile(profileId);
        configuration = viewModel.Configuration;
        Reconfigure(configuration);
        toast.ShowProfile(viewModel.SelectedProfile.Name);
        _ = PersistAsync(configuration, updateStartup: false);
    }

    private void ToggleSnapping(bool enabled)
    {
        viewModel.Settings.SnappingEnabled = enabled;
        viewModel.Save();
    }

    private void Window_Closing(object? sender, CancelEventArgs eventArgs)
    {
        _ = sender;
        if (!allowClose)
        {
            eventArgs.Cancel = true;
            window.Hide();
            viewModel.StatusMessage = "SnapZones läuft im Infobereich weiter";
        }
    }

    private void ExitApplication()
    {
        allowClose = true;
        System.Windows.Application.Current.Shutdown();
    }
}
