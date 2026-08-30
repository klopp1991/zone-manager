using System.Runtime.InteropServices;
using Xunit;

namespace SnapZones.Tests.Support;

/// <summary>
/// A fact that only executes on Windows. Used for assertions that depend on
/// Windows-specific behaviour (path separators, registry, Win32 calls) so that
/// the rest of the suite stays runnable on any build agent.
/// </summary>
public sealed class WindowsOnlyFactAttribute : FactAttribute
{
    public WindowsOnlyFactAttribute()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Skip = "Requires Windows.";
        }
    }
}
