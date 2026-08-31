using SnapZones.Core.AppRules;
using SnapZones.Core.Geometry;
using SnapZones.Core.Models;
using SnapZones.Core.PartMonitors;

namespace SnapZones.Core.Drag;

public sealed class WindowDragCoordinator
{
    private readonly IReadOnlyList<PartMonitorTarget> targets;
    private readonly PartMonitorResolver resolver;
    private readonly OverlayScope overlayScope;
    private readonly IReadOnlyList<AppExclusion> exclusions;
    private readonly List<Guid> spannedZoneIds = [];
    private nint windowHandle;
    private PartMonitorPlacement? hoverPlacement;
    private string? spanMonitorId;

    public WindowDragCoordinator(
        IReadOnlyList<PartMonitorTarget> targets,
        LayoutMetrics metrics,
        OverlayScope overlayScope,
        IReadOnlyList<AppExclusion>? exclusions = null)
    {
        this.targets = targets;
        resolver = new PartMonitorResolver(targets, metrics);
        this.overlayScope = overlayScope;
        this.exclusions = exclusions ?? [];
    }

    public event Action<DragAction>? ActionRequested;

    public DragState State { get; private set; }

    public void Start(nint handle, WindowSnapshot snapshot, PointInt cursor)
    {
        if (State != DragState.Idle || !WindowCandidateEvaluator.IsEligible(snapshot, exclusions))
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

    public void Update(PointInt cursor) => Update(cursor, spanRequested: false);

    /// <summary>
    /// Verfolgt den Zeiger waehrend des Ziehens. Ist <paramref name="spanRequested"/> gesetzt, sammeln
    /// sich die ueberstrichenen Zonen desselben Monitors auf, statt einander abzuloesen; das Fenster
    /// belegt beim Loslassen deren gemeinsame Flaeche. Wird die Taste wieder losgelassen, faellt die
    /// Auswahl auf die Zone unter dem Zeiger zurueck.
    /// </summary>
    public void Update(PointInt cursor, bool spanRequested)
    {
        if (State != DragState.Tracking)
        {
            return;
        }

        var placement = resolver.FindAt(cursor);
        if (spanRequested && placement is not null)
        {
            AddToSpan(placement);
            return;
        }

        if (!spanRequested && spannedZoneIds.Count > 0)
        {
            ClearSpan();
        }
        else if (placement == hoverPlacement)
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
        if (spanMonitorId is { } monitorId && spannedZoneIds.Count > 1)
        {
            ActionRequested?.Invoke(new FillPartMonitorSpanAction(
                windowHandle,
                monitorId,
                spannedZoneIds.ToArray()));
        }
        else if (hoverPlacement is not null)
        {
            ActionRequested?.Invoke(new FillPartMonitorAction(
                windowHandle,
                hoverPlacement.MonitorId,
                hoverPlacement.PartMonitorId));
        }

        ResetState();
    }

    public void End(PointInt finalCursor) => End(finalCursor, spanRequested: false);

    public void End(PointInt finalCursor, bool spanRequested)
    {
        Update(finalCursor, spanRequested);
        End();
    }

    private void AddToSpan(PartMonitorPlacement placement)
    {
        // Eine Auswahl bleibt auf einen Monitor beschraenkt. Ein Fenster ueber zwei Bildschirme zu
        // spannen ergaebe eine Huellbox, die den Zwischenraum und fremde Zonen mit einschliesst.
        if (spanMonitorId is not null &&
            !string.Equals(spanMonitorId, placement.MonitorId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        hoverPlacement = placement;
        if (spannedZoneIds.Contains(placement.PartMonitorId))
        {
            return;
        }

        spanMonitorId = placement.MonitorId;
        spannedZoneIds.Add(placement.PartMonitorId);
        ActionRequested?.Invoke(new HighlightZoneSpanAction(spanMonitorId, spannedZoneIds.ToArray()));
    }

    private void ClearSpan()
    {
        spannedZoneIds.Clear();
        spanMonitorId = null;
    }

    private void ResetState()
    {
        State = DragState.Idle;
        windowHandle = 0;
        hoverPlacement = null;
        ClearSpan();
    }
}
