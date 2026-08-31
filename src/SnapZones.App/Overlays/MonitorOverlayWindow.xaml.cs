using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using SnapZones.Core.Drag;
using SnapZones.Core.Geometry;
using SnapZones.Core.Models;
using SnapZones.Core.Settings;
using SnapZones.Windows.Windows;

namespace SnapZones.App.Overlays;

public partial class MonitorOverlayWindow : Window
{
    private const double VisualInset = SettingsCatalog.OverlayMinimumVisualGap;
    private DragMonitorTarget? target;
    private LayoutMetrics metrics = LayoutMetrics.Default;
    private string accent = "#2F6FED";
    private double overlayOpacity = 0.24;
    private bool showZoneNames = true;
    private IReadOnlyList<Guid> highlightedZoneIds = [];

    public MonitorOverlayWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => OverlayWindowNative.Configure(new WindowInteropHelper(this).Handle);
    }

    public void ShowFor(
        DragMonitorTarget newTarget,
        LayoutMetrics newMetrics,
        string colour,
        double opacity,
        bool displayZoneNames)
    {
        target = newTarget;
        metrics = newMetrics;
        accent = colour;
        overlayOpacity = opacity;
        showZoneNames = displayZoneNames;
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

    public void Highlight(IReadOnlyList<Guid> zoneIds)
    {
        ArgumentNullException.ThrowIfNull(zoneIds);
        if (highlightedZoneIds.SequenceEqual(zoneIds))
        {
            return;
        }

        highlightedZoneIds = zoneIds;
        RenderZones();
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
        foreach (var zone in target.Zones)
        {
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
                Background = new SolidColorBrush(colour)
                {
                    Opacity = active ? Math.Min(0.55, overlayOpacity + 0.12) : overlayOpacity
                },
                BorderBrush = new SolidColorBrush(colour) { Opacity = active ? 0.95 : 0.62 },
                BorderThickness = new Thickness(active ? 2 : 1),
                CornerRadius = new CornerRadius(4),
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
                        Text = zone.Name,
                        Foreground = System.Windows.Media.Brushes.White,
                        FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Text"),
                        FontSize = 13,
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
