using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using SnapZones.Core.Geometry;
using SnapZones.Core.Models;
using SnapZones.Core.PartMonitors;
using SnapZones.Windows.Windows;

namespace SnapZones.App.Overlays;

public partial class MonitorOverlayWindow : Window
{
    private const double VisualInset = 8d;
    private PartMonitorTarget? target;
    private LayoutMetrics metrics = LayoutMetrics.Default;
    private string accent = "#2F6FED";
    private double overlayOpacity = 0.24;
    private bool showZoneNames = true;
    private OverlayStyle style = OverlayStyle.Default;
    private IReadOnlyList<Guid> highlightedZoneIds = [];

    public MonitorOverlayWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => OverlayWindowNative.Configure(new WindowInteropHelper(this).Handle);
    }

    public void ShowFor(
        PartMonitorTarget newTarget,
        LayoutMetrics newMetrics,
        string colour,
        double opacity,
        bool displayZoneNames) => ShowFor(newTarget, newMetrics, colour, opacity, displayZoneNames, OverlayStyle.Default);

    public void ShowFor(
        PartMonitorTarget newTarget,
        LayoutMetrics newMetrics,
        string colour,
        double opacity,
        bool displayZoneNames,
        OverlayStyle newStyle)
    {
        target = newTarget;
        metrics = newMetrics;
        accent = colour;
        overlayOpacity = opacity;
        showZoneNames = displayZoneNames;
        style = newStyle ?? OverlayStyle.Default;
        highlightedZoneIds = [];

        if (!IsVisible)
        {
            Show();
        }

        OverlayWindowNative.Position(
            new WindowInteropHelper(this).Handle,
            new PixelRect(
                target.Monitor.WorkArea.X,
                target.Monitor.WorkArea.Y,
                target.Monitor.WorkArea.Width,
                target.Monitor.WorkArea.Height));
        RenderZones();
    }

    public void Highlight(Guid? zoneId) =>
        Highlight(zoneId is { } id ? [id] : []);

    /// <summary>
    /// Hebt genau die genannten Zonen hervor. Beim Ziehen ueber mehrere Zonen sind das alle
    /// ueberstrichenen; sonst hoechstens eine.
    /// </summary>
    public void Highlight(IReadOnlyList<Guid> zoneIds)
    {
        ArgumentNullException.ThrowIfNull(zoneIds);
        if (highlightedZoneIds.SequenceEqual(zoneIds))
        {
            return;
        }

        highlightedZoneIds = zoneIds.ToArray();
        RenderZones();
    }

    private static System.Windows.Media.Color? TryParseColour(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            return (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(value);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private void RenderZones()
    {
        ZonesCanvas.Children.Clear();
        if (target is null)
        {
            return;
        }

        var scaleX = 96d / target.Monitor.DpiX;
        var scaleY = 96d / target.Monitor.DpiY;
        var colour = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(accent);
        var highlight = TryParseColour(style.HighlightColor) ?? colour;
        var number = 0;
        foreach (var zone in target.PartMonitors)
        {
            number++;
            var pixels = ZoneGeometry.ToPixels(zone.Bounds, target.Monitor.WorkArea, metrics);
            var active = highlightedZoneIds.Contains(zone.Id);
            var rawWidth = pixels.Width * scaleX;
            var rawHeight = pixels.Height * scaleY;
            var insetX = Math.Min(VisualInset, Math.Max(0, (rawWidth - 1) / 2));
            var insetY = Math.Min(VisualInset, Math.Max(0, (rawHeight - 1) / 2));
            var border = new Border
            {
                Width = Math.Max(1, rawWidth - insetX * 2),
                Height = Math.Max(1, rawHeight - insetY * 2),
                Background = new SolidColorBrush(active ? highlight : colour)
                {
                    Opacity = active ? style.HighlightOpacity : overlayOpacity
                },
                BorderBrush = new SolidColorBrush(active ? highlight : colour) { Opacity = active ? 0.95 : 0.62 },
                BorderThickness = new Thickness(active ? style.BorderThickness + 1 : style.BorderThickness),
                CornerRadius = new CornerRadius(style.CornerRadius),
                Child = showZoneNames ? new Border
                {
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
                    VerticalAlignment = System.Windows.VerticalAlignment.Top,
                    Margin = new Thickness(8),
                    Padding = new Thickness(9, 5, 9, 5),
                    Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(210, 32, 32, 32)),
                    CornerRadius = new CornerRadius(3),
                    Child = new TextBlock
                    {
                        // Nummer wie im Editor; sie ist zugleich die Taste in Ctrl + Alt + Nummer.
                        Text = style.Label(number, zone.Name),
                        Foreground = System.Windows.Media.Brushes.White,
                        FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Text"),
                        FontSize = style.LabelFontSize,
                        FontWeight = FontWeights.Medium
                    }
                } : null
            };
            Canvas.SetLeft(border, (pixels.X - target.Monitor.WorkArea.X) * scaleX + insetX);
            Canvas.SetTop(border, (pixels.Y - target.Monitor.WorkArea.Y) * scaleY + insetY);
            ZonesCanvas.Children.Add(border);
        }
    }
}
