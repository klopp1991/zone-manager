using SnapZones.Core.Geometry;

namespace SnapZones.Core.Drag;

public abstract record DragAction;

public sealed record ShowOverlaysAction(IReadOnlyList<string> MonitorIds) : DragAction;

public sealed record HighlightZoneAction(string? MonitorId, Guid? ZoneId) : DragAction;

public sealed record HideOverlaysAction : DragAction;

public sealed record SnapWindowAction(nint WindowHandle, PixelRect Bounds) : DragAction;
