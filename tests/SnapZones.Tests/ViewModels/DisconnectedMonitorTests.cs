using SnapZones.App.ViewModels;
using SnapZones.Core.Geometry;
using SnapZones.Core.Models;
using SnapZones.Core.Monitors;
using Xunit;

namespace SnapZones.Tests.ViewModels;

public sealed class DisconnectedMonitorTests
{
    private static readonly MonitorIdentity Connected = new("CONNECTED", "DISPLAY1", "Monitor A");
    private static readonly MonitorIdentity Gone = new("GONE", "DISPLAY9", "Alter Monitor");

    private static ZoneDefinition Zone(string name) => new(Guid.NewGuid(), name, NormalizedRect.Full);

    private static SnapConfiguration ConfigurationWithOrphan(int orphanLayouts = 1)
    {
        var layouts = new List<MonitorLayout>
        {
            new(Connected, 2560, 1440, [Zone("Voll")])
        };
        for (var index = 0; index < orphanLayouts; index++)
        {
            layouts.Add(new MonitorLayout(Gone, 1920, 1080, [Zone("Voll")])
            {
                Name = $"Alt {index + 1}",
                IsActive = index == 0
            });
        }

        return new SnapConfiguration(
            SnapConfiguration.CurrentSchemaVersion,
            AppSettings.Default(Guid.Empty),
            layouts);
    }

    private static MainViewModel CreateViewModel(SnapConfiguration configuration) => new(
        configuration,
        [new LiveMonitor(Connected, new MonitorWorkArea(0, 0, 2560, 1440), 96, 96, true)]);

    [Fact]
    public void A_monitor_that_is_no_longer_connected_stays_visible_while_it_still_has_layouts()
    {
        var viewModel = CreateViewModel(ConfigurationWithOrphan());

        // Ohne diesen Eintrag taucht der Monitor nur noch als Regelziel auf und seine Layouts
        // sind in der Oberflaeche unerreichbar.
        var orphan = Assert.Single(viewModel.Monitors, choice => !choice.IsConnected);

        Assert.Equal("GONE", orphan.Live.Identity.StableId);
        Assert.Contains("nicht verbunden", orphan.DetailsText, StringComparison.Ordinal);
        Assert.NotNull(orphan.ConnectionNote);
        Assert.Equal(2, viewModel.Monitors.Count);
    }

    [Fact]
    public void A_connected_monitor_carries_no_disconnection_note()
    {
        var viewModel = CreateViewModel(ConfigurationWithOrphan());
        var connected = Assert.Single(viewModel.Monitors, choice => choice.IsConnected);

        Assert.Null(connected.ConnectionNote);
        Assert.DoesNotContain("nicht verbunden", connected.DetailsText, StringComparison.Ordinal);
    }

    [Fact]
    public void The_last_layout_of_a_disconnected_monitor_can_be_deleted_and_removes_the_monitor()
    {
        var viewModel = CreateViewModel(ConfigurationWithOrphan());
        viewModel.SelectedMonitor = viewModel.Monitors.Single(choice => !choice.IsConnected);

        Assert.Single(viewModel.Layouts);
        Assert.True(viewModel.CanDeleteSelectedLayout, "Das letzte Layout eines getrennten Monitors muss loeschbar sein.");

        viewModel.DeleteSelectedLayout();

        Assert.DoesNotContain(viewModel.Monitors, choice => !choice.IsConnected);
        Assert.DoesNotContain(viewModel.Configuration.Layouts, layout => layout.Monitor.StableId == "GONE");
    }

    [Fact]
    public void Deleting_one_of_several_orphan_layouts_keeps_the_monitor_listed()
    {
        var viewModel = CreateViewModel(ConfigurationWithOrphan(orphanLayouts: 2));
        viewModel.SelectedMonitor = viewModel.Monitors.Single(choice => !choice.IsConnected);

        Assert.Equal(2, viewModel.Layouts.Count);
        viewModel.DeleteSelectedLayout();

        Assert.Contains(viewModel.Monitors, choice => !choice.IsConnected);
        Assert.Single(viewModel.Configuration.Layouts, layout => layout.Monitor.StableId == "GONE");
    }

    [Fact]
    public void The_last_layout_of_a_connected_monitor_stays_protected()
    {
        var viewModel = CreateViewModel(ConfigurationWithOrphan());
        viewModel.SelectedMonitor = viewModel.Monitors.Single(choice => choice.IsConnected);

        Assert.Single(viewModel.Layouts);
        Assert.False(viewModel.CanDeleteSelectedLayout);
    }
}
