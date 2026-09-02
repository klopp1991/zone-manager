using System.Runtime.InteropServices;
using SnapZones.Windows.Native;

namespace SnapZones.Windows.Windows;

internal interface IWindowStyleReader
{
    bool TryRead(nint window, int index, out long value);
}

internal sealed class User32WindowStyleReader : IWindowStyleReader
{
    public bool TryRead(nint window, int index, out long value)
    {
        Marshal.SetLastPInvokeError(0);
        var nativeValue = User32.GetWindowLongPtr(window, index);
        var error = Marshal.GetLastPInvokeError();
        value = nativeValue.ToInt64();
        return nativeValue != 0 || error == 0;
    }
}
