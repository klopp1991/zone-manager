using ZoneManager.Core.Geometry;
using ZoneManager.Core.Models;
using ZoneManager.Core.PartMonitors;

namespace ZoneManager.Core.Drag;

public sealed class WindowDragCoordinator
{
    private readonly IReadOnlyList<PartMonitorTarget> targets;
    private readonly PartMonitorResolver resolver;
    private readonly OverlayScope overlayScope;
    private nint windowHandle;
    private PartMonitorPlacement? hoverPlacement;

    public WindowDragCoordinator(
        IReadOnlyList<PartMonitorTarget> targets,
        LayoutMetrics metrics,
        OverlayScope overlayScope)
    {
        this.targets = targets;
        resolver = new PartMonitorResolver(targets, metrics);
        this.overlayScope = overlayScope;
    }

    public event Action<DragAction>? ActionRequested;

    public DragState State { get; private set; }

    public void Start(nint handle, WindowSnapshot snapshot, PointInt cursor)
    {
        if (State != DragState.Idle || !WindowCandidateEvaluator.IsEligible(snapshot))
        {
            return;
        }

        var activeTarget = resolver.FindPhysicalMonitor(cursor);
        if (activeTarget is null)
        {
            return;
        }

        windowHandle = handle;
        State = DragState.Tracking;
        var visibleTargets = overlayScope == OverlayScope.AllMonitors
            ? targets
            : [activeTarget];
        ActionRequested?.Invoke(new ShowOverlaysAction(
            visibleTargets.Select(target => target.Monitor.Identity.StableId).ToArray()));
    }

    public void Update(PointInt cursor)
    {
        if (State != DragState.Tracking)
        {
            return;
        }

        var placement = resolver.FindAt(cursor);
        if (placement == hoverPlacement)
        {
            return;
        }

        hoverPlacement = placement;
        ActionRequested?.Invoke(new HighlightZoneAction(
            placement?.MonitorId,
            placement?.PartMonitorId));
    }

    public void Cancel()
    {
        if (State == DragState.Idle)
        {
            return;
        }

        ActionRequested?.Invoke(new HideOverlaysAction());
        ResetState();
    }

    public void End()
    {
        if (State == DragState.Idle)
        {
            return;
        }

        ActionRequested?.Invoke(new HideOverlaysAction());
        if (hoverPlacement is not null)
        {
            ActionRequested?.Invoke(new FillPartMonitorAction(
                windowHandle,
                hoverPlacement.MonitorId,
                hoverPlacement.PartMonitorId));
        }

        ResetState();
    }

    public void End(PointInt finalCursor)
    {
        Update(finalCursor);
        End();
    }

    private void ResetState()
    {
        State = DragState.Idle;
        windowHandle = 0;
        hoverPlacement = null;
    }
}
