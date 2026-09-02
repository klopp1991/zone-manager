namespace SnapZones.Windows.Hooks;

public enum WindowLifecycleEventKind
{
    Shown,
    Hidden,
    Destroyed,
    LocationChanged,
    MoveSizeEnded,
    MinimizeEnded,
    Focused
}

public sealed record WindowLifecycleEvent(nint WindowHandle, WindowLifecycleEventKind Kind);

public interface IWindowLifecycleHook : IDisposable
{
    event Action<WindowLifecycleEvent>? EventReceived;
    event Action<string>? EmergencyStopped;

    bool IsEnabled { get; }
    void Enable();
    void Disable();
}
