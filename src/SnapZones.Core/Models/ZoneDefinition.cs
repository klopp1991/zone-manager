using System.Text.Json.Serialization;

namespace SnapZones.Core.Models;

public sealed record ZoneDefinition(Guid Id, string Name, NormalizedRect Bounds)
{
    /// <summary>
    /// Die Zone ist ein virtueller Monitor: ein Fenster, das dorthin kommt, wird auf den virtuellen
    /// Monitor des Anzeigetreibers gelegt, dessen Bild in der Zone gespiegelt wird. Das Programm darf
    /// dort sein Vollbild einschalten und fuellt trotzdem nur die Zone. Nicht gesetzt heisst: eine
    /// gewoehnliche Zone.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsVirtualMonitor { get; init; }
}
