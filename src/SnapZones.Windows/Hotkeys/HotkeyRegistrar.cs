using SnapZones.Windows.Native;

namespace SnapZones.Windows.Hotkeys;

/// <summary>
/// Thin, testable seam over the Win32 hotkey API so that callers outside this
/// assembly never have to touch native interop directly.
/// </summary>
public static class HotkeyRegistrar
{
    /// <summary>Win32 <c>WM_HOTKEY</c>.</summary>
    public const int HotkeyMessage = 0x0312;

    public static bool Register(nint window, int id, HotkeyModifiers modifiers, uint virtualKey) =>
        User32.RegisterHotKey(window, id, (uint)modifiers, virtualKey);

    public static bool Unregister(nint window, int id) =>
        User32.UnregisterHotKey(window, id);
}

[Flags]
public enum HotkeyModifiers : uint
{
    None = 0,
    Alt = 0x0001,
    Control = 0x0002,
    Shift = 0x0004,
    Windows = 0x0008,

    /// <summary>Suppresses auto-repeat while the combination is held down.</summary>
    NoRepeat = 0x4000
}
