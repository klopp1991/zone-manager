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

    /// <summary>Die als Hauptzone markierte Zone dieses Layouts, falls es eine gibt.</summary>
    public Guid? MainZoneId => session.MainZoneId;

    /// <summary>Ob die gerade ausgewählte Zone die Hauptzone ist.</summary>
    public bool IsSelectedZoneMainZone => SelectedZone is { } zone && session.MainZoneId == zone.Id;

    /// <summary>Beschriftung der einen Schaltfläche; sie führt in beide Richtungen.</summary>
    public string MainZoneActionLabel => IsSelectedZoneMainZone
        ? "Hauptzone aufheben"
        : "Als Hauptzone festlegen";

    /// <summary>Was in diesem Layout gilt, im Klartext und ohne Farbe.</summary>
    public string MainZoneStateText => session.MainZoneId is null
        ? "Keine Zone dieses Layouts ist Hauptzone."
        : IsSelectedZoneMainZone
            ? "Diese Zone ist die Hauptzone dieses Layouts."
            : $"Hauptzone dieses Layouts ist «{Zones.First(zone => zone.Id == session.MainZoneId).Name}».";
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
