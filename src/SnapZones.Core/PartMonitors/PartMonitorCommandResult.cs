namespace SnapZones.Core.PartMonitors;

public enum PartMonitorCommandStatus
{
    Successful,
    NotEligible,
    TargetMissing,
    WindowsRejected,
    NoPreviousPlacement
}

public sealed record PartMonitorCommandResult(
    PartMonitorCommandStatus Status,
    PartMonitorPlacement? Placement = null);
