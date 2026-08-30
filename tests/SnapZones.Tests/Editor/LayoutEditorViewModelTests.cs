using SnapZones.App.ViewModels;
using SnapZones.Core.Editor;
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

    private static MonitorLayout FullMonitorLayout() => new(
        new MonitorIdentity("A", "DISPLAY1", "Hauptmonitor"),
        3440,
        1440,
        [new ZoneDefinition(Guid.NewGuid(), "Voll", NormalizedRect.Full)]);
}
