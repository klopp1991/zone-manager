using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SnapZones.App.Services;
using SnapZones.App.Controls;
using SnapZones.App.ViewModels;
using SnapZones.App.Views;
using SnapZones.Core.Models;
using Xunit;

namespace SnapZones.Tests.Theme;

public sealed class ThemeResourceTests
{
    [Fact]
    public void Theme_defines_an_explicit_text_colour_for_unstyled_headings()
    {
        WpfThemeHost.Invoke(() =>
        {
            var style = Assert.IsType<Style>(Application.Current.Resources[typeof(TextBlock)]);
            var foreground = style.Setters.OfType<Setter>()
                .SingleOrDefault(setter => setter.Property == TextBlock.ForegroundProperty);

            Assert.NotNull(foreground);
        });
    }

    [Fact]
    public void Theme_replaces_native_templates_that_keep_light_system_colours()
    {
        WpfThemeHost.Invoke(() =>
        {
            var themedControls = new[]
            {
                typeof(Button),
                typeof(TextBox),
                typeof(ComboBox),
                typeof(ComboBoxItem),
                typeof(CheckBox),
                typeof(ListBoxItem),
                typeof(TabControl),
                typeof(TabItem),
                typeof(ScrollBar),
                typeof(Slider)
            };

            foreach (var controlType in themedControls)
            {
                var style = Assert.IsType<Style>(Application.Current.Resources[controlType]);
                var template = style.Setters.OfType<Setter>()
                    .SingleOrDefault(setter => setter.Property == Control.TemplateProperty);

                Assert.True(template?.Value is ControlTemplate, $"Für {controlType.Name} fehlt ein eigenes ControlTemplate.");
            }
        });
    }

    [Fact]
    public void Dark_theme_uses_a_readable_foreground_on_primary_actions()
    {
        WpfThemeHost.Invoke(() =>
        {
            using var service = new ThemeService();
            service.Apply(ThemeMode.Dark);

            var background = Assert.IsType<SolidColorBrush>(Application.Current.Resources["AccentBrush"]).Color;
            var foreground = Assert.IsType<SolidColorBrush>(Application.Current.Resources["AccentInkBrush"]).Color;

            Assert.True(ContrastRatio(background, foreground) >= 4.5d);
        });
    }

    [Fact]
    public void Dark_theme_uses_neutral_windows_greys_for_large_surfaces()
    {
        WpfThemeHost.Invoke(() =>
        {
            using var service = new ThemeService();
            service.Apply(ThemeMode.Dark);

            Assert.Equal(Color.FromRgb(0x20, 0x20, 0x20), ResourceColour("CanvasBrush"));
            Assert.Equal(Color.FromRgb(0x2B, 0x2B, 0x2B), ResourceColour("SurfaceBrush"));
            Assert.Equal(Color.FromRgb(0x33, 0x33, 0x33), ResourceColour("SurfaceRaisedBrush"));
        });
    }

    [Theory]
    [InlineData("AccentBrush")]
    [InlineData("AccentHoverBrush")]
    [InlineData("AccentPressedBrush")]
    [InlineData("AccentStatusBrush")]
    [InlineData("AccentSoftBrush")]
    public void Dark_interaction_colours_have_no_blue_tint(string resourceKey)
    {
        WpfThemeHost.Invoke(() =>
        {
            using var service = new ThemeService();
            service.Apply(ThemeMode.Dark);

            var colour = ResourceColour(resourceKey);

            Assert.Equal(colour.R, colour.G);
            Assert.Equal(colour.G, colour.B);
        });
    }

    [Fact]
    public void Primary_actions_keep_readable_colours_during_hover_and_press()
    {
        WpfThemeHost.Invoke(() =>
        {
            var style = Assert.IsType<Style>(Application.Current.Resources["PrimaryButton"]);
            var template = style.Setters.OfType<Setter>()
                .SingleOrDefault(setter => setter.Property == Control.TemplateProperty);
            Assert.True(template?.Value is ControlTemplate);

            using var service = new ThemeService();
            foreach (var mode in new[] { ThemeMode.Light, ThemeMode.Dark })
            {
                service.Apply(mode);
                var foreground = Assert.IsType<SolidColorBrush>(Application.Current.Resources["AccentInkBrush"]).Color;
                var hover = Assert.IsType<SolidColorBrush>(Application.Current.Resources["AccentHoverBrush"]).Color;
                var pressed = Assert.IsType<SolidColorBrush>(Application.Current.Resources["AccentPressedBrush"]).Color;

                Assert.True(ContrastRatio(hover, foreground) >= 4.5d);
                Assert.True(ContrastRatio(pressed, foreground) >= 4.5d);
            }
        });
    }

