using SnapZones.Core.Geometry;
using SnapZones.Core.Models;

namespace SnapZones.Core.Editor;

public sealed class LayoutEditorSession
{
    private readonly MonitorLayout savedLayout;
    private List<ZoneDefinition> zones;
    private Guid? mainZoneId;

    public LayoutEditorSession(MonitorLayout layout)
    {
        savedLayout = layout with { Zones = [.. layout.Zones] };
        zones = [.. layout.Zones];
        mainZoneId = layout.MainZoneId;
    }

    public IReadOnlyList<ZoneDefinition> Zones => zones;

    /// <summary>Die im Entwurf als Hauptzone markierte Zone, falls es eine gibt.</summary>
    public Guid? MainZoneId => mainZoneId;

    public bool IsDirty => !zones.SequenceEqual(savedLayout.Zones) || mainZoneId != savedLayout.MainZoneId;
    public ZoneValidationResult Validation => ZoneGeometry.Validate(zones);

    public ZoneDefinition AddZone(string name, NormalizedRect bounds)
    {
        var zone = new ZoneDefinition(Guid.NewGuid(), name.Trim(), bounds);
        zones.Add(zone);
        return zone;
    }

    public void MoveZone(Guid zoneId, NormalizedRect bounds) => ReplaceBounds(zoneId, bounds);

    public void ResizeZone(Guid zoneId, NormalizedRect bounds) => ReplaceBounds(zoneId, bounds);

    public void MoveZones(IReadOnlyDictionary<Guid, NormalizedRect> changedBounds)
    {
        foreach (var zoneId in changedBounds.Keys)
        {
            FindIndex(zoneId);
        }

        zones = zones
            .Select(zone => changedBounds.TryGetValue(zone.Id, out var bounds)
                ? zone with { Bounds = bounds }
                : zone)
            .ToList();
    }

    public void UpdateZone(Guid zoneId, string name, NormalizedRect bounds)
    {
        var index = FindIndex(zoneId);
        zones[index] = zones[index] with { Name = name.Trim(), Bounds = bounds };
    }

    public void ReplaceZones(IReadOnlyList<ZoneDefinition> replacement)
    {
        zones = [.. replacement];
        // Eine Vorlage ersetzt alle Zonen samt Kennungen; ein Verweis auf die alte Hauptzone waere leer.
        DropMainZoneIfMissing();
    }

    /// <summary>
    /// Markiert eine Zone als Hauptzone oder hebt die Markierung auf. <c>null</c> hebt sie auf; dieselbe
    /// Zone erneut zu setzen bleibt folgenlos.
    /// </summary>
    public void SetMainZone(Guid? zoneId)
    {
        if (zoneId is Guid wanted && zones.All(zone => zone.Id != wanted))
        {
            throw new KeyNotFoundException("Die Zone wurde nicht gefunden.");
        }

        mainZoneId = zoneId;
    }

    public void DeleteZone(Guid zoneId)
    {
        if (zones.RemoveAll(zone => zone.Id == zoneId) == 0)
        {
            throw new KeyNotFoundException("Die Zone wurde nicht gefunden.");
        }

        DropMainZoneIfMissing();
    }

    public void Reset()
    {
        zones = [.. savedLayout.Zones];
        mainZoneId = savedLayout.MainZoneId;
    }

    public MonitorLayout CreateSnapshot()
    {
        if (!Validation.IsValid)
        {
            throw new InvalidOperationException("Das Layout enthält ungültige Zonen.");
        }

        return savedLayout with { Zones = [.. zones], MainZoneId = mainZoneId };
    }

    private void DropMainZoneIfMissing()
    {
        if (mainZoneId is Guid zoneId && zones.All(zone => zone.Id != zoneId))
        {
            mainZoneId = null;
        }
    }

    private void ReplaceBounds(Guid zoneId, NormalizedRect bounds)
    {
        var index = FindIndex(zoneId);
        zones[index] = zones[index] with { Bounds = bounds };
    }

    private int FindIndex(Guid zoneId)
    {
        var index = zones.FindIndex(zone => zone.Id == zoneId);
        return index >= 0 ? index : throw new KeyNotFoundException("Die Zone wurde nicht gefunden.");
    }
}
