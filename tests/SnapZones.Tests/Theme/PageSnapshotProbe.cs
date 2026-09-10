using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SnapZones.App.Services;
using SnapZones.App.ViewModels;
using SnapZones.App.Views;
using SnapZones.Core.Geometry;
using SnapZones.Core.Models;
using SnapZones.Core.Monitors;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.Theme;

/// <summary>
/// Ein Werkzeug, kein Prueflauf: schreibt jede Seite als PNG in ein Verzeichnis, damit sich das Ergebnis
/// ansehen laesst, ohne die Anwendung zu starten. Laeuft nur, wenn ZONEMANAGER_SNAPSHOT_DIR gesetzt ist.
/// </summary>
public sealed class PageSnapshotProbe
{
    [Fact]
    public void Write_one_image_per_page()
    {
        if (Environment.GetEnvironmentVariable("ZONEMANAGER_SNAPSHOT_DIR") is not { Length: > 0 } directory)
        {
            return;
        }

        WpfThemeHost.Invoke(() =>
        {
            Directory.CreateDirectory(directory);
            using var theme = new ThemeService();
            theme.Apply(ThemeMode.Dark);

            var identity = new MonitorIdentity("DISPLAY-A", "\\\\.\\DISPLAY1", "Dell U5226KW");
            var second = new MonitorIdentity("DISPLAY-B", "\\\\.\\DISPLAY2", "Dell U2723QE");
            var monitors = new[]
            {
                new LiveMonitor(identity, new MonitorWorkArea(0, 0, 3440, 1440), 96, 96, true),
                new LiveMonitor(second, new MonitorWorkArea(3440, 0, 1920, 1080), 96, 96, false)
            };
            var viewModel = new MainViewModel(ConfigurationSamples.TwoLayouts(), monitors)
            {
                RememberedWindowCount = 14
            };
            var window = new MainWindow { Left = -10000, Top = -10000, Width = 1480, Height = 900 };
            window.AttachViewModel(viewModel);
            window.Show();
            try
            {
                var tabs = Assert.Single(Assert.IsType<Grid>(window.Content).Children.OfType<TabControl>());
                var index = 1;
                foreach (var page in tabs.Items.OfType<TabItem>())
                {
                    tabs.SelectedItem = page;
                    window.UpdateLayout();
                    window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Loaded);
                    window.UpdateLayout();
                    Save(window, Path.Combine(directory, $"{index:00}-{Sanitize(page.Header?.ToString())}.png"));
                    index++;
                }
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static void Save(FrameworkElement element, string path)
    {
        var bitmap = new RenderTargetBitmap(
            (int)Math.Ceiling(element.ActualWidth),
            (int)Math.Ceiling(element.ActualHeight),
            96,
            96,
            PixelFormats.Pbgra32);
        bitmap.Render(element);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static string Sanitize(string? name) =>
        string.Concat((name ?? "seite").Split(Path.GetInvalidFileNameChars())).Replace(' ', '-');
}
