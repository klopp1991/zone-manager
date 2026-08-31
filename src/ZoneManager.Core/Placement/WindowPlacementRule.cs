namespace ZoneManager.Core.Placement;

public enum WindowPlacementMode
{
    RememberLast,
    FixedZone,
    Exclude
}

public enum WindowPlacementTrigger
{
    WindowCreated,
    WindowFocused,
    ProfileActivated
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
    Guid? ZoneId,
    WindowPlacementTrigger Trigger = WindowPlacementTrigger.WindowCreated,
    int DelayMilliseconds = 0);

public sealed record RuleResolution(WindowPlacementRule? Rule, bool HasConflict);
