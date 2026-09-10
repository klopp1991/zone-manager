using System.Windows.Interop;
using SnapZones.Core.Models;
using SnapZones.Core.PartMonitors;
using SnapZones.Windows.Native;

namespace SnapZones.Windows.Hotkeys;

/// <summary>
/// Registriert die festen Tastenkuerzel des Programms:
/// <list type="bullet">
/// <item>Not-Aus: Einrasten anhalten und wieder starten; die Kombination ist waehlbar.</item>
/// <item>Ctrl + Alt + Links / Rechts: Vordergrundfenster eine Zone zurueck oder weiter.</item>
/// <item>Ctrl + Alt + 1 bis 9: Vordergrundfenster in die Zone mit dieser Nummer auf seinem Monitor.</item>
/// <item>Ctrl + Alt + Ruecktaste: Vordergrundfenster zurueck an die Stelle vor dem letzten Einrasten.</item>
/// </list>
/// Voreingestellt ist Ctrl + Shift. Ctrl + Alt waere naheliegender, weil Windows die Win-Kombinationen
/// mit Pfeiltasten und Ziffern selbst belegt — es ist aber unbrauchbar: Windows liefert AltGr intern als
/// Ctrl + Alt, sodass <c>RegisterHotKey</c> mit diesen Zusatztasten jedes AltGr-Zeichen auf derselben
/// Taste verschluckt. Auf einer Schweizer Tastatur sind das unter anderem @ (AltGr + 2), # (AltGr + 3)
/// und | (AltGr + 7). Ein Ausweichen im Nachhinein gibt es nicht: sobald das Kuerzel feuert, ist das
/// Zeichen weg. Ctrl + Alt bleibt waehlbar, aber mit Warnung in den Einstellungen.
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
    private const uint VirtualKeyF11 = 0x7A;
    private const uint VirtualKeyF12 = 0x7B;
    private const uint VirtualKeyPause = 0x13;
    private const uint VirtualKeyOne = 0x31;
    private readonly HashSet<int> registeredIds = [];
    private HwndSource? source;

    public event Action? EmergencyStopRequested;

    public event Action<ZoneHotkey>? ZoneHotkeyPressed;

    public HotkeyRegistrationResult Configure(
        bool emergencyStopEnabled,
        bool zoneHotkeysEnabled,
        ZoneHotkeyModifiers modifiers,
        EmergencyHotkey emergencyHotkey)
    {
        EnsureSource();
        UnregisterAll();
        var errors = new List<string>();

        if (emergencyStopEnabled && EmergencyKey(emergencyHotkey) is { } key)
        {
            Register(EmergencyId, Control | Alt | Shift, key, EmergencyLabel(emergencyHotkey), errors);
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

    /// <summary>Die Taste des Not-Aus, oder <c>null</c>, wenn er ausgeschaltet ist.</summary>
    public static uint? EmergencyKey(EmergencyHotkey hotkey) => hotkey switch
    {
        EmergencyHotkey.ControlAltShiftF11 => VirtualKeyF11,
        EmergencyHotkey.ControlAltShiftPause => VirtualKeyPause,
        EmergencyHotkey.Off => null,
        _ => VirtualKeyF12
    };

    /// <summary>Der Not-Aus in Tastennamen, fuer Meldungen und die Tastenkappe.</summary>
    public static string EmergencyLabel(EmergencyHotkey hotkey) => hotkey switch
    {
        EmergencyHotkey.ControlAltShiftF11 => "Ctrl + Alt + Shift + F11",
        EmergencyHotkey.ControlAltShiftPause => "Ctrl + Alt + Shift + Pause",
        EmergencyHotkey.Off => "kein Kürzel",
        _ => "Ctrl + Alt + Shift + F12"
    };

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
