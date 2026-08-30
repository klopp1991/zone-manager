using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using SnapZones.Core.Drag;
using SnapZones.Core.Geometry;
using SnapZones.Core.Models;
using SnapZones.Windows.Windows;

namespace SnapZones.App.Overlays;

public partial class MonitorOverlayWindow : Window
{
    private DragMonitorTarget? target;
    private LayoutMetrics metrics = LayoutMetrics.Default;
    private string accent = "#2F6FED";
    private double overlayOpacity = 0.24;
    private Guid? highlightedZoneId;

    public MonitorOverlayWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) => OverlayWindowNative.Configure(new WindowInteropHelper(this).Handle);
    }

    public void ShowFor(
        DragMonitorTarget newTarget,
        LayoutMetrics newMetrics,
        string colour,
        double opacity)
    {
        target = newTarget;
        metrics = newMetrics;
        accent = colour;
        overlayOpacity = opacity;
        highlightedZoneId = null;

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

    public void Highlight(Guid? zoneId)
    {
        if (highlightedZoneId == zoneId)
        {
            return;
        }

        highlightedZoneId = zoneId;
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
            var active = zone.Id == highlightedZoneId;
            var border = new Border
            {
                Width = pixels.Width * scaleX,
                Height = pixels.Height * scaleY,
                Background = new SolidColorBrush(colour) { Opacity = active ? Math.Min(0.72, overlayOpacity * 2.4) : overlayOpacity },
                BorderBrush = new SolidColorBrush(colour) { Opacity = active ? 1 : 0.68 },
                BorderThickness = new Thickness(active ? 3 : 1.5),
                CornerRadius = new CornerRadius(6),
                Child = new TextBlock
                {
                    Text = zone.Name,
                    Foreground = System.Windows.Media.Brushes.White,
                    FontFamily = new System.Windows.Media.FontFamily("Segoe UI Variable Text"),
                    FontSize = 14,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(14, 10, 14, 10),
                    Effect = new System.Windows.Media.Effects.DropShadowEffect
                    {
                        BlurRadius = 4,
                        ShadowDepth = 1,
                        Opacity = 0.7
                    }
                }
            };
            Canvas.SetLeft(border, (pixels.X - target.Monitor.WorkArea.X) * scaleX);
            Canvas.SetTop(border, (pixels.Y - target.Monitor.WorkArea.Y) * scaleY);
            ZonesCanvas.Children.Add(border);
        }
    }
}
