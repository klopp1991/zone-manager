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
    private string lastActionLabel = string.Empty;
    private string redoLabel = string.Empty;

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

    /// <summary>Die Startzone dieses Layouts, falls es eine gibt.</summary>
    public Guid? StartZoneId => session.StartZoneId;

    /// <summary>Ob die gerade ausgewählte Zone die Startzone ist.</summary>
    public bool IsSelectedZoneStartZone => SelectedZone is { } zone && session.StartZoneId == zone.Id;

    /// <summary>Ob die gerade ausgewählte Zone ein virtueller Monitor (Vollbildzone) ist.</summary>
    public bool IsSelectedZoneFullscreenZone => SelectedZone is { IsFullscreenZone: true };

    /// <summary>Was fuer die ausgewaehlte Zone gilt, im Klartext und ohne Farbe.</summary>
    public string FullscreenZoneStateText => SelectedZone is null
        ? string.Empty
        : IsSelectedZoneFullscreenZone
            ? "Die Zone gilt als eigener Bildschirm; ein Video darin bleibt beim Vollbild in seiner Zone."
            : "Eine gewöhnliche Zone: Fenster werden nur auf ihre Fläche gesetzt.";

    /// <summary>Beschriftung der einen Schaltfläche; sie führt in beide Richtungen.</summary>
    public string StartZoneActionLabel => IsSelectedZoneStartZone
        ? "Startzone aufheben"
        : "Als Startzone festlegen";

    /// <summary>Was in diesem Layout gilt, im Klartext und ohne Farbe.</summary>
    public string StartZoneStateText => session.StartZoneId is null
        ? "Keine Zone dieses Layouts ist Startzone."
        : IsSelectedZoneStartZone
            ? "Diese Zone ist die Startzone dieses Layouts."
            : $"Startzone dieses Layouts ist «{Zones.First(zone => zone.Id == session.StartZoneId).Name}».";
    public bool IsDirty => session.IsDirty;
    public bool IsValid => session.Validation.IsValid;
    public bool CanUndo => session.CanUndo;
    public bool CanRedo => session.CanRedo;

    /// <summary>
    /// Was ein Klick auf ↶ zurueckneh­men wuerde, in wenigen Worten – etwa «Zone 3 verkleinert». Leer,
    /// solange nichts zurueckzunehmen ist. Der Text steht auf der Layout-Flaeche neben den Pfeilen.
    /// </summary>
    public string UndoLabel => session.CanUndo ? lastActionLabel : string.Empty;

    /// <summary>Was ein Klick auf ↷ wiederherstellen wuerde. Leer, solange nichts wiederherzustellen ist.</summary>
    public string RedoLabel => session.CanRedo ? redoLabel : string.Empty;
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
        RecordAction("Zone hinzugefügt");
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
        RecordAction("Zone entfernt");
        NotifyStateChanged();
        NotifyConfigurationChanged();
    }

    public void ApplyTemplate(LayoutTemplate template)
    {
        session.ReplaceZones(LayoutTemplates.Create(template));
        selectedZoneId = Zones[0].Id;
        RecordAction("Vorlage übernommen");
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
        RecordAction("Zone geändert");
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
        RecordAction("Zone geändert");
        NotifyStateChanged();
        NotifyConfigurationChanged();
    }

    public void MoveOrResizeZones(
        Guid selectedZone,
        IReadOnlyDictionary<Guid, NormalizedRect> changedBounds)
    {
        session.MoveZones(changedBounds);
        selectedZoneId = selectedZone;
        RecordAction("Zone geändert");
        NotifyStateChanged();
        NotifyConfigurationChanged();
    }

    /// <summary>
    /// Macht die ausgewählte Zone zur Startzone dieses Layouts, oder hebt die Markierung wieder auf, wenn
    /// sie es schon ist. Andere Layouts behalten ihre eigene Markierung.
    /// </summary>
    public void ToggleSelectedZoneAsStartZone()
    {
        if (selectedZoneId is null)
        {
            return;
        }

        session.SetStartZone(IsSelectedZoneStartZone ? null : selectedZoneId);
        RecordAction("Startzone geändert");
        NotifyStateChanged();
        NotifyConfigurationChanged();
    }

    /// <summary>Macht die ausgewaehlte Zone zum virtuellen Monitor, oder hebt das Kennzeichen wieder auf.</summary>
    public void ToggleSelectedZoneAsFullscreenZone()
    {
        if (selectedZoneId is not Guid zoneId)
        {
            return;
        }

        session.SetVirtualMonitor(zoneId, !IsSelectedZoneFullscreenZone);
        RecordAction("Vollbildzone geändert");
        NotifyStateChanged();
        NotifyConfigurationChanged();
    }

    /// <summary>Ersetzt alle Zonen, etwa durch die einzelne Vollzone eines leeren Layouts.</summary>
    public void ReplaceZones(IReadOnlyList<ZoneDefinition> zones)
    {
        ArgumentNullException.ThrowIfNull(zones);
        session.ReplaceZones(zones);
        selectedZoneId = Zones.FirstOrDefault()?.Id;
        RecordAction("Zonen ersetzt");
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

        RecordAction("Zone entfernt");
        NotifyStateChanged();
        NotifyConfigurationChanged();
        return true;
    }

    /// <summary>
    /// Setzt die Startzone dieses Layouts auf eine bestimmte Zone; <c>null</c> hebt sie auf. Wird fuer
    /// «Rueckgaengig» gebraucht, wo nicht umgeschaltet, sondern ein frueherer Stand hergestellt wird.
    /// </summary>
    public void SetStartZone(Guid? zoneId)
    {
        if (zoneId is Guid wanted && Zones.All(zone => zone.Id != wanted))
        {
            return;
        }

        session.SetStartZone(zoneId);
        RecordAction("Startzone geändert");
        NotifyStateChanged();
        NotifyConfigurationChanged();
    }

    /// <summary>Macht eine Zone zur Startzone oder hebt die Markierung auf, wenn sie es schon ist.</summary>
    public void ToggleStartZone(Guid zoneId)
    {
        if (Zones.All(zone => zone.Id != zoneId))
        {
            return;
        }

        session.SetStartZone(session.StartZoneId == zoneId ? null : zoneId);
        RecordAction("Startzone geändert");
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
        RecordAction("Zone umbenannt");
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
        if (session.StartZoneId is null && (zone.Id == StartZoneId || neighbour.Id == StartZoneId))
        {
            session.SetStartZone(merged.Id);
        }

        selectedZoneId = merged.Id;
        RecordAction("Zonen verbunden");
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
        RecordAction("Zone umbenannt");
        NotifyStateChanged();
        NotifyConfigurationChanged();
    }

    public void Reset()
    {
        session.Reset();
        selectedZoneId = Zones.FirstOrDefault()?.Id;
        RecordAction("Entwurf verworfen");
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
    public bool Undo()
    {
        var undone = lastActionLabel;
        if (!Travel(session.Undo))
        {
            return false;
        }

        redoLabel = undone;
        lastActionLabel = string.Empty;
        NotifyStateChanged();
        return true;
    }

    public bool Redo()
    {
        var redone = redoLabel;
        if (!Travel(session.Redo))
        {
            return false;
        }

        lastActionLabel = redone;
        redoLabel = string.Empty;
        NotifyStateChanged();
        return true;
    }

    /// <summary>
    /// Haelt fest, was zuletzt geschehen ist. Der Text steht auf der Layout-Flaeche neben ↶ und sagt,
    /// was ein Klick zuruecknehmen wuerde; ein neuer Schritt macht die Wiederherstellung hinfaellig.
    /// </summary>
    private void RecordAction(string label)
    {
        lastActionLabel = label;
        redoLabel = string.Empty;
    }

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
        OnPropertyChanged(nameof(StartZoneId));
        OnPropertyChanged(nameof(IsSelectedZoneStartZone));
        OnPropertyChanged(nameof(StartZoneActionLabel));
        OnPropertyChanged(nameof(StartZoneStateText));
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(IsValid));
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(UndoLabel));
        OnPropertyChanged(nameof(RedoLabel));
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
