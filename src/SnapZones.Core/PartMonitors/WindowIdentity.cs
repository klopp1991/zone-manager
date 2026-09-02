namespace SnapZones.Core.PartMonitors;

public readonly record struct WindowIdentity(
    nint Handle,
    uint ProcessId,
    string WindowClass);
