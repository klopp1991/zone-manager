using System.Text.Json.Serialization;

namespace SnapZones.Core.Models;

public sealed record MonitorLayout(
    MonitorIdentity Monitor,
    int SavedWidth,
    int SavedHeight,
    IReadOnlyList<ZoneDefinition> Zones)
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "Standard";
    public bool IsActive { get; init; } = true;

    [JsonIgnore]
    public string? UserFacingMonitorName { get; init; }
}
