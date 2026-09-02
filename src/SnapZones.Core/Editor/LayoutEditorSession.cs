using SnapZones.Core.Geometry;
using SnapZones.Core.Models;

namespace SnapZones.Core.Editor;

/// <summary>
/// Der Entwurf eines Layouts im Editor, mit Verlauf. Jede Aenderung legt den vorherigen Stand auf den
/// Rueckgaengig-Stapel; bis zum 02.09.2026 gab es keinen Weg zurueck, und ein Fehlklick auf eine
/// Vorlage vernichtete ein handgebautes Layout.
/// </summary>
public sealed class LayoutEditorSession
{
    private const int HistoryLimit = 200;
    private readonly MonitorLayout savedLayout;
    private readonly List<EditorState> undoHistory = [];
    private readonly List<EditorState> redoHistory = [];
    private List<ZoneDefinition> zones;
    private Guid? mainZoneId;
    private int interactionDepth;
    private bool interactionRemembered;

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

    public bool CanUndo => undoHistory.Count > 0;
    public bool CanRedo => redoHistory.Count > 0;

    /// <summary>
    /// Fasst alle Aenderungen bis <see cref="EndInteraction"/> zu einem Verlaufseintrag zusammen. Ein
    /// Ziehen mit der Maus meldet jede Bewegung einzeln; ohne Klammer braeuchte «Rueckgaengig» hundert
    /// Schritte fuer einen Zug.
    /// </summary>
    public void BeginInteraction()
    {
        interactionDepth++;
    }

    public void EndInteraction()
    {
        if (interactionDepth == 0)
        {
            return;
        }

        interactionDepth--;
        if (interactionDepth == 0)
        {
            interactionRemembered = false;
        }
    }

    public ZoneDefinition AddZone(string name, NormalizedRect bounds)
    {
        Remember();
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

        Remember();
        zones = zones
            .Select(zone => changedBounds.TryGetValue(zone.Id, out var bounds)
                ? zone with { Bounds = bounds }
                : zone)
            .ToList();
    }

    public void UpdateZone(Guid zoneId, string name, NormalizedRect bounds)
    {
        var index = FindIndex(zoneId);
        var replacement = zones[index] with { Name = name.Trim(), Bounds = bounds };
        if (replacement == zones[index])
        {
            return;
        }

        Remember();
        zones[index] = replacement;
    }

    public void ReplaceZones(IReadOnlyList<ZoneDefinition> replacement)
    {
        Remember();
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

        if (mainZoneId == zoneId)
        {
            return;
        }

        Remember();
        mainZoneId = zoneId;
    }

    public void DeleteZone(Guid zoneId)
    {
        if (zones.All(zone => zone.Id != zoneId))
        {
            throw new KeyNotFoundException("Die Zone wurde nicht gefunden.");
        }

        Remember();
        zones.RemoveAll(zone => zone.Id == zoneId);
        DropMainZoneIfMissing();
    }

    public void Reset()
    {
        Remember();
        zones = [.. savedLayout.Zones];
        mainZoneId = savedLayout.MainZoneId;
    }

    /// <summary>Nimmt die letzte Aenderung zurueck. Ohne Verlauf passiert nichts.</summary>
    public bool Undo()
    {
        if (undoHistory.Count == 0)
        {
            return false;
        }

        redoHistory.Add(Capture());
        Restore(undoHistory[^1]);
        undoHistory.RemoveAt(undoHistory.Count - 1);
        return true;
    }

    /// <summary>Stellt eine zurueckgenommene Aenderung wieder her.</summary>
    public bool Redo()
    {
        if (redoHistory.Count == 0)
        {
            return false;
        }

        undoHistory.Add(Capture());
        Restore(redoHistory[^1]);
        redoHistory.RemoveAt(redoHistory.Count - 1);
        return true;
    }

    public MonitorLayout CreateSnapshot()
    {
        if (!Validation.IsValid)
        {
            throw new InvalidOperationException("Das Layout enthält ungültige Zonen.");
        }

        return savedLayout with { Zones = [.. zones], MainZoneId = mainZoneId };
    }

    private void Remember()
    {
        if (interactionDepth > 0)
        {
            if (interactionRemembered)
            {
                return;
            }

            interactionRemembered = true;
        }

        undoHistory.Add(Capture());
        if (undoHistory.Count > HistoryLimit)
        {
            undoHistory.RemoveAt(0);
        }

        redoHistory.Clear();
    }

    private EditorState Capture() => new([.. zones], mainZoneId);

    private void Restore(EditorState state)
    {
        zones = [.. state.Zones];
        mainZoneId = state.MainZoneId;
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
        if (zones[index].Bounds == bounds)
        {
            return;
        }

        Remember();
        zones[index] = zones[index] with { Bounds = bounds };
    }

    private int FindIndex(Guid zoneId)
    {
        var index = zones.FindIndex(zone => zone.Id == zoneId);
        return index >= 0 ? index : throw new KeyNotFoundException("Die Zone wurde nicht gefunden.");
    }

    private sealed record EditorState(IReadOnlyList<ZoneDefinition> Zones, Guid? MainZoneId);
}
