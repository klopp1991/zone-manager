using SnapZones.Core.AppRules;

namespace SnapZones.Windows.Windows;

public sealed record WindowRuleCandidate(
    nint WindowHandle,
    AppWindowIdentity Identity,
    bool IsFullscreen = false,
    bool IsMinimized = false);
