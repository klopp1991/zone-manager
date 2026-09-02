using System.Windows.Interop;
using SnapZones.Core.Models;
using SnapZones.Core.PartMonitors;
using SnapZones.Windows.Native;

namespace SnapZones.Windows.Hotkeys;

/// <summary>
/// Registriert die festen Tastenkuerzel des Programms:
/// <list type="bullet">
/// <item>Ctrl + Alt + Shift + F12: Einrasten anhalten und wieder starten.</item>
/// <item>Ctrl + Alt + Links / Rechts: Vordergrundfenster eine Zone zurueck oder weiter.</item>
/// <item>Ctrl + Alt + 1 bis 9: Vordergrundfenster in die Zone mit dieser Nummer auf seinem Monitor.</item>
/// <item>Ctrl + Alt + Ruecktaste: Vordergrundfenster zurueck an die Stelle vor dem letzten Einrasten.</item>
/// </list>
/// Ctrl + Alt ist gewaehlt, weil Windows die Win-Kombinationen mit Pfeiltasten und Ziffern selbst belegt.
/// </summary>
public sealed class GlobalHotkeyService : IGlobalHotkeyService
{
    private const int HotkeyMessage = 0x0312;
    private const int EmergencyId = 999;
    private const int PreviousZoneId = 1001;
    private const int NextZoneId = 1002;
    private const int RestoreId = 1003;
    private const int FirstZoneNumberId = 1011;
    private const uint Alt = 0x0001;
    private const uint Control = 0x0002;
    private const uint Shift = 0x0004;
    private const uint Win = 0x0008;
    private const uint NoRepeat = 0x4000;
    private const uint VirtualKeyBackspace = 0x08;
    private const uint VirtualKeyLeft = 0x25;
    private const uint VirtualKeyRight = 0x27;
    private const uint VirtualKeyF12 = 0x7B;
    private const uint VirtualKeyOne = 0x31;
    private readonly HashSet<int> registeredIds = [];
    private HwndSource? source;

    public event Action? EmergencyStopRequested;

    public event Action<ZoneHotkey>? ZoneHotkeyPressed;

    public HotkeyRegistrationResult Configure(bool emergencyStopEnabled, bool zoneHotkeysEnabled, ZoneHotkeyModifiers modifiers)
    {
        EnsureSource();
        UnregisterAll();
        var errors = new List<string>();

        if (emergencyStopEnabled)
        {
            Register(EmergencyId, Control | Alt | Shift, VirtualKeyF12, "Ctrl + Alt + Shift + F12", errors);
        }

        if (zoneHotkeysEnabled)
        {
            var flags = ModifierFlags(modifiers);
            var label = ModifierLabel(modifiers);
            Register(PreviousZoneId, flags, VirtualKeyLeft, $"{label} + Links", errors);
            Register(NextZoneId, flags, VirtualKeyRight, $"{label} + Rechts", errors);
            Register(RestoreId, flags, VirtualKeyBackspace, $"{label} + Rücktaste", errors);
            for (var number = 1; number <= 9; number++)
            {
                Register(FirstZoneNumberId + number - 1, flags, VirtualKeyOne + (uint)number - 1, $"{label} + {number}", errors);
            }
        }

        return new HotkeyRegistrationResult(errors);
    }

    /// <summary>Die Win32-Modifikatorbits zu einer Auswahl. Oeffentlich, damit die Zuordnung pruefbar ist.</summary>
    public static uint ModifierFlags(ZoneHotkeyModifiers modifiers) => modifiers switch
    {
        ZoneHotkeyModifiers.ControlShift => Control | Shift,
        ZoneHotkeyModifiers.AltShift => Alt | Shift,
        ZoneHotkeyModifiers.ControlWin => Control | Win,
        _ => Control | Alt
    };

    public static string ModifierLabel(ZoneHotkeyModifiers modifiers) => modifiers switch
    {
        ZoneHotkeyModifiers.ControlShift => "Ctrl + Shift",
        ZoneHotkeyModifiers.AltShift => "Alt + Shift",
        ZoneHotkeyModifiers.ControlWin => "Ctrl + Win",
        _ => "Ctrl + Alt"
    };

    public void Dispose()
    {
        UnregisterAll();
        if (source is not null)
        {
            source.RemoveHook(ProcessMessage);
            source.Dispose();
            source = null;
        }
    }

    private void Register(int id, uint modifiers, uint virtualKey, string label, List<string> errors)
    {
        if (User32.RegisterHotKey(source!.Handle, id, modifiers | NoRepeat, virtualKey))
        {
            registeredIds.Add(id);
        }
        else
        {
            errors.Add($"Das Tastenkürzel {label} ist bereits belegt.");
        }
    }

    private void EnsureSource()
    {
        if (source is not null)
        {
            return;
        }

        source = new HwndSource(new HwndSourceParameters("SnapZones.Hotkeys")
        {
            Width = 0,
            Height = 0,
            WindowStyle = unchecked((int)0x80000000)
        });
        source.AddHook(ProcessMessage);
    }

    private nint ProcessMessage(nint window, int message, nint wParam, nint lParam, ref bool handled)
    {
        _ = window;
        _ = lParam;
        if (message != HotkeyMessage)
        {
            return 0;
        }

        var id = wParam.ToInt32();
        handled = true;
        switch (id)
        {
            case EmergencyId:
                EmergencyStopRequested?.Invoke();
                break;
            case PreviousZoneId:
                ZoneHotkeyPressed?.Invoke(new ZoneHotkey(ZoneHotkeyAction.PreviousZone));
                break;
            case NextZoneId:
                ZoneHotkeyPressed?.Invoke(new ZoneHotkey(ZoneHotkeyAction.NextZone));
                break;
            case RestoreId:
                ZoneHotkeyPressed?.Invoke(new ZoneHotkey(ZoneHotkeyAction.RestorePrevious));
                break;
            case >= FirstZoneNumberId and < FirstZoneNumberId + 9:
                ZoneHotkeyPressed?.Invoke(new ZoneHotkey(ZoneHotkeyAction.ZoneByNumber, id - FirstZoneNumberId + 1));
                break;
            default:
                break;
        }

        return 0;
    }

    private void UnregisterAll()
    {
        if (source is not null)
        {
            foreach (var id in registeredIds)
            {
                _ = User32.UnregisterHotKey(source.Handle, id);
            }
        }

        registeredIds.Clear();
    }
}
