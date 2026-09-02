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

    /// <summary>
    /// Die Hauptzone dieses Layouts, falls eine festgelegt ist. In ihr landen neu erscheinende Fenster,
    /// die sonst niemandem zugeordnet werden können. Es gibt in der gesamten Konfiguration höchstens
    /// eine Hauptzone; <see cref="Layouts.MainZone"/> setzt das durch und löst sie zur Laufzeit auf.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? MainZoneId { get; init; }

    [JsonIgnore]
    public string? UserFacingMonitorName { get; init; }
}