    [Theory]
    [InlineData(ThemeMode.Light)]
    [InlineData(ThemeMode.Dark)]
    public void Semantic_theme_colours_meet_text_and_control_contrast(ThemeMode mode)
    {
        WpfThemeHost.Invoke(() =>
        {
            using var service = new ThemeService();
            service.Apply(mode);

            var textPairs = new[]
            {
                ("InkBrush", "CanvasBrush"),
                ("InkBrush", "SurfaceBrush"),
                ("MutedBrush", "CanvasBrush"),
                ("MutedBrush", "SurfaceBrush"),
                ("AccentInkBrush", "AccentBrush"),
                ("AccentStatusBrush", "AccentSoftBrush"),
                ("WarningBrush", "WarningSoftBrush"),
                ("DangerBrush", "SurfaceBrush"),
                ("DisabledInkBrush", "DisabledSurfaceBrush")
            };
            foreach (var pair in textPairs)
            {
                AssertContrast(pair.Item1, pair.Item2, 4.5d);
            }

            AssertContrast("ControlBorderBrush", "SurfaceRaisedBrush", 3d);
            AssertContrast("DisabledCheckBrush", "DisabledSurfaceBrush", 3d);
        });
    }

    [Fact]
    public void Disabled_checked_box_uses_a_visible_checkmark()
    {
        WpfThemeHost.Invoke(() =>
        {
            using var service = new ThemeService();
            service.Apply(ThemeMode.Dark);
            var checkBox = new CheckBox { IsChecked = true, IsEnabled = false, Content = "Test" };
            checkBox.Measure(new Size(180, 40));
            checkBox.Arrange(new Rect(0, 0, 180, 40));
            checkBox.ApplyTemplate();

            var checkMark = VisualDescendants<System.Windows.Shapes.Path>(checkBox).Single();
            Assert.Equal(Application.Current.Resources["DisabledCheckBrush"], checkMark.Stroke);
        });
    }

