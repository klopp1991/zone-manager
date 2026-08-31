using ZoneManager.Core.Geometry;
using ZoneManager.Core.Models;

namespace ZoneManager.Core.Monitors;

public sealed record LiveMonitor(
    MonitorIdentity Identity,
    MonitorWorkArea WorkArea,
    uint DpiX,
    uint DpiY,
    bool IsPrimary,
    double? PhysicalWidthCentimeters = null,
    double? PhysicalHeightCentimeters = null);
