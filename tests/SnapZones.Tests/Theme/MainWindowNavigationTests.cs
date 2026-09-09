using System.Windows;
using System.Windows.Controls;
using SnapZones.App.Controls;
using SnapZones.App.Views;
using SnapZones.App.ViewModels;
using SnapZones.Core.Geometry;
using SnapZones.Core.Models;
using SnapZones.Core.Monitors;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.Theme;

public sealed class MainWindowNavigationTests
{
    [Fact]
    public void Main_window_groups_the_fourteen_pages_and_opens_zones_and_layouts()
    {
        WpfThemeHost.Invoke(() =>
        {
            var window = new MainWindow();
            var root = Assert.IsType<Grid>(window.Content);
            var tabs = Assert.Single(root.Children.OfType<TabControl>());
            var pages = tabs.Items.OfType<TabItem>().ToArray();

            Assert.Equal(
                [
                    "Zonen & Layouts", "Monitore", "Fenster zuordnen", "In Ruhe lassen",
                    "Ziehen & Einrasten", "Aussehen der Zonen", "Abstände & Raster", "Fenster merken",
                    "Vollbild & Videos", "Tastenkürzel",
                    "Aussehen & Start", "Sicherung & Stände", "System & Rechte", "Über Zone Manager"
                ],
                pages.Select(item => item.Header?.ToString() ?? string.Empty).ToArray());
            Assert.Equal("Zonen & Layouts", Assert.IsType<TabItem>(tabs.SelectedItem).Header);

            // Drei Gruppen: die Ueberschrift haengt am ersten Eintrag jeder Gruppe.
            Assert.Equal("EINRICHTEN", Chrome.GetGroup(pages[0]));
            Assert.Equal("VERHALTEN", Chrome.GetGroup(pages[4]));
            Assert.Equal("PROGRAMM", Chrome.GetGroup(pages[10]));
            Assert.Equal(string.Empty, Chrome.GetGroup(pages[1]));
        });
    }

    [Fact]
    public void No_page_carries_a_title_or_a_width_limit()
    {
        WpfThemeHost.Invoke(() =>
        {
            var window = new MainWindow();
            var tabs = Assert.Single(Assert.IsType<Grid>(window.Content).Children.OfType<TabControl>());

            foreach (var page in tabs.Items.OfType<TabItem>())
            {
                Assert.DoesNotContain(
                    UiTree.LogicalDescendants<TextBlock>(page),
                    text => text.Style == Application.Current.Resources["PageTitle"]);
                Assert.DoesNotContain(
                    UiTree.LogicalDescendants<Panel>(page),
                    panel => !double.IsPositiveInfinity(panel.MaxWidth));
            }
        });
    }

    [Fact]
    public void Sidebar_counts_follow_the_configuration()
    {
        WpfThemeHost.Invoke(() =>
        {
            var window = new MainWindow { Left = -10000 };
            var viewModel = new MainViewModel(ConfigurationSamples.TwoLayouts(), []);
            window.AttachViewModel(viewModel);
            var tabs = Assert.Single(Assert.IsType<Grid>(window.Content).Children.OfType<TabControl>());
            var pages = tabs.Items.OfType<TabItem>().ToArray();
            window.Show();
            try
            {
                window.UpdateLayout();
                Assert.Equal("2", Chrome.GetBadge(pages[0]));
                Assert.Equal("1", Chrome.GetBadge(pages[1]));
                Assert.Equal("0", Chrome.GetBadge(pages[2]));
                Assert.Equal("0", Chrome.GetBadge(pages[3]));

                viewModel.AppExclusions.AddExclusion("notepad.exe");
                window.UpdateLayout();

                Assert.Equal("1", Chrome.GetBadge(pages[3]));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void The_search_navigates_to_the_page_opens_its_tuning_and_clears_itself()
    {
        WpfThemeHost.Invoke(() =>
        {
            var window = new MainWindow();
            var viewModel = new MainViewModel(ConfigurationSamples.TwoLayouts(), []);
            window.AttachViewModel(viewModel);
            var tabs = Assert.Single(Assert.IsType<Grid>(window.Content).Children.OfType<TabControl>());

            viewModel.SearchQuery = "Wachhund";

            Assert.True(viewModel.HasSearchQuery);
            var result = Assert.Single(viewModel.SearchResults);
            Assert.Equal("Ziehen & Einrasten › Feinabstimmung", result.Path);

            window.ShowPage(result.Page, result.TuningSection);
            viewModel.ClearSearch();

            Assert.Equal("Ziehen & Einrasten", Assert.IsType<TabItem>(tabs.SelectedItem).Header);
            Assert.True(Assert.IsType<TuningExpander>(window.FindName("DragTuning")).IsExpanded);
            Assert.Empty(viewModel.SearchResults);

            viewModel.SearchQuery = "xyzzy";
            Assert.True(viewModel.HasNoSearchResults);
        });
    }

    [Fact]
    public void Main_window_has_a_status_bar_without_a_permanent_state_pill()
    {
        WpfThemeHost.Invoke(() =>
        {
            var window = new MainWindow();
            var root = Assert.IsType<Grid>(window.Content);

            Assert.DoesNotContain(
                UiTree.LogicalDescendants<TextBlock>(window),
                textBlock => string.Equals(textBlock.Text, "BEREICHE", StringComparison.OrdinalIgnoreCase));

            // Die Statuszeile ist die einzige Stelle, an der StatusMessage sichtbar wird; die Pille
            // «Einrasten aktiv» ist seit dem 05.09.2026 weg, nur ein angehaltenes Einrasten erscheint.
            var statusBar = Assert.Single(root.Children.OfType<Border>(), border => Grid.GetRow(border) == 2);
            var message = Assert.IsType<TextBlock>(window.FindName("StatusMessageText"));
            Assert.Equal("StatusMessage", message.GetBindingExpression(TextBlock.TextProperty)!.ParentBinding.Path.Path);
            Assert.True(statusBar.IsDescendantOf(root) || ReferenceEquals(statusBar.Parent, root));
            Assert.Null(window.FindName("SnappingStatePill"));
            // Nur die Nummer, ohne das Wort «Version».
            Assert.DoesNotContain("Version", Assert.IsType<TextBlock>(window.FindName("VersionLabel")).Text, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Attached_layout_editor_fills_all_eight_measurements()
    {
        WpfThemeHost.Invoke(() =>
        {
            var monitorIdentity = new MonitorIdentity("DISPLAY-A", "\\\\.\\DISPLAY1", "Hauptmonitor");
            var monitor = new LiveMonitor(
                monitorIdentity,
                new MonitorWorkArea(0, 0, 3440, 1440),
                96,
                96,
                true);
            var window = new MainWindow();
            window.AttachViewModel(new MainViewModel(ConfigurationSamples.TwoLayouts(), [monitor]));
            var panel = Assert.IsType<ZoneValuesPanel>(window.FindName("ZoneValues"));

            Assert.Equal("Zone 1", Assert.IsType<TextBlock>(panel.FindName("ZoneTitleText")).Text);
            Assert.All(
                new[]
                {
                    "ZonePositionXText", "ZonePositionYText", "ZoneWidthText", "ZoneHeightText",
                    "ZoneMarginLeftText", "ZoneMarginTopText", "ZoneMarginRightText", "ZoneMarginBottomText"
                },
                name => Assert.False(
                    string.IsNullOrWhiteSpace(Assert.IsType<TextBox>(panel.FindName(name)).Text),
                    $"Das Formularfeld {name} darf nicht leer sein."));
        });
    }
}
