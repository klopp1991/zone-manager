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
    /// Die Startzone dieses Layouts, falls eine festgelegt ist. In ihr landen neu erscheinende Fenster,
    /// die sonst niemandem zugeordnet werden können. Es gibt in der gesamten Konfiguration höchstens
    /// eine gültige Startzone; <see cref="Layouts.StartZone"/> setzt das durch und löst sie zur Laufzeit auf.
    ///
    /// <para>
    /// Der gespeicherte Name bleibt <c>MainZoneId</c>, damit ein vor dem 10.09.2026 geschriebener Stand
    /// ohne Umbau weiterhin gelesen und geschrieben wird.
    /// </para>
    /// </summary>
    [JsonPropertyName("MainZoneId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Guid? StartZoneId { get; init; }

    [JsonIgnore]
    public string? UserFacingMonitorName { get; init; }
}
