namespace ZoneManager.Core.Drag;

public sealed record WindowSnapshot(
    bool IsVisible,
    bool IsChild,
    bool IsOwnProcess,
    bool IsToolWindow,
    bool IsCloaked,
    bool IsTitleBarDrag);
