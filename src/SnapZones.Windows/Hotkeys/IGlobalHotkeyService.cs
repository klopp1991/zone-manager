namespace SnapZones.Windows.Hotkeys;

public interface IGlobalHotkeyService : IDisposable
{
    event Action? EmergencyStopRequested;

    HotkeyRegistrationResult Configure(bool emergencyStopEnabled);
}

public sealed record HotkeyRegistrationResult(IReadOnlyList<string> Errors);
