using System.Windows.Interop;
using ZoneManager.Windows.Native;

namespace ZoneManager.Windows.Hotkeys;

public sealed class GlobalHotkeyService : IGlobalHotkeyService
{
    private const int HotkeyMessage = 0x0312;
    private const int EmergencyId = 999;
    private const uint Alt = 0x0001;
    private const uint Control = 0x0002;
    private const uint Shift = 0x0004;
    private const uint NoRepeat = 0x4000;
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
            if (User32.RegisterHotKey(source!.Handle, EmergencyId, Control | Alt | Shift | NoRepeat, 0x7B))
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

        source = new HwndSource(new HwndSourceParameters("ZoneManager.Hotkeys")
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
                _ = User32.UnregisterHotKey(source.Handle, id);
            }
        }

        registeredIds.Clear();
    }
}
