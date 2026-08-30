using System.Runtime.InteropServices;
using SnapZones.Windows.Native;

namespace SnapZones.Windows.Theme;

public static class WindowThemeFrame
{
    private const int UseImmersiveDarkMode = 20;

    public static bool Apply(nint windowHandle, bool dark)
    {
        if (windowHandle == 0)
        {
            return false;
        }

        var value = dark ? 1 : 0;
        return DwmApi.DwmSetWindowAttribute(
            windowHandle,
            UseImmersiveDarkMode,
            ref value,
            Marshal.SizeOf<int>()) == 0;
    }
}

