using System.Windows;
using System.Windows.Controls;
using ZoneManager.App.Views;
using ZoneManager.App.ViewModels;
using ZoneManager.Core.Geometry;
using ZoneManager.Core.Models;
using ZoneManager.Core.Monitors;
using ZoneManager.Tests.Support;
using Xunit;

namespace ZoneManager.Tests.Theme;

public sealed class MainWindowNavigationTests
{
    [Fact]
    public void Main_window_uses_the_requested_navigation_order_and_opens_layouts()
    {
        WpfThemeHost.Invoke(() =>
        {
            var window = new MainWindow();
            var root = Assert.IsType<Grid>(window.Content);
            var tabs = Assert.Single(root.Children.OfType<TabControl>());

            Assert.Equal(
                ["Monitore", "Layouts", "Regeln", "Skalierung", "Einstellungen", "Import & Export"],
                tabs.Items.OfType<TabItem>().Select(item => item.Header?.ToString() ?? string.Empty).ToArray());
            Assert.Equal("Layouts", Assert.IsType<TabItem>(tabs.SelectedItem).Header);
        });
    }

    [Fact]
    public void Main_window_does_not_render_the_sidebar_label_or_statusbar()
    {
        WpfThemeHost.Invoke(() =>
        {
            var window = new MainWindow();
            var root = Assert.IsType<Grid>(window.Content);

            Assert.DoesNotContain(
                LogicalDescendants<TextBlock>(window),
                textBlock => string.Equals(textBlock.Text, "BEREICHE", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(root.Children.OfType<Border>(), border => Grid.GetRow(border) == 2);
        });
    }

    [Fact]
    public void Attached_layout_editor_displays_the_layout_name_and_all_eight_measurements()
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

            Assert.Equal("Arbeit", Assert.IsType<TextBox>(window.FindName("LayoutNameText")).Text);
            Assert.All(
                new[]
                {
                    "ZonePositionXText", "ZonePositionYText", "ZoneWidthText", "ZoneHeightText",
                    "ZoneMarginLeftText", "ZoneMarginTopText", "ZoneMarginRightText", "ZoneMarginBottomText"
                },
                name => Assert.False(
                    string.IsNullOrWhiteSpace(Assert.IsType<TextBox>(window.FindName(name)).Text),
                    $"Das Formularfeld {name} darf nicht leer sein."));
        });
    }

    private static IEnumerable<T> LogicalDescendants<T>(DependencyObject parent) where T : DependencyObject
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
