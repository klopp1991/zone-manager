using SnapZones.Core.Editor;
using SnapZones.Core.Geometry;
using SnapZones.Core.Models;

namespace SnapZones.App.ViewModels;

public sealed class LayoutEditorViewModel : ViewModelBase
{
    private readonly LayoutEditorSession session;
    private readonly int monitorWidth;
    private readonly int monitorHeight;
    private Guid? selectedZoneId;

    public LayoutEditorViewModel(MonitorLayout layout)
    {
        session = new LayoutEditorSession(layout);
        monitorWidth = Math.Max(1, layout.SavedWidth);
        monitorHeight = Math.Max(1, layout.SavedHeight);
        selectedZoneId = session.Zones.FirstOrDefault()?.Id;
    }

    public event Action? ConfigurationChanged;

    public IReadOnlyList<ZoneDefinition> Zones => session.Zones;
    public ZoneDefinition? SelectedZone => Zones.FirstOrDefault(zone => zone.Id == selectedZoneId);

    /// <summary>Die Auffangzone dieses Layouts, falls es eine gibt.</summary>
    public Guid? MainZoneId => session.MainZoneId;

    /// <summary>Ob die gerade ausgewählte Zone die Auffangzone ist.</summary>
    public bool IsSelectedZoneMainZone => SelectedZone is { } zone && session.MainZoneId == zone.Id;

    /// <summary>Beschriftung der einen Schaltfläche; sie führt in beide Richtungen.</summary>
    public string MainZoneActionLabel => IsSelectedZoneMainZone
        ? "Auffangzone aufheben"
        : "Als Auffangzone festlegen";

    /// <summary>Was in diesem Layout gilt, im Klartext und ohne Farbe.</summary>
    public string MainZoneStateText => session.MainZoneId is null
        ? "Keine Zone dieses Layouts ist Auffangzone."
        : IsSelectedZoneMainZone
            ? "Diese Zone ist die Auffangzone dieses Layouts."
            : $"Auffangzone dieses Layouts ist «{Zones.First(zone => zone.Id == session.MainZoneId).Name}».";
    public bool IsDirty => session.IsDirty;
    public bool IsValid => session.Validation.IsValid;
    public bool CanUndo => session.CanUndo;
    public bool CanRedo => session.CanRedo;
    public bool CanSave => IsDirty && session.Validation.IsValid;
    public string ValidationMessage => session.Validation.IsValid
        ? string.Empty
        : string.Join(" ", session.Validation.Errors.Select(error => error.Message).Distinct());

    public void SelectZone(Guid zoneId)
    {
        if (Zones.All(zone => zone.Id != zoneId))
        {
            return;
        }

        selectedZoneId = zoneId;
        NotifyStateChanged();
    }

    public bool AddZone()
    {
        var freeArea = LargestFreeRectangle.Find(Zones.Select(zone => zone.Bounds).ToArray());
        if (freeArea is null)
        {
            return false;
        }

        var zone = session.AddZone($"Zone {Zones.Count + 1}", freeArea);
        selectedZoneId = zone.Id;
        NotifyStateChanged();
        NotifyConfigurationChanged();
        return true;
    }

    public void DeleteSelected()
    {
        if (selectedZoneId is null)
        {
            return;
        }

        session.DeleteZone(selectedZoneId.Value);
        selectedZoneId = Zones.FirstOrDefault()?.Id;
        NotifyStateChanged();
        NotifyConfigurationChanged();
    }

    public void ApplyTemplate(LayoutTemplate template)
    {
        session.ReplaceZones(LayoutTemplates.Create(template));
        selectedZoneId = Zones[0].Id;
        NotifyStateChanged();
        NotifyConfigurationChanged();
    }

    public void UpdateSelectedZone(string name, double xPercent, double yPercent, double widthPercent, double heightPercent)
        => UpdateSelectedZoneFromPositionAndSize(
            name,
            xPercent,
            yPercent,
            widthPercent,
            heightPercent,
            MeasurementUnit.Percent);

    public void UpdateSelectedZoneFromPositionAndSize(
        string name,
        double left,
        double top,
        double width,
        double height,
        MeasurementUnit unit)
        => UpdateSelectedZoneFromPositionAndSize(
            name,
            new ZoneMeasurement(left, unit),
            new ZoneMeasurement(top, unit),
            new ZoneMeasurement(width, unit),
            new ZoneMeasurement(height, unit));

    public void UpdateSelectedZoneFromPositionAndSize(
        string name,
        ZoneMeasurement left,
        ZoneMeasurement top,
        ZoneMeasurement width,
        ZoneMeasurement height)
    {
        if (selectedZoneId is null)
        {
            return;
        }

        var bounds = ZoneEditorGeometry.FromPositionAndSize(
            left, top, width, height, monitorWidth, monitorHeight);
        session.UpdateZone(selectedZoneId.Value, name, bounds);
        NotifyStateChanged();
        NotifyConfigurationChanged();
    }

