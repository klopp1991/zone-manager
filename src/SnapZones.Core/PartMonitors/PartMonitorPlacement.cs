using SnapZones.Core.Geometry;

namespace SnapZones.Core.PartMonitors;

public sealed record PartMonitorPlacement(
    string MonitorId,
    Guid PartMonitorId,
    PixelRect Bounds);
