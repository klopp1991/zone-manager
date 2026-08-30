using SnapZones.Core.Drag;
using SnapZones.Core.Geometry;
using SnapZones.Core.Layouts;

namespace SnapZones.Windows.Windows;

public interface IWindowService
{
    WindowSnapshot? Inspect(nint window, PointInt cursor, int ownProcessId);
    bool TrySnap(nint window, PixelRect bounds);
    IReadOnlyList<WindowPlacement> GetMovableTopLevelWindows(int ownProcessId);
    WindowRuleCandidate? InspectRuleCandidate(nint window, int ownProcessId);
    IReadOnlyList<WindowRuleCandidate> GetRuleCandidates(int ownProcessId);
    bool TryGetCursorPosition(out PointInt point);
    bool IsEscapePressed();
    bool IsShiftPressed();
}
