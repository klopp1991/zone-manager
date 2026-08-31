using ZoneManager.Core.Geometry;

namespace ZoneManager.Core.PartMonitors;

public interface IPartMonitorWindowGateway
{
    WindowPlacementSnapshot? Capture(nint windowHandle);
    bool TryApplyNormal(WindowIdentity identity, PixelRect bounds);
    bool TryRestore(WindowPlacementSnapshot snapshot);
}
