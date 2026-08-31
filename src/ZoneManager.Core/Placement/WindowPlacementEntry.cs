using ZoneManager.Core.Geometry;
using ZoneManager.Core.Models;

namespace ZoneManager.Core.Placement;

public sealed record WindowPlacementEntry(
    WindowIdentity Identity,
    string MonitorStableId,
    Guid? ZoneId,
    MonitorWorkArea SourceWorkArea,
    PixelRect NormalBoundsPixels,
    NormalizedRect NormalBoundsNormalized,
    bool WasMaximized,
    DateTimeOffset LastUpdatedUtc);

public sealed record WindowPlacementCatalog(int SchemaVersion, IReadOnlyList<WindowPlacementEntry> Entries)
{
    public const int CurrentSchemaVersion = 1;
    public static WindowPlacementCatalog Empty { get; } = new(CurrentSchemaVersion, []);
}

public sealed record PlacementMonitorTarget(string StableId, MonitorWorkArea WorkArea, bool IsPrimary);

public sealed record PlacementZoneTarget(Guid ProfileId, Guid ZoneId, string MonitorStableId, PixelRect Bounds);
