using System.Windows.Interop;
using SnapZones.Windows.Hotkeys;

namespace SnapZones.App.Services;

public sealed class GlobalHotkeyService : IGlobalHotkeyService
{
    private const int EmergencyId = 999;
    private const uint VirtualKeyF12 = 0x7B;

    private const HotkeyModifiers EmergencyModifiers =
        HotkeyModifiers.Control | HotkeyModifiers.Alt | HotkeyModifiers.Shift | HotkeyModifiers.NoRepeat;

    private readonly HashSet<int> registeredIds = [];
    private HwndSource? source;

    public event Action? EmergencyStopRequested;

    public HotkeyRegistrationResult Configure(bool emergencyStopEnabled)
    {
        EnsureSource();
        UnregisterAll();
        var errors = new List<string>();

        if (emergencyStopEnabled)
        {
            if (HotkeyRegistrar.Register(source!.Handle, EmergencyId, EmergencyModifiers, VirtualKeyF12))
            {
                registeredIds.Add(EmergencyId);
            }
            else
            {
                errors.Add("Der Not-Aus-Hotkey Ctrl + Alt + Shift + F12 ist bereits belegt.");
            }
        }

        return new HotkeyRegistrationResult(errors);
    }

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
        if (message != HotkeyRegistrar.HotkeyMessage)
        {
            return 0;
        }

        var id = wParam.ToInt32();
        handled = true;
        if (id == EmergencyId)
        {
            EmergencyStopRequested?.Invoke();
        }
        return 0;
    }

    private void UnregisterAll()
    {
        if (source is not null)
        {
            foreach (var id in registeredIds)
            {
                _ = HotkeyRegistrar.Unregister(source.Handle, id);
            }
        }

        registeredIds.Clear();
    }
}
