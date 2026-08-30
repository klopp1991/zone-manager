using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;
using SnapZones.Presentation.ViewModels;
using SnapZones.App.Views;
using SnapZones.Core.Geometry;
using SnapZones.Core.Models;
using SnapZones.Core.Monitors;
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
            var layoutsPage = pages.Single(page => Equals(page.Header, "Layouts"));
            var monitorsPage = pages.Single(page => Equals(page.Header, "Monitore"));
            var windowsDisplayPage = pages.Single(page => Equals(page.Header, "Windows-Anzeige"));
            var nameField = Assert.IsType<TextBox>(window.FindName("MonitorNameText"));
            var managementList = Assert.IsType<ListBox>(window.FindName("MonitorManagementList"));
            var identifyButton = Assert.IsType<Button>(window.FindName("IdentifyMonitorsButton"));

            Assert.True(Array.IndexOf(pages, monitorsPage) < Array.IndexOf(pages, windowsDisplayPage));
            Assert.Contains(nameField, LogicalDescendants<TextBox>(monitorsPage));
            Assert.Contains(managementList, LogicalDescendants<ListBox>(monitorsPage));
            Assert.Contains(identifyButton, LogicalDescendants<Button>(monitorsPage));
            Assert.DoesNotContain(nameField, LogicalDescendants<TextBox>(layoutsPage));
            Assert.Equal("Eigener Monitorname", AutomationProperties.GetName(nameField));
        });
    }

    [Fact]
    public void Monitor_page_lists_all_monitors_and_changes_the_shared_selection()
    {
        WpfThemeHost.Invoke(() =>
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
            var window = new MainWindow();
            window.AttachViewModel(viewModel);
            var root = Assert.IsType<Grid>(window.Content);
            var tabs = Assert.Single(root.Children.OfType<TabControl>());
            tabs.SelectedItem = tabs.Items.OfType<TabItem>().Single(item => Equals(item.Header, "Monitore"));
            root.Measure(new Size(1180, 720));
            root.Arrange(new Rect(0, 0, 1180, 720));
            root.UpdateLayout();
            var managementList = Assert.IsType<ListBox>(window.FindName("MonitorManagementList"));

            Assert.Equal(2, managementList.Items.Count);
            managementList.SelectedIndex = 1;

            Assert.Equal("SECOND", viewModel.SelectedMonitor?.Live.Identity.StableId);
            Assert.Same(viewModel.SelectedMonitor, managementList.SelectedItem);
        });
    }

    [Fact]
    public void Monitor_template_binds_the_user_facing_name_instead_of_an_identifier()
    {
        WpfThemeHost.Invoke(() =>
        {
            var window = new MainWindow();
            var identity = new MonitorIdentity("TECHNICAL-ID", "\\\\.\\DISPLAY3", "Dell U2723QE");
            var monitor = new LiveMonitor(identity, new MonitorWorkArea(0, 0, 3840, 2160), 96, 96, false);
            var choice = new MonitorChoice(monitor, new MonitorLayout(identity, 3840, 2160, []), 3, "Rechts");
            var template = Assert.IsType<DataTemplate>(window.FindResource("MonitorItemTemplate"));
            var content = Assert.IsType<StackPanel>(template.LoadContent());
            var name = Assert.IsType<TextBlock>(content.Children[0]);
            var binding = Assert.IsType<Binding>(BindingOperations.GetBinding(name, TextBlock.TextProperty));

            Assert.Equal(nameof(MonitorChoice.UserFacingName), binding.Path.Path);
            Assert.Equal("Rechts", choice.UserFacingName);
            Assert.DoesNotContain("TECHNICAL-ID", choice.UserFacingName, StringComparison.Ordinal);
        });
    }

    private static IEnumerable<T> LogicalDescendants<T>(DependencyObject parent)
        where T : DependencyObject
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in LogicalDescendants<T>(child))
            {
                yield return descendant;
            }
        }
    }
}
