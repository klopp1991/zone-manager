namespace SnapZones.Core.Models;

public sealed record MonitorLayout(
    MonitorIdentity Monitor,
    int SavedWidth,
    int SavedHeight,
    IReadOnlyList<ZoneDefinition> Zones);
