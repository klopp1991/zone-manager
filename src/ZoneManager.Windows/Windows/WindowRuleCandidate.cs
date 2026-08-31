using ZoneManager.Core.AppRules;

namespace ZoneManager.Windows.Windows;

public sealed record WindowRuleCandidate(nint WindowHandle, AppWindowIdentity Identity);
