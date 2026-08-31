using ZoneManager.Core.Geometry;

namespace ZoneManager.Windows.Windows;

public interface IPlacementWindowService
{
    PlacementWindowSnapshot? Inspect(nint windowHandle, int excludedProcessId);
    bool TryPlace(nint windowHandle, PixelRect normalBounds, bool maximize);
    IReadOnlyList<nint> EnumerateEligibleWindows(int excludedProcessId);
    nint GetForegroundWindow();
}
