namespace SnapZones.Windows.Hooks;

public interface IWindowMoveHook : IDisposable
{
    event Action<nint>? MoveStarted;
    event Action<nint>? MoveEnded;
    event Action<string>? EmergencyStopped;

    bool IsEnabled { get; }
    void Enable();
    void Disable();

    /// <summary>Wie viele Ereignisse in zehn Sekunden der Schutzschalter zulaesst. Vorgabe: keine Aenderung.</summary>
    void SetEventLimit(int maximumEvents)
    {
    }
}
