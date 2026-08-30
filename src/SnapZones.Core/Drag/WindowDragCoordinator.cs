using SnapZones.Core.Geometry;
using SnapZones.Core.Models;

namespace SnapZones.Core.Drag;

public sealed class WindowDragCoordinator
{
    private readonly IReadOnlyList<DragMonitorTarget> targets;
    private readonly OverlayScope overlayScope;
    private nint windowHandle;
    private DragMonitorTarget? hoverTarget;
    private ZoneDefinition? hoverZone;

    public WindowDragCoordinator(
        IReadOnlyList<DragMonitorTarget> targets,
        OverlayScope overlayScope)
    {
        this.targets = targets;
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

        var activeTarget = FindTarget(cursor);
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

        var target = FindTarget(cursor);
        var zone = target is null
            ? null
            : ZoneGeometry.HitTest(target.Zones, target.Monitor.WorkArea, cursor);
        if (target == hoverTarget && zone == hoverZone)
        {
            return;
        }

        hoverTarget = target;
        hoverZone = zone;
        ActionRequested?.Invoke(new HighlightZoneAction(
            target?.Monitor.Identity.StableId,
            zone?.Id));
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
        if (hoverTarget is not null && hoverZone is not null)
        {
            var bounds = ZoneGeometry.ToPixels(hoverZone.Bounds, hoverTarget.Monitor.WorkArea);
            ActionRequested?.Invoke(new SnapWindowAction(windowHandle, bounds));
        }

        ResetState();
    }

    public void End(PointInt finalCursor)
    {
        Update(finalCursor);
        End();
    }

    private DragMonitorTarget? FindTarget(PointInt cursor) => targets.FirstOrDefault(target =>
        cursor.X >= target.Monitor.WorkArea.X &&
        cursor.X < target.Monitor.WorkArea.X + target.Monitor.WorkArea.Width &&
        cursor.Y >= target.Monitor.WorkArea.Y &&
        cursor.Y < target.Monitor.WorkArea.Y + target.Monitor.WorkArea.Height);

    private void ResetState()
    {
        State = DragState.Idle;
        windowHandle = 0;
        hoverTarget = null;
        hoverZone = null;
    }
}
