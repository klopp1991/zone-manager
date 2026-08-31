using ZoneManager.Core.Geometry;

namespace ZoneManager.Core.PartMonitors;

public sealed record WindowPlacementSnapshot(
    WindowIdentity Identity,
    uint Flags,
    uint ShowCommand,
    PointInt MinPosition,
    PointInt MaxPosition,
    PixelRect NormalPosition);
