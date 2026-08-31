using ZoneManager.Core.Geometry;

namespace ZoneManager.Core.PartMonitors;

public sealed record PartMonitorPlacement(
    string MonitorId,
    Guid PartMonitorId,
    PixelRect Bounds);
