using System.Text.Json.Serialization;

namespace SnapZones.Core.Models;

public sealed record ZoneDefinition(Guid Id, string Name, NormalizedRect Bounds)
{
    /// <summary>
    /// Die Zone gilt als eigener Bildschirm: ein Fenster, das dorthin kommt, wird auf den virtuellen
    /// Monitor des Vollbildzonen-Treibers gelegt, dessen Bild in der Zone erscheint. Das Programm darf
    /// dort sein Vollbild einschalten und fuellt trotzdem nur die Zone. Nicht gesetzt heisst: eine
    /// gewoehnliche Zone.
    ///
    /// <para>
    /// Der gespeicherte Name bleibt <c>IsVirtualMonitor</c>, damit ein vor dem 10.09.2026 geschriebener
    /// Stand ohne Umbau weiterhin gelesen und geschrieben wird.
    /// </para>
    /// </summary>
    [JsonPropertyName("IsVirtualMonitor")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsFullscreenZone { get; init; }
}
