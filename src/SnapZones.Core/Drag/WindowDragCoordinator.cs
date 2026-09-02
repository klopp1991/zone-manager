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
    private string? shownMonitorId;

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

        // Der Ziehstart liegt oft ueber der Taskleiste, also ausserhalb jeder Arbeitsflaeche. Dann gilt
        // der naechstgelegene Monitor; frueher erschien in diesem Fall gar kein Overlay.
        var activeTarget = resolver.FindNearestMonitor(cursor);
        if (activeTarget is null)
        {
            return;
        }

        windowHandle = handle;
        State = DragState.Tracking;
        ShowOverlaysFor(activeTarget);
    }

    /// <summary>
    /// Zeigt die Zonen dort, wo der eingestellte Geltungsbereich sie verlangt. Auf allen Monitoren
    /// gleichzeitig, oder nur auf dem genannten; im zweiten Fall verschwinden sie auf allen uebrigen.
    /// </summary>
    private void ShowOverlaysFor(PartMonitorTarget target)
    {
        var visibleTargets = overlayScope == OverlayScope.AllMonitors
            ? targets
            : [target];
        shownMonitorId = target.Monitor.Identity.StableId;
        ActionRequested?.Invoke(new ShowOverlaysAction(
            visibleTargets.Select(candidate => candidate.Monitor.Identity.StableId).ToArray()));
    }

    /// <summary>
    /// Laesst die Zonen dem Mauszeiger ueber die Monitorgrenze folgen. Nur fuer
    /// <see cref="OverlayScope.CursorMonitor"/>; die uebrigen Geltungsbereiche bleiben unberuehrt.
    ///
    /// Liegt der Zeiger gerade auf keinem Monitor — etwa ueber der Taskleiste oder in der Luecke
    /// zwischen zwei unterschiedlich hohen Bildschirmen —, bleibt die bisherige Anzeige stehen. Sie in
    /// diesem Moment auszublenden und gleich wieder einzublenden ergaebe nur ein Flackern.
    /// </summary>
    private void FollowCursorAcrossMonitors(PointInt cursor)
    {
        if (overlayScope != OverlayScope.CursorMonitor)
        {
            return;
        }

        var target = resolver.FindPhysicalMonitor(cursor);
        if (target is null ||
            string.Equals(target.Monitor.Identity.StableId, shownMonitorId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        ShowOverlaysFor(target);
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

        FollowCursorAcrossMonitors(cursor);

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
        shownMonitorId = null;
        ClearSpan();
    }
}
