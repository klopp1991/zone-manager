using SnapZones.Core.Profiles;

namespace SnapZones.Windows.Hotkeys;

public interface IGlobalHotkeyService : IDisposable
{
    event Action<Guid>? ProfileRequested;
    event Action? EmergencyStopRequested;

    HotkeyRegistrationResult Configure(QuickSlotRegistrationPlanResult plan, bool emergencyStopEnabled);
}

public sealed record HotkeyRegistrationResult(IReadOnlyList<string> Errors);