    [Fact]
    public void Keyboard_focused_slider_uses_the_themed_accent_indicator()
    {
        WpfThemeHost.Invoke(() =>
        {
            using var service = new ThemeService();
            service.Apply(ThemeMode.Dark);
            var slider = new Slider { Width = 220, Minimum = 0, Maximum = 100, Value = 50 };
            var window = new Window
            {
                Content = slider,
                Width = 260,
                Height = 90,
                Left = -10000,
                Top = -10000,
                ShowInTaskbar = false
            };
            window.Show();
            try
            {
                Assert.True(slider.Focus());
                window.UpdateLayout();
                var focusBorder = Assert.IsType<Border>(slider.Template.FindName("FocusBorder", slider));

                Assert.Equal(Visibility.Visible, focusBorder.Visibility);
                Assert.Equal(Application.Current.Resources["AccentBrush"], focusBorder.BorderBrush);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Main_window_root_uses_the_themed_canvas_instead_of_transparency()
    {
        WpfThemeHost.Invoke(() =>
        {
            var window = new MainWindow();
            var root = Assert.IsType<Grid>(window.Content);

            Assert.Equal(Application.Current.Resources["CanvasBrush"], root.Background);
        });
    }

    [Fact]
    public void Main_window_header_icon_has_enough_pixels_for_maximum_supported_scaling()
    {
        WpfThemeHost.Invoke(() =>
        {
            var window = new MainWindow();
            var headerIcon = Assert.Single(LogicalDescendants<Image>(window));
            var bitmap = Assert.IsAssignableFrom<BitmapSource>(headerIcon.Source);

            Assert.True(bitmap.PixelWidth >= 180,
                $"Das Header-Icon hat nur {bitmap.PixelWidth} Pixel und wird bei hoher Skalierung vergroessert.");
        });
    }

    [Fact]
    public void Main_window_taskbar_icon_does_not_use_the_smallest_ico_frame()
    {
        WpfThemeHost.Invoke(() =>
        {
            var window = new MainWindow();
            var bitmap = Assert.IsAssignableFrom<BitmapSource>(window.Icon);

            Assert.True(bitmap.PixelWidth >= 64,
                $"Das Fenster-Icon hat nur {bitmap.PixelWidth} Pixel und wird in der Taskleiste vergroessert.");
        });
    }

    [Fact]
    public void Brand_icon_uses_only_neutral_greys()
    {
        WpfThemeHost.Invoke(() =>
        {
            var window = new MainWindow();
            var headerIcon = Assert.Single(LogicalDescendants<Image>(window));
            var bitmap = Assert.IsAssignableFrom<BitmapSource>(headerIcon.Source);
            var stride = bitmap.PixelWidth * 4;
            var pixels = new byte[stride * bitmap.PixelHeight];
            bitmap.CopyPixels(pixels, stride, 0);

            for (var index = 0; index < pixels.Length; index += 4)
            {
                if (pixels[index + 3] == 0)
                {
                    continue;
                }

                Assert.InRange(Math.Abs(pixels[index] - pixels[index + 1]), 0, 2);
                Assert.InRange(Math.Abs(pixels[index + 1] - pixels[index + 2]), 0, 2);
            }
        });
    }

    [Fact]
    public void Brand_icon_uses_two_wide_lower_tiles_instead_of_a_monitor_stand()
    {
        WpfThemeHost.Invoke(() =>
        {
            var window = new MainWindow();
            var headerIcon = Assert.Single(LogicalDescendants<Image>(window));
            var bitmap = Assert.IsAssignableFrom<BitmapSource>(headerIcon.Source);

            Assert.True(PixelBrightness(bitmap, 70, 200) >= 145);
            Assert.True(PixelBrightness(bitmap, 180, 200) >= 145);
        });
    }

    [Fact]
    public void Main_window_uses_a_windows_style_left_navigation()
    {
        WpfThemeHost.Invoke(() =>
        {
            var window = new MainWindow();
            var root = Assert.IsType<Grid>(window.Content);
            var tabs = Assert.Single(root.Children.OfType<TabControl>());

            Assert.Equal(Dock.Left, tabs.TabStripPlacement);
        });
    }

    [Fact]
    public void Window_placement_page_is_embedded_themed_and_keeps_technical_values_copyable()
    {
        WpfThemeHost.Invoke(() =>
        {
            var window = new MainWindow();
            window.AttachViewModel(new MainViewModel(SnapConfiguration.CreateDefault(), []));
            window.Left = -10000;
            window.ShowInTaskbar = false;
            window.Show();
            try
            {
                var root = Assert.IsType<Grid>(window.Content);
            var tabs = Assert.Single(root.Children.OfType<TabControl>());
            var placementTab = tabs.Items.OfType<TabItem>()
                .Single(item => Equals(item.Header, "Fensterplatzierung"));
            tabs.SelectedItem = placementTab;

            var automatic = Assert.IsType<CheckBox>(window.FindName("PlacementAutomaticCheckBox"));
            var select = Assert.IsType<Button>(window.FindName("PlacementSelectButton"));
            var apply = Assert.IsType<Button>(window.FindName("PlacementApplyButton"));
            var remember = Assert.IsType<Button>(window.FindName("PlacementRememberButton"));
            var exclude = Assert.IsType<Button>(window.FindName("PlacementExcludeButton"));
            var forget = Assert.IsType<Button>(window.FindName("PlacementForgetButton"));
            var fix = Assert.IsType<Button>(window.FindName("PlacementFixButton"));
            var applicationKey = Assert.IsType<TextBox>(window.FindName("PlacementApplicationKeyText"));
            var windowClass = Assert.IsType<TextBox>(window.FindName("PlacementWindowClassText"));
            var status = Assert.IsType<TextBlock>(window.FindName("PlacementRuleStatusText"));
            var editorStatus = Assert.IsType<TextBlock>(window.FindName("PlacementRuleEditorStatusText"));
            var operationStatus = Assert.IsType<TextBox>(window.FindName("PlacementOperationStatusText"));
            var placementScroller = Assert.IsType<ScrollViewer>(window.FindName("PlacementScroller"));
            foreach (var button in new[] { apply, remember, exclude, forget, fix })
            {
                button.GetBindingExpression(UIElement.IsEnabledProperty)?.UpdateTarget();
            }

            Assert.NotNull(automatic.GetBindingExpression(ToggleButton.IsCheckedProperty));
            Assert.True(select.IsEnabled);
            Assert.All(new[] { apply, remember, exclude, forget, fix }, button => Assert.False(button.IsEnabled));
            Assert.True(applicationKey.IsReadOnly);
            Assert.True(windowClass.IsReadOnly);
            Assert.NotNull(status.GetBindingExpression(TextBlock.TextProperty));
            Assert.NotNull(editorStatus.GetBindingExpression(TextBlock.TextProperty));
            foreach (var button in new[] { remember, exclude, fix })
            {
                Assert.Equal(
                    "WindowPlacement.CanSaveRule",
                    button.GetBindingExpression(UIElement.IsEnabledProperty)!.ParentBinding.Path.Path);
            }
            foreach (var button in new[] { apply, forget })
            {
                Assert.Equal(
                    "WindowPlacement.HasLearnedSelection",
                    button.GetBindingExpression(UIElement.IsEnabledProperty)!.ParentBinding.Path.Path);
            }
            Assert.True(operationStatus.IsReadOnly);
            Assert.Equal(BindingMode.OneWay,
                operationStatus.GetBindingExpression(TextBox.TextProperty)!.ParentBinding.Mode);
            Assert.Equal(ScrollBarVisibility.Auto, placementScroller.HorizontalScrollBarVisibility);
            window.Width = window.MinWidth;
            window.Height = window.MinHeight;
            window.UpdateLayout();
            Assert.True(placementScroller.ViewportWidth > 0);
            Assert.True(placementScroller.ScrollableWidth > 0);
            Assert.Equal(Visibility.Visible, placementScroller.ComputedHorizontalScrollBarVisibility);
            Assert.Equal(Application.Current.Resources["SurfaceBrush"],
                Assert.IsType<Border>(window.FindName("PlacementLearnedCard")).Background);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Main_window_hides_explanations_behind_accessible_info_buttons()
    {
        WpfThemeHost.Invoke(() =>
        {
            var window = new MainWindow();
            var requiredHelpButtons = new[]
            {
                "ProfileInfoButton",
                "ZonePositionInfoButton",
                "ZoneMarginsInfoButton",
                "ThemeInfoButton",
                "OverlayScopeInfoButton",
                "TriggerModeInfoButton",
                "OuterMarginsInfoButton",
                "ZoneGapInfoButton",
                "MagnetDistanceInfoButton",
                "OverlayColourInfoButton",
                "OverlayOpacityInfoButton",
                "OverlayVisualMarginInfoButton"
            };

            foreach (var name in requiredHelpButtons)
            {
                var button = Assert.IsType<Button>(window.FindName(name));
                var helpText = Assert.IsType<string>(button.ToolTip);

                Assert.True(button.Focusable, $"{name} ist nicht per Tastatur erreichbar.");
                Assert.True(helpText.Length >= 30, $"{name} enthält keinen ausreichenden Hilfetext.");
                Assert.False(string.IsNullOrWhiteSpace(System.Windows.Automation.AutomationProperties.GetName(button)));
            }

            var helpStyle = Assert.IsType<Style>(Application.Current.Resources["HelpText"]);
            Assert.DoesNotContain(
                LogicalDescendants<TextBlock>(window),
                textBlock => ReferenceEquals(textBlock.Style, helpStyle));
        });
    }

    [Fact]
    public void Main_window_uses_larger_text_for_readability()
    {
        WpfThemeHost.Invoke(() =>
        {
            var window = new MainWindow();
            var profileNameText = Assert.IsType<TextBox>(window.FindName("ProfileNameText"));

            Assert.Equal(15d, window.FontSize);
            Assert.Equal(15d, profileNameText.FontSize);
            Assert.Equal(30d, StyleFontSize("PageTitle"));
            Assert.Equal(19d, StyleFontSize("SectionTitle"));
            Assert.Equal(14d, StyleFontSize("FieldLabel"));
            Assert.Equal(13.5d, StyleFontSize("HelpText"));
        });
    }

    [Fact]
    public void Info_icons_are_smaller_without_reducing_the_hit_target()
    {
        WpfThemeHost.Invoke(() =>
        {
            var window = new MainWindow();
            var button = Assert.IsType<Button>(window.FindName("ProfileInfoButton"));

            Assert.True(button.ApplyTemplate());
            var glyph = Assert.IsType<Grid>(button.Template.FindName("InfoGlyph", button));

            Assert.Equal(22d, button.Width);
            Assert.Equal(22d, button.Height);
            Assert.Equal(18d, glyph.Width);
            Assert.Equal(18d, glyph.Height);
        });
    }

    [Theory]
    [InlineData("ZoneGapPercentText", "ZoneGapPercent")]
    [InlineData("MagnetThresholdPercentText", "MagnetThresholdPercent")]
    [InlineData("OverlayOpacityPercentText", "OverlayOpacityPercent")]
    public void Settings_sliders_accept_percentage_input_from_the_keyboard(string textBoxName, string propertyName)
    {
        WpfThemeHost.Invoke(() =>
        {
            var viewModel = new MainViewModel(SnapConfiguration.CreateDefault(), []);
            var settings = viewModel.Settings;
            var window = new MainWindow { DataContext = viewModel, Left = -10000 };
            window.Show();
            try
            {
                var textBox = Assert.IsType<TextBox>(window.FindName(textBoxName));

                textBox.Text = "50.5";
                var binding = textBox.GetBindingExpression(TextBox.TextProperty);
                Assert.NotNull(binding);
                binding.UpdateSource();

                var property = typeof(SettingsViewModel).GetProperty(propertyName);
                Assert.NotNull(property);
                Assert.Equal(50.5, Assert.IsType<double>(property.GetValue(settings)));
                Assert.False(textBox.IsReadOnly);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Overlay_colour_picker_applies_the_selected_colour_and_updates_the_preview()
    {
        WpfThemeHost.Invoke(() =>
        {
            var constructor = typeof(MainWindow).GetConstructor([typeof(Func<string, string?>)]);
            Assert.NotNull(constructor);
            var window = Assert.IsType<MainWindow>(constructor.Invoke([
                new Func<string, string?>(_ => "#A1B2C3")
            ]));
            var viewModel = new MainViewModel(SnapConfiguration.CreateDefault(), []);
            var settings = viewModel.Settings;
            window.DataContext = viewModel;
            window.Left = -10000;
            window.Show();
            try
            {
                var button = Assert.IsType<Button>(window.FindName("OverlayColorPickerButton"));
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

                Assert.Equal("#A1B2C3", settings.OverlayColor);
                var preview = Assert.IsType<Border>(window.FindName("OverlayColorPreview"));
                preview.GetBindingExpression(Border.BackgroundProperty)?.UpdateTarget();
                var brush = Assert.IsType<SolidColorBrush>(preview.Background);
                Assert.Equal(Color.FromRgb(0xA1, 0xB2, 0xC3), brush.Color);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Settings_page_uses_the_available_window_width()
    {
        WpfThemeHost.Invoke(() =>
        {
            var window = new MainWindow();
            var root = Assert.IsType<Grid>(window.Content);
            var tabs = Assert.Single(root.Children.OfType<TabControl>());
            tabs.SelectedItem = tabs.Items.OfType<TabItem>().Single(item => Equals(item.Header, "Einstellungen"));
            var size = new Size(1480, 900);

            root.Measure(size);
            root.Arrange(new Rect(size));
            root.UpdateLayout();

            var content = Assert.IsAssignableFrom<FrameworkElement>(window.FindName("SettingsContent"));
            Assert.True(content.ActualWidth >= 1100,
                $"Der Einstellungsbereich nutzt nur {content.ActualWidth:0} von 1480 Pixel Fensterbreite.");
        });
    }

    [Fact]
    public void Dark_layout_editor_uses_a_neutral_zone_fill()
    {
        WpfThemeHost.Invoke(() =>
        {
            using var service = new ThemeService();
            service.Apply(ThemeMode.Dark);
            var zoneId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var canvas = new LayoutCanvas
            {
                Zones = [new ZoneDefinition(zoneId, "Voll", NormalizedRect.Full)],
                SelectedZoneId = zoneId,
                Width = 400,
                Height = 300
            };
            canvas.Measure(new Size(400, 300));
            canvas.Arrange(new Rect(0, 0, 400, 300));
            canvas.UpdateLayout();
            var bitmap = new RenderTargetBitmap(400, 300, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(canvas);
            var pixel = new byte[4];
            bitmap.CopyPixels(new Int32Rect(200, 150, 1, 1), pixel, 4, 0);

            Assert.InRange(Math.Abs(pixel[0] - pixel[1]), 0, 2);
            Assert.InRange(Math.Abs(pixel[1] - pixel[2]), 0, 2);
        });
    }

    [Theory]
    [InlineData(ThemeMode.Light, true)]
    [InlineData(ThemeMode.Dark, false)]
    public void Main_window_render_contains_no_large_surface_from_the_opposite_theme(ThemeMode mode, bool expectBrightSurface)
    {
        WpfThemeHost.Invoke(() =>
        {
            using var service = new ThemeService();
            service.Apply(mode);
            var window = new MainWindow();
            var root = Assert.IsType<Grid>(window.Content);
            var size = new Size(1180, 720);
            root.Measure(size);
            root.Arrange(new Rect(size));
            root.UpdateLayout();
            var tabs = Assert.Single(root.Children.OfType<TabControl>());
            for (var selectedIndex = 0; selectedIndex < tabs.Items.Count; selectedIndex++)
            {
                tabs.SelectedIndex = selectedIndex;
                root.UpdateLayout();
                var bitmap = new RenderTargetBitmap(1180, 720, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(root);
                var pixels = new byte[1180 * 720 * 4];
                bitmap.CopyPixels(pixels, 1180 * 4, 0);

                var opaque = 0;
                var bright = 0;
                var dark = 0;
                for (var index = 0; index < pixels.Length; index += 4)
                {
                    if (pixels[index + 3] < 250)
                    {
                        continue;
                    }

                    opaque++;
                    if (pixels[index] > 230 && pixels[index + 1] > 230 && pixels[index + 2] > 230)
                    {
                        bright++;
                    }
                    if (pixels[index] < 55 && pixels[index + 1] < 55 && pixels[index + 2] < 55)
                    {
                        dark++;
                    }
                }

                Assert.True(opaque > 1180 * 720 * 0.95d);
                var brightShare = bright / (double)opaque;
                var darkShare = dark / (double)opaque;
                Assert.Equal(expectBrightSurface, brightShare > 0.50d);
                Assert.Equal(!expectBrightSurface, darkShare > 0.50d);
                Assert.True((expectBrightSurface ? darkShare : brightShare) < 0.08d);
            }
        });
    }

    private static void AssertContrast(string foregroundKey, string backgroundKey, double minimum)
    {
        var foreground = Assert.IsType<SolidColorBrush>(Application.Current.Resources[foregroundKey]).Color;
        var background = Assert.IsType<SolidColorBrush>(Application.Current.Resources[backgroundKey]).Color;
        Assert.True(ContrastRatio(foreground, background) >= minimum,
            $"{foregroundKey} auf {backgroundKey} unterschreitet {minimum:0.0}:1.");
    }

    private static Color ResourceColour(string key) =>
        Assert.IsType<SolidColorBrush>(Application.Current.Resources[key]).Color;

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

    private static double ContrastRatio(Color first, Color second)
    {
        static double Luminance(Color colour)
        {
            static double Channel(byte value)
            {
                var normalized = value / 255d;
                return normalized <= 0.04045d
                    ? normalized / 12.92d
                    : Math.Pow((normalized + 0.055d) / 1.055d, 2.4d);
            }

            return 0.2126d * Channel(colour.R) + 0.7152d * Channel(colour.G) + 0.0722d * Channel(colour.B);
        }

        var light = Math.Max(Luminance(first), Luminance(second));
        var dark = Math.Min(Luminance(first), Luminance(second));
        return (light + 0.05d) / (dark + 0.05d);
    }

    private static double PixelBrightness(BitmapSource bitmap, int x, int y)
    {
        var pixel = new byte[4];
        bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);
        return 0.0722d * pixel[0] + 0.7152d * pixel[1] + 0.2126d * pixel[2];
    }

    private static double StyleFontSize(string resourceKey)
    {
        var style = Assert.IsType<Style>(Application.Current.Resources[resourceKey]);
        var setter = Assert.Single(style.Setters.OfType<Setter>(),
            candidate => candidate.Property == TextBlock.FontSizeProperty);
        return Assert.IsType<double>(setter.Value);
    }
}

internal static class WpfThemeHost
{
    private static readonly ManualResetEventSlim Ready = new(false);
    private static readonly Thread UiThread = StartThread();

    public static void Invoke(Action action)
    {
        Ready.Wait();
        Application.Current.Dispatcher.Invoke(action);
    }

    private static Thread StartThread()
    {
        var thread = new Thread(() =>
        {
            _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            var resources = Assert.IsType<ResourceDictionary>(Application.LoadComponent(
                new Uri("/SaschaWindowZones;component/Themes/Theme.xaml", UriKind.Relative)));
            Application.Current.Resources.MergedDictionaries.Add(resources);
            Ready.Set();
            Dispatcher.Run();
        })
        {
            IsBackground = true,
            Name = "WPF-Theme-Tests"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return thread;
    }
}
