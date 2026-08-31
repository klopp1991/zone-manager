using SnapZones.Core.Geometry;
using SnapZones.Core.Models;

namespace SnapZones.Core.Drag;

public sealed class WindowDragCoordinator
{
    private readonly IReadOnlyList<DragMonitorTarget> targets;
    private readonly OverlayScope overlayScope;
    private readonly List<ZoneDefinition> selectedZones = [];
    private nint windowHandle;
    private DragMonitorTarget? hoverTarget;

    public WindowDragCoordinator(
        IReadOnlyList<DragMonitorTarget> targets,
        OverlayScope overlayScope)
    {
        this.targets = targets;
        this.overlayScope = overlayScope;
    }

    public event Action<DragAction>? ActionRequested;

    public DragState State { get; private set; }

    /// <summary>Zones the window would be placed in if the drag ended now.</summary>
    public IReadOnlyList<ZoneDefinition> SelectedZones => selectedZones;

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

    public void Update(PointInt cursor) => Update(cursor, spanRequested: false);

    /// <summary>
    /// Tracks the cursor during a drag.
    /// </summary>
    /// <param name="cursor">Current cursor position in virtual desktop pixels.</param>
    /// <param name="spanRequested">
    /// True while the user holds the span modifier. The hovered zone is then
    /// added to the selection instead of replacing it, and the window is placed
    /// in the rectangle enclosing all selected zones. A span never reaches
    /// across monitors: hovering another monitor starts a new selection there.
    /// </param>
    public void Update(PointInt cursor, bool spanRequested)
    {
        if (State != DragState.Tracking)
        {
            return;
        }

        var target = FindTarget(cursor);
        var zone = target is null
            ? null
            : ZoneGeometry.HitTest(target.Zones, target.Monitor.WorkArea, cursor);

        var previousTarget = hoverTarget;
        var previousSelection = selectedZones.ToArray();

        // A span stays on one monitor. Leaving it discards the selection.
        var keepSelection = spanRequested && target is not null && target == previousTarget;
        if (!keepSelection)
        {
            selectedZones.Clear();
        }

        hoverTarget = target;

        if (zone is not null && !selectedZones.Any(selected => selected.Id == zone.Id))
        {
            selectedZones.Add(zone);
        }

        if (target == previousTarget && SameZones(previousSelection, selectedZones))
        {
            return;
        }

        ActionRequested?.Invoke(new HighlightZoneAction(
            target?.Monitor.Identity.StableId,
            selectedZones.Select(selected => selected.Id).ToArray()));
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
        if (hoverTarget is not null && selectedZones.Count > 0)
        {
            var bounds = ZoneSpan.BoundingBox(selectedZones, hoverTarget.Monitor.WorkArea);
            ActionRequested?.Invoke(new SnapWindowAction(windowHandle, bounds));
        }

        ResetState();
    }

    public void End(PointInt finalCursor) => End(finalCursor, spanRequested: false);

    public void End(PointInt finalCursor, bool spanRequested)
    {
        Update(finalCursor, spanRequested);
        End();
    }

    private static bool SameZones(IReadOnlyList<ZoneDefinition> first, IReadOnlyList<ZoneDefinition> second)
    {
        if (first.Count != second.Count)
        {
            return false;
        }

        for (var index = 0; index < first.Count; index++)
        {
            if (first[index].Id != second[index].Id)
            {
                return false;
            }
        }

        return true;
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
        selectedZones.Clear();
    }
}
