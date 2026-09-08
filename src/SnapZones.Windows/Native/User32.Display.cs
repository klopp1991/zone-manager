using System.Runtime.InteropServices;

namespace SnapZones.Windows.Native;

/// <summary>Anzeigemodi setzen und die Skalierung eines Monitors lesen und schreiben.</summary>
internal static partial class User32
{
    internal const int EnumCurrentSettings = -1;
    internal const int EnumRegistrySettings = -2;

    internal const uint CdsUpdateRegistry = 0x00000001;
    internal const uint CdsNoReset = 0x10000000;

    internal const uint DmPosition = 0x00000020;
    internal const uint DmBitsPerPel = 0x00040000;
    internal const uint DmPelsWidth = 0x00080000;
    internal const uint DmPelsHeight = 0x00100000;
    internal const uint DmDisplayFrequency = 0x00400000;

    internal const uint DisplayDeviceAttachedToDesktop = 0x00000001;
    internal const uint DisplayDeviceMirroringDriver = 0x00000008;

    internal const int DispChangeSuccessful = 0;

    /// <summary>
    /// Die beiden Anfragen sind nicht dokumentiert, aber dieselben, mit denen die Einstellungen-App
    /// die Skalierung je Monitor liest und setzt. Die Werte sind Stufen relativ zur empfohlenen.
    /// </summary>
    internal const int DisplayConfigDeviceInfoGetDpiScale = -3;
    internal const int DisplayConfigDeviceInfoSetDpiScale = -4;

    [DllImport("user32.dll", EntryPoint = "EnumDisplaySettingsExW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumDisplaySettingsEx(
        string? deviceName,
        int modeNumber,
        ref DevModeNative devMode,
        uint flags);

    [DllImport("user32.dll", EntryPoint = "ChangeDisplaySettingsExW", CharSet = CharSet.Unicode)]
    internal static extern int ChangeDisplaySettingsEx(
        string? deviceName,
        ref DevModeNative devMode,
        nint window,
        uint flags,
        nint parameter);

    /// <summary>Der Aufruf ohne Modus, der die in der Registrierung vorgemerkten Aenderungen anwendet.</summary>
    [DllImport("user32.dll", EntryPoint = "ChangeDisplaySettingsExW", CharSet = CharSet.Unicode)]
    internal static extern int ChangeDisplaySettingsExApply(
        string? deviceName,
        nint devMode,
        nint window,
        uint flags,
        nint parameter);

    [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")]
    internal static extern int DisplayConfigGetDpiScale(ref DisplayConfigGetDpiScaleNative requestPacket);

    [DllImport("user32.dll", EntryPoint = "DisplayConfigSetDeviceInfo")]
    internal static extern int DisplayConfigSetDpiScale(ref DisplayConfigSetDpiScaleNative setPacket);
}
