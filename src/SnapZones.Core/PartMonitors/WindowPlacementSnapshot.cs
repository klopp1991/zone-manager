using SnapZones.Core.Geometry;

namespace SnapZones.Core.PartMonitors;

public sealed record WindowPlacementSnapshot(
    WindowIdentity Identity,
    uint Flags,
    uint ShowCommand,
    PointInt MinPosition,
    PointInt MaxPosition,
    PixelRect NormalPosition);
