namespace SnapZones.Core.Placement;

public enum WindowPlacementMode
{
    RememberLast,
    FixedZone,
    Exclude
}

public sealed record WindowPlacementRule(
    Guid Id,
    bool IsEnabled,
    string ApplicationKey,
    string? WindowClass,
    WindowKind? WindowKind,
    string? TitlePattern,
    WindowPlacementMode Action,
    Guid? ProfileId,
    string? MonitorStableId,
    Guid? ZoneId);

public sealed record RuleResolution(WindowPlacementRule? Rule, bool HasConflict);
