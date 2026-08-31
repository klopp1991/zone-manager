using SnapZones.Core.Drag;
using SnapZones.Core.Geometry;

namespace SnapZones.Windows.Windows;

public interface IWindowService
{
    WindowSnapshot? Inspect(nint window, PointInt cursor, int ownProcessId);
    bool TrySnap(nint window, PixelRect bounds);
    bool TryGetCursorPosition(out PointInt point);
    bool IsEscapePressed();
    bool IsShiftPressed();

    /// <summary>
    /// True while the modifier that spans a drag across several zones is held
    /// down. Holding it adds the hovered zone to the selection instead of
    /// replacing it.
    /// </summary>
    bool IsSpanModifierPressed();
}
