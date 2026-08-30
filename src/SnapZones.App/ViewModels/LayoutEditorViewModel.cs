using SnapZones.Core.Editor;
using SnapZones.Core.Models;

namespace SnapZones.App.ViewModels;

public sealed class LayoutEditorViewModel : ViewModelBase
{
    private readonly LayoutEditorSession session;
    private Guid? selectedZoneId;

    public LayoutEditorViewModel(MonitorLayout layout)
    {
        session = new LayoutEditorSession(layout);
        selectedZoneId = session.Zones.FirstOrDefault()?.Id;
    }

    public IReadOnlyList<ZoneDefinition> Zones => session.Zones;
    public ZoneDefinition? SelectedZone => Zones.FirstOrDefault(zone => zone.Id == selectedZoneId);
    public bool IsDirty => session.IsDirty;
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

    public void AddZone()
    {
        var offset = Math.Min(0.45, Zones.Count * 0.04);
        var zone = session.AddZone($"Zone {Zones.Count + 1}", new NormalizedRect(offset, offset, 0.4, 0.4));
        selectedZoneId = zone.Id;
        NotifyStateChanged();
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
    }

    public void ApplyTemplate(LayoutTemplate template)
    {
        session.ReplaceZones(LayoutTemplates.Create(template));
        selectedZoneId = Zones[0].Id;
        NotifyStateChanged();
    }

    public void UpdateSelectedZone(string name, double xPercent, double yPercent, double widthPercent, double heightPercent)
    {
        if (selectedZoneId is null)
        {
            return;
        }

        var bounds = new NormalizedRect(
            xPercent / 100d,
            yPercent / 100d,
            widthPercent / 100d,
            heightPercent / 100d);
        session.UpdateZone(selectedZoneId.Value, name, bounds);
        NotifyStateChanged();
    }

    public void MoveOrResizeZone(Guid zoneId, NormalizedRect bounds)
    {
        session.MoveZone(zoneId, bounds);
        selectedZoneId = zoneId;
        NotifyStateChanged();
    }

    public void Reset()
    {
        session.Reset();
        selectedZoneId = Zones.FirstOrDefault()?.Id;
        NotifyStateChanged();
    }

    public MonitorLayout CreateSnapshot() => session.CreateSnapshot();

    private void NotifyStateChanged()
    {
        OnPropertyChanged(nameof(Zones));
        OnPropertyChanged(nameof(SelectedZone));
        OnPropertyChanged(nameof(IsDirty));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(ValidationMessage));
    }
}
