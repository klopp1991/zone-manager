using SnapZones.Core.Geometry;

namespace SnapZones.Core.Drag;

public abstract record DragAction;

public sealed record ShowOverlaysAction(IReadOnlyList<string> MonitorIds) : DragAction;

public sealed record HighlightZoneAction(string? MonitorId, Guid? ZoneId) : DragAction;

/// <summary>
/// Hebt mehrere Zonen gleichzeitig hervor, waehrend ein Fenster mit gedrueckter Strg-Taste ueber sie
/// gezogen wird. Die Reihenfolge ist die des Ueberstreichens.
/// </summary>
public sealed record HighlightZoneSpanAction(string MonitorId, IReadOnlyList<Guid> ZoneIds) : DragAction;

public sealed record HideOverlaysAction : DragAction;

public sealed record FillPartMonitorAction(
    nint WindowHandle,
    string MonitorId,
    Guid PartMonitorId) : DragAction;

public sealed record FillPartMonitorSpanAction(
    nint WindowHandle,
    string MonitorId,
    IReadOnlyList<Guid> PartMonitorIds) : DragAction;
