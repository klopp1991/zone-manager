using SnapZones.Presentation.ViewModels;
using SnapZones.Core.Editor;
using SnapZones.Core.Geometry;
using SnapZones.Core.Models;
using Xunit;

namespace SnapZones.Tests.Editor;

public sealed class LayoutEditorViewModelTests
{
    [Fact]
    public void ApplyTemplate_replaces_draft_and_selects_first_zone()
    {
        var viewModel = new LayoutEditorViewModel(FullMonitorLayout());

        viewModel.ApplyTemplate(LayoutTemplate.ThreeColumns);

        Assert.Equal(3, viewModel.Zones.Count);
        Assert.Equal(viewModel.Zones[0].Id, viewModel.SelectedZone?.Id);
        Assert.True(viewModel.IsDirty);
        Assert.True(viewModel.CanSave);
    }

    [Fact]
    public void UpdateZone_changes_normalized_bounds_from_percent_values()
    {
        var viewModel = new LayoutEditorViewModel(FullMonitorLayout());
        viewModel.SelectZone(viewModel.Zones[0].Id);

        viewModel.UpdateSelectedZone("Haupt", 10, 5, 80, 90);

        Assert.Equal(new NormalizedRect(0.1, 0.05, 0.8, 0.9), viewModel.SelectedZone?.Bounds);
        Assert.True(viewModel.CanSave);
    }

    [Fact]
    public void AddZone_uses_largest_free_area_beside_existing_half_width_zone()
    {
        var layout = FullMonitorLayout() with
        {
            Zones = [new ZoneDefinition(Guid.NewGuid(), "Links", new NormalizedRect(0, 0, 0.5, 1))]
        };
        var viewModel = new LayoutEditorViewModel(layout);

        var added = viewModel.AddZone();

        Assert.True(added);
        Assert.Equal(new NormalizedRect(0.5, 0, 0.5, 1), viewModel.SelectedZone?.Bounds);
    }

    [Fact]
    public void AddZone_returns_false_without_changing_fully_occupied_layout()
    {
        var viewModel = new LayoutEditorViewModel(FullMonitorLayout());

        var added = viewModel.AddZone();

        Assert.False(added);
        Assert.Single(viewModel.Zones);
    }

    [Fact]
    public void UpdateSelectedZoneFromMargins_accepts_pixel_input()
    {
        var viewModel = new LayoutEditorViewModel(FullMonitorLayout());

        viewModel.UpdateSelectedZoneFromMargins("Mitte", 344, 144, 688, 288, MeasurementUnit.Pixels);

        Assert.Equal(new NormalizedRect(0.1, 0.1, 0.7, 0.7), viewModel.SelectedZone?.Bounds);
    }

    [Fact]
    public void GetSelectedValues_returns_synchronized_percent_values()
    {
        var viewModel = new LayoutEditorViewModel(FullMonitorLayout());
        viewModel.UpdateSelectedZoneFromPositionAndSize("Haupt", 344, 144, 1720, 720, MeasurementUnit.Pixels);

        var values = viewModel.GetSelectedValues(MeasurementUnit.Percent);

        Assert.Equal(new ZoneEditorValues(10, 10, 40, 40, 50, 50), values);
    }

    private static MonitorLayout FullMonitorLayout() => new(
        new MonitorIdentity("A", "DISPLAY1", "Hauptmonitor"),
        3440,
        1440,
        [new ZoneDefinition(Guid.NewGuid(), "Voll", NormalizedRect.Full)]);
}
