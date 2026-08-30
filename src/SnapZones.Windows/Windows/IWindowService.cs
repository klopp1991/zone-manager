using SnapZones.Core.Drag;
using SnapZones.Core.Geometry;

namespace SnapZones.Windows.Windows;

public interface IWindowService
{
    WindowSnapshot? Inspect(nint window, PointInt cursor, int ownProcessId);
    bool TrySnap(nint window, PixelRect bounds);
}
