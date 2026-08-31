using System.Runtime.InteropServices;

namespace ZoneManager.Windows.Native;

internal static class Shcore
{
    [DllImport("shcore.dll")]
    internal static extern int GetDpiForMonitor(nint monitor, int dpiType, out uint dpiX, out uint dpiY);
}
