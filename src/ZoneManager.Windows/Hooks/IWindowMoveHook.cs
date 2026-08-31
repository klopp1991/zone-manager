namespace ZoneManager.Windows.Hooks;

public interface IWindowMoveHook : IDisposable
{
    event Action<nint>? MoveStarted;
    event Action<nint>? MoveEnded;
    event Action<string>? EmergencyStopped;

    bool IsEnabled { get; }
    void Enable();
    void Disable();
}
