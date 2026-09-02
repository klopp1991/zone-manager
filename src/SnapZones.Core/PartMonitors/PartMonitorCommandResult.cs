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
    PartMonitorPlacement? Placement = null,
    PlacementOutcome? Outcome = null)
{
    /// <summary>Warum die Platzierung nicht gelungen ist, im Klartext, sofern bekannt.</summary>
    public string? Reason => Outcome?.Rejection;
}
