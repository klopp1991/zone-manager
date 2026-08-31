using ZoneManager.Core.Drag;
using ZoneManager.Core.Geometry;
using ZoneManager.Core.Layouts;
using ZoneManager.Core.PartMonitors;

namespace ZoneManager.Windows.Windows;

public interface IWindowService : IPartMonitorWindowGateway
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
