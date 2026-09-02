using SnapZones.Core.Models;
using SnapZones.Core.PartMonitors;

namespace SnapZones.Windows.Hotkeys;

public interface IGlobalHotkeyService : IDisposable
{
    /// <summary>Der Not-Aus (Ctrl + Alt + Shift + F12); schaltet das Einrasten aus und wieder ein.</summary>
    event Action? EmergencyStopRequested;

    /// <summary>Ein Zonenkuerzel fuer das Vordergrundfenster, siehe <see cref="ZoneHotkeyAction"/>.</summary>
    event Action<ZoneHotkey>? ZoneHotkeyPressed;

    HotkeyRegistrationResult Configure(bool emergencyStopEnabled) => Configure(emergencyStopEnabled, emergencyStopEnabled);

    /// <param name="emergencyStopEnabled">Ob der Not-Aus registriert wird.</param>
    /// <param name="zoneHotkeysEnabled">Ob die Zonenkuerzel registriert werden; nur solange das Einrasten laeuft.</param>
    HotkeyRegistrationResult Configure(bool emergencyStopEnabled, bool zoneHotkeysEnabled) =>
        Configure(emergencyStopEnabled, zoneHotkeysEnabled, ZoneHotkeyModifiers.ControlAlt);

    /// <param name="modifiers">Die Zusatztasten der Zonenkuerzel; der Not-Aus bleibt bei Ctrl + Alt + Shift + F12.</param>
    HotkeyRegistrationResult Configure(bool emergencyStopEnabled, bool zoneHotkeysEnabled, ZoneHotkeyModifiers modifiers);
}

public sealed record HotkeyRegistrationResult(IReadOnlyList<string> Errors);
