using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using SnapZones.App.Controls;
using SnapZones.App.ViewModels;
using SnapZones.App.Views;
using SnapZones.Core.Geometry;
using SnapZones.Core.Models;
using SnapZones.Core.Monitors;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.Theme;

/// <summary>Die Seite «Zonen &amp; Layouts»: Monitorauswahl, ein Tab je Layout, Vorlagen im Menue.</summary>
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
                UiTree.VisualDescendants<TextBlock>(root),
                textBlock => AutomationProperties.GetName(textBlock) == "Layoutübersicht");
        });
    }

    [Fact]
    public void Layout_page_shows_one_tab_per_layout_and_marks_the_active_one()
    {
        WpfThemeHost.Invoke(() =>
        {
            var monitor = new LiveMonitor(new MonitorIdentity("DISPLAY-A", "DISPLAY1", "Monitor A"), new MonitorWorkArea(0, 0, 3440, 1440), 96, 96, true);
            var viewModel = new MainViewModel(ConfigurationSamples.TwoLayouts(), [monitor]);
            var window = new MainWindow { Left = -10000 };
            window.AttachViewModel(viewModel);
            var tabs = Assert.Single(Assert.IsType<Grid>(window.Content).Children.OfType<TabControl>());
            tabs.SelectedItem = tabs.Items.OfType<TabItem>().Single(item => Equals(item.Header, "Zonen & Layouts"));
            window.Show();
            try
            {
                window.UpdateLayout();
                var layoutTabs = Assert.IsType<ItemsControl>(window.FindName("LayoutTabs"));
                Assert.Equal(2, layoutTabs.Items.Count);
                var buttons = UiTree.VisualDescendants<Button>(layoutTabs).Where(button => button.DataContext is MonitorLayout).ToArray();
                Assert.Equal(2, buttons.Length);
                Assert.True(Chrome.GetIsCurrent(buttons[0]));
                Assert.False(Chrome.GetIsCurrent(buttons[1]));

                // Ein Klick wechselt nur das bearbeitete Layout; aktiv bleibt «Arbeit».
                buttons[1].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                window.UpdateLayout();

                Assert.Equal("Abend", viewModel.SelectedLayout?.Name);
                Assert.True(viewModel.Configuration.Layouts.Single(layout => layout.Name == "Arbeit").IsActive);
                Assert.True(Chrome.GetIsCurrent(buttons[1]));
                Assert.IsType<Button>(window.FindName("AddLayoutButton"));
                Assert.Null(window.FindName("LayoutSelector"));
                Assert.Null(window.FindName("DeleteLayoutButton"));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Layout_page_uses_a_compact_monitor_selector_and_keeps_the_editor_wide()
    {
        WpfThemeHost.Invoke(() =>
        {
            var firstMonitor = new LiveMonitor(new MonitorIdentity("MONITOR-LEFT", "DISPLAY1", "Monitor links"), new MonitorWorkArea(0, 0, 2560, 1440), 96, 96, true, 60, 34);
            var secondMonitor = new LiveMonitor(new MonitorIdentity("MONITOR-RIGHT", "DISPLAY2", "Monitor rechts"), new MonitorWorkArea(2560, 0, 1920, 1080), 96, 96, false, 53, 30);
            var viewModel = new MainViewModel(SnapConfiguration.CreateDefault(), [firstMonitor, secondMonitor]);
            var window = new MainWindow();
            window.AttachViewModel(viewModel);
            var root = Assert.IsType<Grid>(window.Content);
            var tabs = Assert.Single(root.Children.OfType<TabControl>());
            tabs.SelectedItem = tabs.Items.OfType<TabItem>().Single(item => Equals(item.Header, "Zonen & Layouts"));
            var size = new Size(1180, 720);

            root.Measure(size);
            root.Arrange(new Rect(size));
            root.UpdateLayout();

            var selector = Assert.IsType<ComboBox>(window.FindName("LayoutMonitorSelector"));
            var editorArea = Assert.IsType<Grid>(window.FindName("LayoutEditorArea"));
            selector.SelectedIndex = 1;
            root.UpdateLayout();

            Assert.Same(viewModel.Monitors[1], viewModel.SelectedMonitor);
            Assert.Equal(220d, selector.ActualWidth, 1);
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
            var monitor = new LiveMonitor(new MonitorIdentity("MONITOR-A", "DISPLAY1", "Monitor A"), new MonitorWorkArea(0, 0, 2560, 1440), 96, 96, true);
            var window = new MainWindow();
            window.AttachViewModel(new MainViewModel(SnapConfiguration.CreateDefault(), [monitor]));
            var root = Assert.IsType<Grid>(window.Content);
            var tabs = Assert.Single(root.Children.OfType<TabControl>());
            tabs.SelectedItem = tabs.Items.OfType<TabItem>().Single(item => Equals(item.Header, "Zonen & Layouts"));

            root.Measure(new Size(width, height));
            root.Arrange(new Rect(0, 0, width, height));
            root.UpdateLayout();

            var editorArea = Assert.IsType<Grid>(window.FindName("LayoutEditorArea"));
            var editorBounds = BoundsRelativeTo(editorArea, root);
            var headerControls = new FrameworkElement[]
            {
                Assert.IsType<ComboBox>(window.FindName("LayoutMonitorSelector")),
                Assert.IsType<Button>(window.FindName("AddLayoutButton")),
                Assert.IsType<Button>(window.FindName("ToggleValuePanelButton"))
            };

            Assert.All(headerControls, control =>
            {
                var bounds = BoundsRelativeTo(control, root);
                Assert.True(bounds.Bottom + 8 <= editorBounds.Top,
                    $"{control.Name} endet bei {bounds.Bottom:0.0}, der Editor beginnt bereits bei {editorBounds.Top:0.0}.");
            });

            var footer = Assert.IsType<Button>(window.FindName("DrawOnMonitorButton"));
            var page = Assert.IsType<Grid>(tabs.Items.OfType<TabItem>().Single(item => Equals(item.Header, "Zonen & Layouts")).Content);
            Assert.True(BoundsRelativeTo(footer, root).Right <= BoundsRelativeTo(page, root).Right + 0.5,
                "Die Hauptaktion der Fusszeile ragt aus der Seite.");
        });
    }

    [Fact]
    public void Template_menu_renders_each_adaptive_suggestion_as_a_graphic_preview_card()
    {
        WpfThemeHost.Invoke(() =>
        {
            var identity = new MonitorIdentity("MONITOR-WIDE", "DISPLAY1", "Super-Ultrawide");
            var monitor = new LiveMonitor(identity, new MonitorWorkArea(0, 0, 5120, 1440), 96, 96, true, 119, 34);
            var window = new MainWindow { Left = -10000 };
            window.AttachViewModel(new MainViewModel(SnapConfiguration.CreateDefault(), [monitor]));
            var tabs = Assert.Single(Assert.IsType<Grid>(window.Content).Children.OfType<TabControl>());
            tabs.SelectedItem = tabs.Items.OfType<TabItem>().Single(item => Equals(item.Header, "Zonen & Layouts"));
            window.Show();
            try
            {
                var popup = Assert.IsType<Popup>(window.FindName("TemplatePopup"));
                Assert.IsType<Button>(window.FindName("TemplateMenuButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.True(popup.IsOpen);
                var items = Assert.IsType<ItemsControl>(window.FindName("TemplateSuggestions"));
                items.UpdateLayout();
                var previews = UiTree.VisualDescendants<LayoutTemplatePreview>(items).ToArray();

                Assert.True(items.Items.Count >= 4);
                Assert.Equal(items.Items.Count, previews.Length);
                Assert.All(previews, preview => Assert.NotNull(preview.Suggestion));
                Assert.All(previews, preview => Assert.Equal(5120d / 1440d, preview.Suggestion!.MonitorAspectRatio, 6));
                popup.IsOpen = false;
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static Rect BoundsRelativeTo(FrameworkElement element, UIElement ancestor)
    {
        var topLeft = element.TranslatePoint(new Point(0, 0), ancestor);
        return new Rect(topLeft, element.RenderSize);
    }
}
