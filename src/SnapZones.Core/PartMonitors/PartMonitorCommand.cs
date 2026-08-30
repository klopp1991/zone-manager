namespace SnapZones.Core.PartMonitors;

public abstract record PartMonitorCommand(nint WindowHandle);

public sealed record FillPartMonitorCommand(
    nint WindowHandle,
    string MonitorId,
    Guid PartMonitorId) : PartMonitorCommand(WindowHandle);

public sealed record CyclePartMonitorCommand(
    nint WindowHandle,
    string CurrentMonitorId,
    Guid CurrentPartMonitorId,
    int Offset) : PartMonitorCommand(WindowHandle);

public sealed record RestorePreviousPlacementCommand(
    nint WindowHandle) : PartMonitorCommand(WindowHandle);
