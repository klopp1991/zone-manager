using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using SnapZones.App.Controls;
using SnapZones.App.ViewModels;
using SnapZones.App.Views;
using SnapZones.Core.Geometry;
using SnapZones.Core.Models;
using SnapZones.Core.Monitors;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.Theme;

public sealed class MonitorManagementUiTests
{
    [Fact]
    public void Main_window_keeps_monitor_renaming_on_a_separate_monitor_page()
    {
        WpfThemeHost.Invoke(() =>
        {
            var window = new MainWindow();
            var root = Assert.IsType<Grid>(window.Content);
            var tabs = Assert.Single(root.Children.OfType<TabControl>());
            var pages = tabs.Items.OfType<TabItem>().ToArray();
            var layoutsPage = pages.Single(page => Equals(page.Header, "Zonen & Layouts"));
            var monitorsPage = pages.Single(page => Equals(page.Header, "Monitore"));
            var nameField = Assert.IsType<TextBox>(window.FindName("MonitorNameText"));
            var identifyButton = Assert.IsType<Button>(window.FindName("IdentifyMonitorsButton"));

            // Die Skalierungswerte stehen auf derselben Seite wie die Monitore, aufklappbar.
            Assert.DoesNotContain(pages, page => Equals(page.Header, "Skalierung"));
            Assert.Contains(Assert.IsType<TextBlock>(window.FindName("ScalingResolutionText")), UiTree.LogicalDescendants<TextBlock>(monitorsPage));
            Assert.Contains(nameField, UiTree.LogicalDescendants<TextBox>(monitorsPage));
            Assert.Contains(identifyButton, UiTree.LogicalDescendants<Button>(monitorsPage));
            Assert.Contains(Assert.IsType<ZonePreview>(window.FindName("MonitorPreview")), UiTree.LogicalDescendants<ZonePreview>(monitorsPage));
            Assert.DoesNotContain(nameField, UiTree.LogicalDescendants<TextBox>(layoutsPage));
            Assert.Equal("Eigener Monitorname", AutomationProperties.GetName(nameField));
            Assert.True(Assert.IsType<Expander>(window.FindName("DetectedValuesExpander")).IsExpanded);
        });
    }

    [Fact]
    public void Monitor_page_lists_one_tab_per_monitor_and_names_how_many_are_connected()
    {
        WpfThemeHost.Invoke(() =>
        {
            var viewModel = TwoMonitors(out var window);
            var tabs = Assert.IsType<ItemsControl>(window.FindName("MonitorTabs"));

            Assert.Equal(
                "Monitors",
                tabs.GetBindingExpression(ItemsControl.ItemsSourceProperty)!.ParentBinding.Path.Path);
            Assert.Equal("Angeschlossen: 2 Monitore", viewModel.MonitorConnectionSummary);
            Assert.All(viewModel.Monitors, monitor => Assert.Equal(string.Empty, monitor.ConnectionSuffix));

            // Ein abgestecktes Gerät behält seinen Tab und sagt in der Zeile, dass es fehlt.
            var unplugged = viewModel.Monitors[0] with { IsConnected = false };
            Assert.Equal(" · nicht angeschlossen", unplugged.ConnectionSuffix);
        });
    }

    [Fact]
    public void Monitor_page_moves_the_selected_monitor_and_updates_the_shared_dropdown_order()
    {
        WpfThemeHost.Invoke(() =>
        {
            var viewModel = TwoMonitors(out _);
            viewModel.SelectedMonitor = viewModel.Monitors[1];

            Assert.True(viewModel.CanMoveMonitorLeft);
            Assert.False(viewModel.CanMoveMonitorRight);
            viewModel.MoveMonitorLeft();

            Assert.Equal(["SECOND", "FIRST"], viewModel.Monitors.Select(monitor => monitor.Live.Identity.StableId));
            Assert.Equal("SECOND", viewModel.SelectedMonitor!.Live.Identity.StableId);
            Assert.Equal(["stable:SECOND", "stable:FIRST"], viewModel.Configuration.MonitorOrder);

            // Ziehen fuehrt auf denselben Weg.
            viewModel.MoveMonitorTo(viewModel.Monitors[0], viewModel.Monitors[1]);
            Assert.Equal(["FIRST", "SECOND"], viewModel.Monitors.Select(monitor => monitor.Live.Identity.StableId));
        });
    }

    [Fact]
    public void The_overview_card_describes_each_monitor_without_an_identifier()
    {
        var identity = new MonitorIdentity("TECHNICAL-ID", "\\\\.\\DISPLAY3", "Dell U2723QE");
        var monitor = new LiveMonitor(identity, new MonitorWorkArea(0, 0, 3840, 2100), 120, 120, false, Bounds: new PixelRect(0, 0, 3840, 2160));
        var layout = new MonitorLayout(identity, 3840, 2100, []);
        var choice = new MonitorChoice(monitor, layout, 3, "Rechts") { Layouts = [layout, layout with { Id = Guid.NewGuid(), Name = "Abend" }] };

        Assert.Equal("Rechts", choice.UserFacingName);
        Assert.Equal("3840 × 2160 · 125 %", choice.OverviewDetailsText);
        Assert.Equal("2 Layouts", choice.LayoutCountText);
        Assert.Equal(3840d / 2100d, choice.AspectRatio, 6);
        Assert.DoesNotContain("TECHNICAL-ID", choice.UserFacingName, StringComparison.Ordinal);
    }

    private static MainViewModel TwoMonitors(out MainWindow window)
    {
        var firstIdentity = new MonitorIdentity("FIRST", "DISPLAY1", "Erster Monitor");
        var secondIdentity = new MonitorIdentity("SECOND", "DISPLAY2", "Zweiter Monitor");
        var monitors = new[]
        {
            new LiveMonitor(firstIdentity, new MonitorWorkArea(0, 0, 2560, 1440), 96, 96, true),
            new LiveMonitor(secondIdentity, new MonitorWorkArea(2560, 0, 1920, 1080), 96, 96, false)
        };
        var configuration = new SnapConfiguration(
            SnapConfiguration.CurrentSchemaVersion,
            AppSettings.Default(Guid.Empty),
            [
                new MonitorLayout(firstIdentity, 2560, 1440, [new ZoneDefinition(Guid.NewGuid(), "Voll", NormalizedRect.Full)]),
                new MonitorLayout(secondIdentity, 1920, 1080, [new ZoneDefinition(Guid.NewGuid(), "Voll", NormalizedRect.Full)])
            ]);
        var viewModel = new MainViewModel(configuration, monitors);
        window = new MainWindow();
        window.AttachViewModel(viewModel);
        return viewModel;
    }
}