    public void UpdateSelectedZoneFromMargins(
        string name,
        double left,
        double top,
        double right,
        double bottom,
        MeasurementUnit unit)
        => UpdateSelectedZoneFromMargins(
            name,
            new ZoneMeasurement(left, unit),
            new ZoneMeasurement(top, unit),
            new ZoneMeasurement(right, unit),
            new ZoneMeasurement(bottom, unit));

    public void UpdateSelectedZoneFromMargins(
        string name,
        ZoneMeasurement left,
        ZoneMeasurement top,
        ZoneMeasurement right,
        ZoneMeasurement bottom)
    {
        if (selectedZoneId is null)
        {
            return;
        }

        var bounds = ZoneEditorGeometry.FromMargins(
            left, top, right, bottom, monitorWidth, monitorHeight);
        session.UpdateZone(selectedZoneId.Value, name, bounds);
        NotifyStateChanged();
        NotifyConfigurationChanged();
    }

    public ZoneEditorValues GetSelectedValues(MeasurementUnit unit) => SelectedZone is { } zone
        ? ZoneEditorGeometry.ToValues(zone.Bounds, unit, monitorWidth, monitorHeight)
        : throw new InvalidOperationException("Es ist keine Zone ausgewählt.");

    public void MoveOrResizeZone(Guid zoneId, NormalizedRect bounds)
    {
        session.MoveZone(zoneId, bounds);
        selectedZoneId = zoneId;
        NotifyStateChanged();
        NotifyConfigurationChanged();
    }

    public void MoveOrResizeZones(
        Guid selectedZone,
        IReadOnlyDictionary<Guid, NormalizedRect> changedBounds)
    {
        session.MoveZones(changedBounds);
        selectedZoneId = selectedZone;
        NotifyStateChanged();
        NotifyConfigurationChanged();
    }

    /// <summary>
    /// Macht die ausgewählte Zone zur Hauptzone dieses Layouts, oder hebt die Markierung wieder auf, wenn
    /// sie es schon ist. Andere Layouts behalten ihre eigene Markierung.
    /// </summary>
    public void ToggleSelectedZoneAsMainZone()
    {
        if (selectedZoneId is null)
        {
            return;
        }

        session.SetMainZone(IsSelectedZoneMainZone ? null : selectedZoneId);
        NotifyStateChanged();
        NotifyConfigurationChanged();
    }

    /// <summary>Ersetzt alle Zonen, etwa durch die einzelne Vollzone eines leeren Layouts.</summary>
    public void ReplaceZones(IReadOnlyList<ZoneDefinition> zones)
    {
        ArgumentNullException.ThrowIfNull(zones);
        session.ReplaceZones(zones);
        selectedZoneId = Zones.FirstOrDefault()?.Id;
        NotifyStateChanged();
        NotifyConfigurationChanged();
    }

    /// <summary>Loescht eine beliebige Zone, etwa aus dem Kontextmenue; mindestens eine bleibt.</summary>
    public bool DeleteZone(Guid zoneId)
    {
        if (Zones.Count <= 1 || Zones.All(zone => zone.Id != zoneId))
        {
            return false;
        }

        session.DeleteZone(zoneId);
        if (selectedZoneId == zoneId || selectedZoneId is null)
        {
            selectedZoneId = Zones.FirstOrDefault()?.Id;
        }

        NotifyStateChanged();
        NotifyConfigurationChanged();
        return true;
    }

    /// <summary>Macht eine Zone zur Auffangzone oder hebt die Markierung auf, wenn sie es schon ist.</summary>
    public void ToggleMainZone(Guid zoneId)
    {
        if (Zones.All(zone => zone.Id != zoneId))
        {
            return;
        }

        session.SetMainZone(session.MainZoneId == zoneId ? null : zoneId);
        NotifyStateChanged();
        NotifyConfigurationChanged();
    }

    public void RenameZone(Guid zoneId, string name)
    {
        var zone = Zones.FirstOrDefault(candidate => candidate.Id == zoneId);
        if (zone is null)
        {
            return;
        }

        session.UpdateZone(zoneId, name, zone.Bounds);
        NotifyStateChanged();
        NotifyConfigurationChanged();
    }

    /// <summary>
    /// Die Nachbarn einer Zone, mit denen sie sich zu einem Rechteck verbinden laesst: sie teilen eine
    /// ganze Kante in gleicher Laenge, sodass die Vereinigung keine Luecke laesst.
    /// </summary>
    public IReadOnlyList<ZoneDefinition> MergeableNeighbours(Guid zoneId)
    {
        var zone = Zones.FirstOrDefault(candidate => candidate.Id == zoneId);
        if (zone is null)
        {
            return [];
        }

        return Zones.Where(candidate => candidate.Id != zoneId && SharesFullEdge(zone.Bounds, candidate.Bounds)).ToArray();
    }

