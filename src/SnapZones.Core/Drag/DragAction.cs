using SnapZones.Core.Geometry;

namespace SnapZones.Core.Drag;

public abstract record DragAction;

public sealed record ShowOverlaysAction(IReadOnlyList<string> MonitorIds) : DragAction;

/// <summary>
/// Zones to highlight on one monitor. More than one identifier means the user
/// is spanning the drag across several zones; the window will be placed in the
/// rectangle enclosing them.
/// </summary>
public sealed record HighlightZoneAction(string? MonitorId, IReadOnlyList<Guid> ZoneIds) : DragAction;

public sealed record HideOverlaysAction : DragAction;

public sealed record SnapWindowAction(nint WindowHandle, PixelRect Bounds) : DragAction;
