using ZoneManager.Core.AppRules;

namespace ZoneManager.Windows.Hooks;

public interface IWindowRuleHook : IDisposable
{
    event Action<AppRuleEvent, nint>? RuleEvent;
    event Action<string>? EmergencyStopped;
    bool IsEnabled { get; }
    void Enable();
    void Disable();
}
