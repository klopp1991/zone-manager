using SnapZones.Core.Geometry;
using SnapZones.Core.Models;

namespace SnapZones.Core.Monitors;

public sealed record LiveMonitor(
    MonitorIdentity Identity,
    MonitorWorkArea WorkArea,
    uint DpiX,
    uint DpiY,
    bool IsPrimary,
    double? PhysicalWidthCentimeters = null,
    double? PhysicalHeightCentimeters = null);
