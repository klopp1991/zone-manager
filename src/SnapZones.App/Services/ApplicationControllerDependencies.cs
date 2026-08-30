using System.Windows.Threading;
using SnapZones.App.Overlays;
using SnapZones.App.Views;
using SnapZones.Core.Drag;
using SnapZones.Core.Geometry;
using SnapZones.Core.Models;
using SnapZones.Core.Monitors;
using SnapZones.Core.Persistence;
using SnapZones.Core.Placement;
using SnapZones.Windows.Hooks;
using SnapZones.Windows.Hotkeys;
using SnapZones.Windows.Windows;
using ControllerDragAction = SnapZones.Core.Drag.DragAction;

namespace SnapZones.App.Services;

public interface IConfigurationSaveCoordinator : IDisposable
{
    event Action<Exception?>? SaveFinished;
    void RequestSave(SnapConfiguration configuration);
    Task FlushAsync(CancellationToken cancellationToken);
}

public interface IApplicationOverlayService : IDisposable
{
    void UpdateTargets(IReadOnlyList<DragMonitorTarget> targets);
    void Show(
        IReadOnlyList<string> monitorIds,
        LayoutMetrics metrics,
        string colour,
        double opacity,
        bool showZoneNames);
    void Highlight(string? monitorId, Guid? zoneId);
    void HideAll();
}

public interface IApplicationTrayService : IDisposable
{
    void Update(SnapConfiguration configuration);
}

public interface IApplicationToastService : IDisposable
{
    void ShowProfile(string profileName);
}

public interface IWindowDragCoordinator : IDisposable
{
    event Action<ControllerDragAction>? ActionRequested;
    DragState State { get; }
    void Start(nint windowHandle, WindowSnapshot snapshot, PointInt cursor);
    void Update(PointInt cursor);
    void Cancel();
    void End();
    void End(PointInt finalCursor);
}

public interface IWindowDragCoordinatorFactory
{
    IWindowDragCoordinator Create(IReadOnlyList<DragMonitorTarget> targets, OverlayScope overlayScope);
}

