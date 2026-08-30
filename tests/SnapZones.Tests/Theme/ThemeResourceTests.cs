using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SnapZones.App.Services;
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