    /// <summary>
    /// Verbindet zwei Zonen zu einer: die Vereinigung ersetzt beide, der Name der ersten bleibt. Gelingt nur,
    /// wenn die Zonen eine ganze Kante teilen; sonst bleibt alles unveraendert.
    /// </summary>
    public bool MergeZones(Guid zoneId, Guid neighbourId)
    {
        var zone = Zones.FirstOrDefault(candidate => candidate.Id == zoneId);
        var neighbour = Zones.FirstOrDefault(candidate => candidate.Id == neighbourId);
        if (zone is null || neighbour is null || !SharesFullEdge(zone.Bounds, neighbour.Bounds))
        {
            return false;
        }

        var left = Math.Min(zone.Bounds.X, neighbour.Bounds.X);
        var top = Math.Min(zone.Bounds.Y, neighbour.Bounds.Y);
        var right = Math.Max(zone.Bounds.X + zone.Bounds.Width, neighbour.Bounds.X + neighbour.Bounds.Width);
        var bottom = Math.Max(zone.Bounds.Y + zone.Bounds.Height, neighbour.Bounds.Y + neighbour.Bounds.Height);
        var merged = zone with { Bounds = new NormalizedRect(left, top, right - left, bottom - top) };
        var replacement = Zones
            .Where(candidate => candidate.Id != neighbourId)
            .Select(candidate => candidate.Id == zoneId ? merged : candidate)
            .ToArray();
        session.ReplaceZones(replacement);
        if (session.MainZoneId is null && (zone.Id == MainZoneId || neighbour.Id == MainZoneId))
        {
            session.SetMainZone(merged.Id);
        }

        selectedZoneId = merged.Id;
        NotifyStateChanged();
        NotifyConfigurationChanged();
        return true;
    }

    private static bool SharesFullEdge(NormalizedRect first, NormalizedRect second)
    {
        const double epsilon = 0.0005;
        var sameColumns = Math.Abs(first.X - second.X) < epsilon && Math.Abs(first.Width - second.Width) < epsilon;
        var stackedVertically = Math.Abs(first.Y + first.Height - second.Y) < epsilon || Math.Abs(second.Y + second.Height - first.Y) < epsilon;
        var sameRows = Math.Abs(first.Y - second.Y) < epsilon && Math.Abs(first.Height - second.Height) < epsilon;
        var sideBySide = Math.Abs(first.X + first.Width - second.X) < epsilon || Math.Abs(second.X + second.Width - first.X) < epsilon;
        return (sameColumns && stackedVertically) || (sameRows && sideBySide);
    }

    public void RenameSelectedZone(string name)
    {
        if (selectedZoneId is null || SelectedZone is not { } selectedZone)
        {
            return;
        }

        session.UpdateZone(selectedZoneId.Value, name, selectedZone.Bounds);
        NotifyStateChanged();
        NotifyConfigurationChanged();
    }

    public void Reset()
    {
        session.Reset();
        selectedZoneId = Zones.FirstOrDefault()?.Id;
        NotifyStateChanged();
        NotifyConfigurationChanged();
    }

    /// <summary>Klammert die Aenderungen eines Mausziehens zu einem Verlaufseintrag.</summary>
    public void BeginInteractiveChange() => session.BeginInteraction();

    public void EndInteractiveChange()
    {
        session.EndInteraction();
        NotifyStateChanged();
    }

    /// <summary>Nimmt die letzte Aenderung zurueck; die Auswahl bleibt, wenn es die Zone noch gibt.</summary>
    public bool Undo() => Travel(session.Undo);

    public bool Redo() => Travel(session.Redo);

    private bool Travel(Func<bool> step)
    {
        if (!step())
        {
            return false;
        }

        if (selectedZoneId is null || Zones.All(zone => zone.Id != selectedZoneId))
        {
            selectedZoneId = Zones.FirstOrDefault()?.Id;
        }

        NotifyStateChanged();
        NotifyConfigurationChanged();
        return true;
    }

    public MonitorLayout CreateSnapshot() => session.CreateSnapshot();

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(Zones));
        OnPropertyChanged(nameof(SelectedZone));
        OnPropertyChanged(nameof(MainZoneId));
        OnPropertyChanged(nameof(IsSelectedZoneMainZone));
        OnPropertyChanged(nameof(MainZoneActionLabel));
        OnPropertyChanged(nameof(MainZoneStateText));
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(IsValid));
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(ValidationMessage));
    }

    private void NotifyConfigurationChanged()
    {
        if (IsValid)
        {
            ConfigurationChanged?.Invoke();
        }
    }
}
