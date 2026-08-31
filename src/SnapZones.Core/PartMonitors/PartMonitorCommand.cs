namespace SnapZones.Core.PartMonitors;

public abstract record PartMonitorCommand(nint WindowHandle);

public sealed record FillPartMonitorCommand(
    nint WindowHandle,
    string MonitorId,
    Guid PartMonitorId) : PartMonitorCommand(WindowHandle);

/// <summary>
/// Ein Fenster ueber mehrere Zonen desselben Monitors legen. Ziel ist die Huellbox dieser Zonen.
/// </summary>
public sealed record FillPartMonitorSpanCommand(
    nint WindowHandle,
    string MonitorId,
    IReadOnlyList<Guid> PartMonitorIds) : PartMonitorCommand(WindowHandle);

public sealed record CyclePartMonitorCommand(
    nint WindowHandle,
    string CurrentMonitorId,
    Guid CurrentPartMonitorId,
    int Offset) : PartMonitorCommand(WindowHandle);

public sealed record RestorePreviousPlacementCommand(
    nint WindowHandle) : PartMonitorCommand(WindowHandle);
