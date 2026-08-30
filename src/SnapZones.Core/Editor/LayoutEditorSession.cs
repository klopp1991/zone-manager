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
        var index = zones.FindIndex(zone => zone.Id == zoneId);
        if (index < 0)
        {
            throw new KeyNotFoundException("Die Zone wurde nicht gefunden.");
        }

        zones[index] = zones[index] with { Bounds = bounds };
    }
}
