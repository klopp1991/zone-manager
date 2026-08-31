namespace ZoneManager.Core.AppRules;

public enum AppRuleEvent
{
    WindowCreated,
    WindowFocused,
    LayoutActivated
}

public sealed record AppRule(
    Guid Id,
    string ProcessPath,
    string? WindowTitlePattern,
    string? WindowClass,
    AppRuleEvent Event,
    int DelayMilliseconds,
    int RetryCount,
    int Priority,
    bool IsEnabled,
    Guid TargetLayoutId,
    Guid TargetZoneId);

public sealed record AppWindowIdentity(
    int ProcessId,
    string ProcessPath,
    string WindowTitle,
    string WindowClass);
