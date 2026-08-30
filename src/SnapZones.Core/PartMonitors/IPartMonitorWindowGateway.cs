using SnapZones.Core.Geometry;

namespace SnapZones.Core.PartMonitors;

public interface IPartMonitorWindowGateway
{
    WindowPlacementSnapshot? Capture(nint windowHandle);
    bool TryApplyNormal(WindowIdentity identity, PixelRect bounds);
    bool TryRestore(WindowPlacementSnapshot snapshot);
}
