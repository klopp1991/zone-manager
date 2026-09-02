using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using SnapZones.App.Controls;
using SnapZones.App.ViewModels;
using SnapZones.App.Views;
using SnapZones.Core.Geometry;
using SnapZones.Core.Models;
using SnapZones.Core.Monitors;
using Xunit;

namespace SnapZones.Tests.Theme;

public sealed class LayoutSuggestionPresentationTests
{
    [Fact]
    public void Main_window_header_omits_the_layout_summary()
    {
        WpfThemeHost.Invoke(() =>
        {
            var window = new MainWindow();
            var root = Assert.IsType<Grid>(window.Content);

            Assert.DoesNotContain(
                VisualDescendants<TextBlock>(root),
                textBlock => AutomationProperties.GetName(textBlock) == "Layoutübersicht");
        });
    }

    [Fact]
    public void Layout_header_controls_share_one_height_without_monitor_details()
    {
        WpfThemeHost.Invoke(() =>
        {
            var monitor = new LiveMonitor(
                new MonitorIdentity("MONITOR-A", "DISPLAY1", "Monitor A"),
                new MonitorWorkArea(0, 0, 5120, 1380),
                96,
                96,
                true,
                119,
                34);
            var viewModel = new MainViewModel(SnapConfiguration.CreateDefault(), [monitor]);
            var window = new MainWindow();
            window.AttachViewModel(viewModel);
            var root = Assert.IsType<Grid>(window.Content);
            var tabs = Assert.Single(root.Children.OfType<TabControl>());
            var layoutTab = tabs.Items.OfType<TabItem>().Single(item => Equals(item.Header, "Layouts"));
            tabs.SelectedItem = layoutTab;

            root.Measure(new Size(1480, 900));
            root.Arrange(new Rect(0, 0, 1480, 900));
            root.UpdateLayout();

            var addButton = LogicalDescendants<Button>(layoutTab)
                .Single(button => AutomationProperties.GetName(button) == "Neues Layout erstellen");
            var controls = new FrameworkElement[]
            {
                Assert.IsType<ComboBox>(window.FindName("LayoutMonitorSelector")),
                Assert.IsType<ComboBox>(window.FindName("LayoutSelector")),
                Assert.IsType<TextBox>(window.FindName("LayoutNameText")),
                addButton,
                Assert.IsType<Button>(window.FindName("DeleteLayoutButton"))
            };
            var expectedHeight = controls[0].ActualHeight;

            Assert.All(controls, control => Assert.Equal(expectedHeight, control.ActualHeight, 3));
            Assert.DoesNotContain(
                VisualDescendants<TextBlock>(controls[0]),
                textBlock => textBlock.Text == viewModel.Monitors[0].DetailsText);
        });
    }

    [Fact]
    public void Main_window_exposes_monitor_layout_management_without_a_profile_page()
    {
        WpfThemeHost.Invoke(() =>
        {
            var monitor = new LiveMonitor(
                new MonitorIdentity("MONITOR-A", "DISPLAY1", "Monitor A"),
                new MonitorWorkArea(0, 0, 2560, 1440),
                96,
                96,
                true);
            var viewModel = new MainViewModel(SnapConfiguration.CreateDefault(), [monitor]);
            var window = new MainWindow();
            window.AttachViewModel(viewModel);
            var root = Assert.IsType<Grid>(window.Content);
            var tabs = Assert.Single(root.Children.OfType<TabControl>());

            Assert.DoesNotContain(tabs.Items.OfType<TabItem>(), item => Equals(item.Header, "Profile"));
            Assert.IsType<ComboBox>(window.FindName("LayoutMonitorSelector"));
            Assert.IsType<ComboBox>(window.FindName("LayoutSelector"));
            Assert.IsType<TextBox>(window.FindName("LayoutNameText"));
            Assert.IsType<TextBox>(window.FindName("MonitorNameText"));
            Assert.IsType<Button>(window.FindName("IdentifyMonitorsButton"));
            var deleteButton = Assert.IsType<Button>(window.FindName("DeleteLayoutButton"));
            var binding = System.Windows.Data.BindingOperations.GetBinding(deleteButton, Button.IsEnabledProperty);
            Assert.Equal(nameof(MainViewModel.CanDeleteSelectedLayout), binding?.Path.Path);
        });
    }

    [Fact]
    public void Layout_page_uses_a_compact_monitor_selector_and_keeps_the_editor_wide()
    {
        WpfThemeHost.Invoke(() =>
        {
            var firstMonitor = new LiveMonitor(
                new MonitorIdentity("MONITOR-LEFT", "DISPLAY1", "Monitor links"),
                new MonitorWorkArea(0, 0, 2560, 1440),
                96,
                96,
                true,
                60,
                34);
            var secondMonitor = new LiveMonitor(
                new MonitorIdentity("MONITOR-RIGHT", "DISPLAY2", "Monitor rechts"),
                new MonitorWorkArea(2560, 0, 1920, 1080),
                96,
                96,
                false,
                53,
                30);
            var viewModel = new MainViewModel(SnapConfiguration.CreateDefault(), [firstMonitor, secondMonitor]);
            var window = new MainWindow();
            window.AttachViewModel(viewModel);
            var root = Assert.IsType<Grid>(window.Content);
            var tabs = Assert.Single(root.Children.OfType<TabControl>());
            tabs.SelectedItem = tabs.Items.OfType<TabItem>().Single(item => Equals(item.Header, "Layouts"));
            var size = new Size(1180, 720);

            root.Measure(size);
            root.Arrange(new Rect(size));
            root.UpdateLayout();

            var selector = Assert.IsType<ComboBox>(window.FindName("LayoutMonitorSelector"));
            var editorArea = Assert.IsType<Grid>(window.FindName("LayoutEditorArea"));
            selector.SelectedIndex = 1;
            root.UpdateLayout();

            Assert.Same(viewModel.Monitors[1], viewModel.SelectedMonitor);
            Assert.True(editorArea.ActualWidth >= 600,
                $"Der Layout-Editor ist bei Mindestbreite nur {editorArea.ActualWidth:0} Pixel breit.");
        });
    }

