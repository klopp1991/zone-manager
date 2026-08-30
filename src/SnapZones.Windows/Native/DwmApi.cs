using System.Runtime.InteropServices;

namespace SnapZones.Windows.Native;

internal static class DwmApi
{
    [DllImport("dwmapi.dll")]
    internal static extern int DwmGetWindowAttribute(
        nint window,
        int attribute,
        out int value,
        int valueSize);
}
