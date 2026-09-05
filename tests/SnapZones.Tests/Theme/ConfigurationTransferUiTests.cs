using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows;
using SnapZones.App.Views;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.Theme;

public sealed class ConfigurationTransferUiTests
{
    [Fact]
    public void Main_window_places_accessible_export_and_import_actions_in_their_own_navigation_page()
    {
        WpfThemeHost.Invoke(() =>
        {
            var window = new MainWindow();
            var root = Assert.IsType<Grid>(window.Content);
            var tabs = Assert.Single(root.Children.OfType<TabControl>());
            var transferPage = tabs.Items
                .OfType<TabItem>()
                .Single(item => Equals(item.Header, "Programm"));
            var exportButton = Assert.IsType<Button>(window.FindName("ExportConfigurationButton"));
            var importButton = Assert.IsType<Button>(window.FindName("ImportConfigurationButton"));
            var pageButtons = UiTree.LogicalDescendants<Button>(transferPage).ToArray();
            var header = root.Children
                .OfType<Border>()
                .Single(border => Grid.GetRow(border) == 0);
            var headerButtons = UiTree.LogicalDescendants<Button>(header).ToArray();

            Assert.Equal("Vollständig exportieren", AutomationProperties.GetName(exportButton));
            Assert.Equal("Vollständig importieren", AutomationProperties.GetName(importButton));
            Assert.Contains(exportButton, pageButtons);
            Assert.Contains(importButton, pageButtons);
            Assert.DoesNotContain(exportButton, headerButtons);
            Assert.DoesNotContain(importButton, headerButtons);

            // Beide Aktionen sind gleichrangig und teilen sich denselben Schaltflaechenstil.
            Assert.Same(exportButton.Style, importButton.Style);
            Assert.NotSame(window.FindResource("PrimaryButton"), exportButton.Style);
        });
    }
}
