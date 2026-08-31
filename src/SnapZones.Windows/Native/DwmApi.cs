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

    /// <summary>Eigener Name, damit die RECT-Variante nicht mit der int-Variante kollidiert.</summary>
    [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")]
    internal static extern int DwmGetWindowRectAttribute(
        nint window,
        int attribute,
        out RectNative value,
        int valueSize);

    [DllImport("dwmapi.dll")]
    internal static extern int DwmSetWindowAttribute(
        nint window,
        int attribute,
        ref int value,
        int valueSize);
}
