using SnapZones.Core.Drag;
using SnapZones.Core.Geometry;
using SnapZones.Core.Layouts;
using SnapZones.Core.PartMonitors;

namespace SnapZones.Windows.Windows;

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

    /// <summary>Ob Strg gedrueckt ist. Waehlt beim Ziehen mehrere Zonen gleichzeitig aus.</summary>
    bool IsControlPressed();

    /// <summary>
    /// Ob das Fenster nur mit Administratorrechten bewegt werden koennte, weil es einem hoeher
    /// berechtigten Programm gehoert.
    /// </summary>
    bool RequiresElevation(nint window);
}
