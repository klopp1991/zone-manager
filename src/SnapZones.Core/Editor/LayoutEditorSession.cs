using SnapZones.Core.Geometry;
using SnapZones.Core.Models;

namespace SnapZones.Core.Editor;

public sealed class LayoutEditorSession
{
    private readonly MonitorLayout savedLayout;
    private List<ZoneDefinition> zones;

    public LayoutEditorSession(MonitorLayout layout)
    {
        savedLayout = layout with { Zones = [.. layout.Zones] };
        zones = [.. layout.Zones];
    }

    public IReadOnlyList<ZoneDefinition> Zones => zones;
    public bool IsDirty => !zones.SequenceEqual(savedLayout.Zones);
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

    public void ReplaceZones(IReadOnlyList<ZoneDefinition> replacement) => zones = [.. replacement];

    public void DeleteZone(Guid zoneId)
    {
        if (zones.RemoveAll(zone => zone.Id == zoneId) == 0)
        {
            throw new KeyNotFoundException("Die Zone wurde nicht gefunden.");
        }
    }

    public void Reset() => zones = [.. savedLayout.Zones];

    public MonitorLayout CreateSnapshot()
    {
        if (!Validation.IsValid)
        {
            throw new InvalidOperationException("Das Layout enthält ungültige Zonen.");
        }

        return savedLayout with { Zones = [.. zones] };
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