    [Theory]
    [InlineData(1180, 720)]
    [InlineData(1480, 900)]
    public void Layout_header_controls_keep_clear_of_the_editor_at_supported_window_sizes(double width, double height)
    {
        WpfThemeHost.Invoke(() =>
        {
            var monitor = new LiveMonitor(
                new MonitorIdentity("MONITOR-A", "DISPLAY1", "Monitor A"),
                new MonitorWorkArea(0, 0, 2560, 1440),
                96,
                96,
                true);
            var window = new MainWindow();
            window.AttachViewModel(new MainViewModel(SnapConfiguration.CreateDefault(), [monitor]));
            var root = Assert.IsType<Grid>(window.Content);

            root.Measure(new Size(width, height));
            root.Arrange(new Rect(0, 0, width, height));
            root.UpdateLayout();

            var editorArea = Assert.IsType<Grid>(window.FindName("LayoutEditorArea"));
            var editorBounds = BoundsRelativeTo(editorArea, root);
            var headerControls = new FrameworkElement[]
            {
                Assert.IsType<ComboBox>(window.FindName("LayoutMonitorSelector")),
                Assert.IsType<ComboBox>(window.FindName("LayoutSelector")),
                Assert.IsType<TextBox>(window.FindName("LayoutNameText")),
                Assert.IsType<TextBox>(window.FindName("MonitorNameText")),
                Assert.IsType<Button>(window.FindName("DeleteLayoutButton"))
            };

            Assert.All(headerControls, control =>
            {
                var bounds = BoundsRelativeTo(control, root);
                Assert.True(bounds.Bottom + 12 <= editorBounds.Top,
                    $"{control.Name} endet bei {bounds.Bottom:0.0}, der Editor beginnt bereits bei {editorBounds.Top:0.0}.");
            });
        });
    }

    [Fact]
    public void Layout_header_actions_stay_inside_the_page_at_minimum_width()
    {
        WpfThemeHost.Invoke(() =>
        {
            var monitor = new LiveMonitor(
                new MonitorIdentity("MONITOR-A", "DISPLAY1", "Monitor A"),
                new MonitorWorkArea(0, 0, 2560, 1440),
                96,
                96,
                true);
            var window = new MainWindow();
            window.AttachViewModel(new MainViewModel(SnapConfiguration.CreateDefault(), [monitor]));
            var root = Assert.IsType<Grid>(window.Content);
            var tabs = Assert.Single(root.Children.OfType<TabControl>());
            var layoutTab = tabs.Items.OfType<TabItem>().Single(item => Equals(item.Header, "Layouts"));
            var page = Assert.IsType<Grid>(layoutTab.Content);
            var size = new Size(1180, 720);

            root.Measure(size);
            root.Arrange(new Rect(size));
            root.UpdateLayout();

            var deleteButton = Assert.IsType<Button>(window.FindName("DeleteLayoutButton"));
            var pageBounds = BoundsRelativeTo(page, root);
            var buttonBounds = BoundsRelativeTo(deleteButton, root);

            Assert.True(buttonBounds.Right <= pageBounds.Right,
                $"Die Layoutaktion endet bei {buttonBounds.Right:0.0}, die Seite bereits bei {pageBounds.Right:0.0}.");
        });
    }

    [Fact]
    public void Layout_page_renders_each_adaptive_suggestion_as_a_graphic_preview_card()
    {
        WpfThemeHost.Invoke(() =>
        {
            var identity = new MonitorIdentity("MONITOR-WIDE", "DISPLAY1", "Super-Ultrawide");
            var monitor = new LiveMonitor(
                identity,
                new MonitorWorkArea(0, 0, 5120, 1440),
                96,
                96,
                true,
                119,
                34);
            var window = new MainWindow();
            window.AttachViewModel(new MainViewModel(SnapConfiguration.CreateDefault(), [monitor]));
            var root = Assert.IsType<Grid>(window.Content);
            var tabs = Assert.Single(root.Children.OfType<TabControl>());
            tabs.SelectedItem = tabs.Items.OfType<TabItem>().Single(item => Equals(item.Header, "Layouts"));
            root.Measure(new Size(1480, 900));
            root.Arrange(new Rect(0, 0, 1480, 900));
            root.UpdateLayout();

            var items = Assert.IsType<ItemsControl>(window.FindName("TemplateSuggestions"));
            var previews = VisualDescendants<LayoutTemplatePreview>(items).ToArray();

            Assert.Equal(items.Items.Count, previews.Length);
            Assert.True(previews.Length >= 4);
            Assert.All(previews, preview => Assert.NotNull(preview.Suggestion));
            Assert.All(previews, preview => Assert.Equal(5120d / 1440d, preview.Suggestion!.MonitorAspectRatio, 6));
        });
    }

    private static IEnumerable<T> VisualDescendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in VisualDescendants<T>(child))
            {
                yield return descendant;
            }
        }
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

    private static Rect BoundsRelativeTo(FrameworkElement element, UIElement ancestor)
    {
        var topLeft = element.TranslatePoint(new Point(0, 0), ancestor);
        return new Rect(topLeft, element.RenderSize);
    }
}