public sealed record ApplicationControllerDependencies(
    IConfigurationSaveCoordinator ConfigurationSaveCoordinator,
    IWindowMoveHook MoveHook,
    IGlobalHotkeyService Hotkeys,
    IWindowService WindowService,
    IApplicationOverlayService Overlays,
    IApplicationTrayService Tray,
    IApplicationToastService Toast,
    IWindowDragCoordinatorFactory DragCoordinatorFactory,
    IWindowPlacementEngine PlacementEngine,
    IWindowLifecycleHook PlacementLifecycleHook,
    Action<string, string, Exception?> Log)
{
    public static ApplicationControllerDependencies CreateDefault(
        MainWindow window,
        IConfigurationRepository configurationRepository,
        IWindowPlacementRepository placementRepository,
        IReadOnlyList<LiveMonitor> monitors,
        Func<SnapConfiguration> configurationFactory,
        Action<Guid> activateProfile,
        Action<bool> toggleSnapping,
        Action requestExit,
        FileLog log)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(configurationRepository);
        ArgumentNullException.ThrowIfNull(placementRepository);
        ArgumentNullException.ThrowIfNull(monitors);
        ArgumentNullException.ThrowIfNull(configurationFactory);
        ArgumentNullException.ThrowIfNull(log);

        var synchronizationContext = SynchronizationContext.Current
            ?? new DispatcherSynchronizationContext(window.Dispatcher);
        var lifecycleHook = new WindowLifecycleHook(synchronizationContext);
        var placementSaveCoordinator = new WindowPlacementSaveCoordinator(
            placementRepository,
            TimeSpan.FromMilliseconds(250));
        var placementEngine = new WindowPlacementEngine(
            lifecycleHook,
            new WindowsPlacementWindowService(),
            placementSaveCoordinator,
            WindowPlacementCatalog.Empty,
            () => BuildPlacementEnvironment(configurationFactory(), monitors),
            Environment.ProcessId,
            message => log.Write("DEBUG", message));

        return new ApplicationControllerDependencies(
            new ConfigurationSaveCoordinatorAdapter(new ConfigurationSaveCoordinator(configurationRepository)),
            new WindowMoveHook(synchronizationContext, message => log.Write("DEBUG", message)),
            new GlobalHotkeyService(),
            new WindowsWindowService(),
            new OverlayServiceAdapter(new OverlayManager()),
            new TrayServiceAdapter(new TrayIconService(window, activateProfile, toggleSnapping, requestExit)),
            new ToastServiceAdapter(new ProfileChangedToast()),
            new WindowDragCoordinatorFactory(),
            placementEngine,
            lifecycleHook,
            log.Write);
    }

    private static PlacementEnvironment BuildPlacementEnvironment(
        SnapConfiguration configuration,
        IReadOnlyList<LiveMonitor> monitors)
    {
        var monitorTargets = monitors
            .Select(monitor => new PlacementMonitorTarget(
                monitor.Identity.StableId,
                monitor.WorkArea,
                monitor.IsPrimary))
            .ToArray();
        var metrics = new LayoutMetrics(
            configuration.Settings.EffectiveOuterMargins,
            configuration.Settings.ZoneGap);
        var zoneTargets = new List<PlacementZoneTarget>();
        foreach (var profile in configuration.Profiles)
        {
            foreach (var monitor in monitors)
            {
                var layout = profile.Monitors.FirstOrDefault(saved =>
                    string.Equals(saved.Monitor.StableId, monitor.Identity.StableId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(saved.Monitor.DeviceName, monitor.Identity.DeviceName, StringComparison.OrdinalIgnoreCase));
                if (layout is null)
                {
                    continue;
                }

                zoneTargets.AddRange(layout.Zones.Select(zone => new PlacementZoneTarget(
                    profile.Id,
                    zone.Id,
                    monitor.Identity.StableId,
                    ZoneGeometry.ToPixels(zone.Bounds, monitor.WorkArea, metrics))));
            }
        }

        return new PlacementEnvironment(configuration, monitorTargets, zoneTargets);
    }

    private sealed class ConfigurationSaveCoordinatorAdapter(ConfigurationSaveCoordinator coordinator)
        : IConfigurationSaveCoordinator
    {
        public event Action<Exception?>? SaveFinished
        {
            add => coordinator.SaveFinished += value;
            remove => coordinator.SaveFinished -= value;
        }

        public void RequestSave(SnapConfiguration configuration) => coordinator.RequestSave(configuration);
        public Task FlushAsync(CancellationToken cancellationToken) => coordinator.FlushAsync(cancellationToken);
        public void Dispose() { }
    }

    private sealed class OverlayServiceAdapter(OverlayManager overlays) : IApplicationOverlayService
    {
        public void UpdateTargets(IReadOnlyList<DragMonitorTarget> targets) => overlays.UpdateTargets(targets);
        public void Show(
            IReadOnlyList<string> monitorIds,
            LayoutMetrics metrics,
            string colour,
            double opacity,
            bool showZoneNames) => overlays.Show(monitorIds, metrics, colour, opacity, showZoneNames);
        public void Highlight(string? monitorId, Guid? zoneId) => overlays.Highlight(monitorId, zoneId);
        public void HideAll() => overlays.HideAll();
        public void Dispose() => overlays.Dispose();
    }

    private sealed class TrayServiceAdapter(TrayIconService tray) : IApplicationTrayService
    {
        public void Update(SnapConfiguration configuration) => tray.Update(configuration);
        public void Dispose() => tray.Dispose();
    }

    private sealed class ToastServiceAdapter(ProfileChangedToast toast) : IApplicationToastService
    {
        public void ShowProfile(string profileName) => toast.ShowProfile(profileName);
        public void Dispose() => toast.Close();
    }

    private sealed class WindowDragCoordinatorFactory : IWindowDragCoordinatorFactory
    {
        public IWindowDragCoordinator Create(IReadOnlyList<DragMonitorTarget> targets, OverlayScope overlayScope) =>
            new WindowDragCoordinatorAdapter(new WindowDragCoordinator(targets, overlayScope));
    }

    private sealed class WindowDragCoordinatorAdapter : IWindowDragCoordinator
    {
        private readonly WindowDragCoordinator coordinator;

        public WindowDragCoordinatorAdapter(WindowDragCoordinator coordinator)
        {
            this.coordinator = coordinator;
            coordinator.ActionRequested += ForwardAction;
        }

        public event Action<ControllerDragAction>? ActionRequested;
        public DragState State => coordinator.State;
        public void Start(nint windowHandle, WindowSnapshot snapshot, PointInt cursor) =>
            coordinator.Start(windowHandle, snapshot, cursor);
        public void Update(PointInt cursor) => coordinator.Update(cursor);
        public void Cancel() => coordinator.Cancel();
        public void End() => coordinator.End();
        public void End(PointInt finalCursor) => coordinator.End(finalCursor);
        public void Dispose()
        {
            coordinator.ActionRequested -= ForwardAction;
            ActionRequested = null;
        }

        private void ForwardAction(ControllerDragAction action) => ActionRequested?.Invoke(action);
    }
}
