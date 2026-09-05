using System.Globalization;
using System.Windows;
using System.Windows.Media;
using SnapZones.Core.Models;
using MediaColor = System.Windows.Media.Color;

namespace SnapZones.App.Controls;

/// <summary>
/// Ein Monitor in klein: Rahmen, Flaeche und die Zonen eines Layouts, ohne Bedienung. Dient der Uebersicht,
/// der Monitorseite und der Vorschau der Darstellung. Im Overlay-Modus zeichnet es die Zonen so, wie das
/// Overlay sie beim Ziehen zeigt: mit der eingestellten Farbe, Deckkraft, Rahmenbreite und Beschriftung.
/// </summary>
public sealed class ZonePreview : FrameworkElement
{
    public static readonly DependencyProperty ZonesProperty = DependencyProperty.Register(
        nameof(Zones), typeof(IReadOnlyList<ZoneDefinition>), typeof(ZonePreview),
        new FrameworkPropertyMetadata(Array.Empty<ZoneDefinition>(), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty AspectRatioProperty = DependencyProperty.Register(
        nameof(AspectRatio), typeof(double), typeof(ZonePreview),
        new FrameworkPropertyMetadata(16d / 9d, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty HighlightedZoneIdProperty = DependencyProperty.Register(
        nameof(HighlightedZoneId), typeof(Guid?), typeof(ZonePreview),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MainZoneIdProperty = DependencyProperty.Register(
        nameof(MainZoneId), typeof(Guid?), typeof(ZonePreview),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShowLabelsProperty = DependencyProperty.Register(
        nameof(ShowLabels), typeof(bool), typeof(ZonePreview),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LabelFontSizeProperty = DependencyProperty.Register(
        nameof(LabelFontSize), typeof(double), typeof(ZonePreview),
        new FrameworkPropertyMetadata(12d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FrameRadiusProperty = DependencyProperty.Register(
        nameof(FrameRadius), typeof(double), typeof(ZonePreview),
        new FrameworkPropertyMetadata(8d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty FramePaddingProperty = DependencyProperty.Register(
        nameof(FramePadding), typeof(double), typeof(ZonePreview),
        new FrameworkPropertyMetadata(5d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty OverlayModeProperty = DependencyProperty.Register(
        nameof(OverlayMode), typeof(bool), typeof(ZonePreview),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty OverlayColorProperty = DependencyProperty.Register(
        nameof(OverlayColor), typeof(string), typeof(ZonePreview),
        new FrameworkPropertyMetadata("#707070", FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty OverlayOpacityPercentProperty = DependencyProperty.Register(
        nameof(OverlayOpacityPercent), typeof(double), typeof(ZonePreview),
        new FrameworkPropertyMetadata(24d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HighlightColorProperty = DependencyProperty.Register(
        nameof(HighlightColor), typeof(string), typeof(ZonePreview),
        new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HighlightOpacityPercentProperty = DependencyProperty.Register(
        nameof(HighlightOpacityPercent), typeof(double), typeof(ZonePreview),
        new FrameworkPropertyMetadata(36d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty OverlayBorderThicknessProperty = DependencyProperty.Register(
        nameof(OverlayBorderThickness), typeof(int), typeof(ZonePreview),
        new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty OverlayCornerRadiusProperty = DependencyProperty.Register(
        nameof(OverlayCornerRadius), typeof(int), typeof(ZonePreview),
        new FrameworkPropertyMetadata(4, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LabelStyleProperty = DependencyProperty.Register(
        nameof(LabelStyle), typeof(OverlayLabelStyle), typeof(ZonePreview),
        new FrameworkPropertyMetadata(OverlayLabelStyle.NumberAndName, FrameworkPropertyMetadataOptions.AffectsRender));

    public IReadOnlyList<ZoneDefinition> Zones
    {
        get => (IReadOnlyList<ZoneDefinition>)GetValue(ZonesProperty);
        set => SetValue(ZonesProperty, value);
    }

    public double AspectRatio
    {
        get => (double)GetValue(AspectRatioProperty);
        set => SetValue(AspectRatioProperty, value);
    }

    public Guid? HighlightedZoneId
    {
        get => (Guid?)GetValue(HighlightedZoneIdProperty);
        set => SetValue(HighlightedZoneIdProperty, value);
    }

    public Guid? MainZoneId
    {
        get => (Guid?)GetValue(MainZoneIdProperty);
        set => SetValue(MainZoneIdProperty, value);
    }

    public bool ShowLabels
    {
        get => (bool)GetValue(ShowLabelsProperty);
        set => SetValue(ShowLabelsProperty, value);
    }

    public double LabelFontSize
    {
        get => (double)GetValue(LabelFontSizeProperty);
        set => SetValue(LabelFontSizeProperty, value);
    }

    public double FrameRadius
    {
        get => (double)GetValue(FrameRadiusProperty);
        set => SetValue(FrameRadiusProperty, value);
    }

    public double FramePadding
    {
        get => (double)GetValue(FramePaddingProperty);
        set => SetValue(FramePaddingProperty, value);
    }

    public bool OverlayMode
    {
        get => (bool)GetValue(OverlayModeProperty);
        set => SetValue(OverlayModeProperty, value);
    }

    public string OverlayColor
    {
        get => (string)GetValue(OverlayColorProperty);
        set => SetValue(OverlayColorProperty, value);
    }

    public double OverlayOpacityPercent
    {
        get => (double)GetValue(OverlayOpacityPercentProperty);
        set => SetValue(OverlayOpacityPercentProperty, value);
    }

    public string HighlightColor
    {
        get => (string)GetValue(HighlightColorProperty);
        set => SetValue(HighlightColorProperty, value);
    }

    public double HighlightOpacityPercent
    {
        get => (double)GetValue(HighlightOpacityPercentProperty);
        set => SetValue(HighlightOpacityPercentProperty, value);
    }

    public int OverlayBorderThickness
    {
        get => (int)GetValue(OverlayBorderThicknessProperty);
        set => SetValue(OverlayBorderThicknessProperty, value);
    }

    public int OverlayCornerRadius
    {
        get => (int)GetValue(OverlayCornerRadiusProperty);
        set => SetValue(OverlayCornerRadiusProperty, value);
    }

    public OverlayLabelStyle LabelStyle
    {
        get => (OverlayLabelStyle)GetValue(LabelStyleProperty);
        set => SetValue(LabelStyleProperty, value);
    }

    /// <summary>Die Flaeche des Monitors (ohne Rahmen) in Steuerelementkoordinaten.</summary>
    public Rect ScreenRectangle
    {
        get
        {
            var monitor = MonitorRectangle();
            var padding = FramePadding;
            return new Rect(
                monitor.X + padding,
                monitor.Y + padding,
                Math.Max(1, monitor.Width - 2 * padding),
                Math.Max(1, monitor.Height - 2 * padding));
        }
    }

    protected override System.Windows.Size MeasureOverride(System.Windows.Size availableSize)
    {
        // Die Breite gibt der Platz vor; die Hoehe folgt dem Seitenverhaeltnis des Monitors.
        var ratio = AspectRatio > 0 ? AspectRatio : 16d / 9d;
        if (double.IsInfinity(availableSize.Width) && double.IsInfinity(availableSize.Height))
        {
            return new System.Windows.Size(320, 320 / ratio);
        }

        if (double.IsInfinity(availableSize.Height))
        {
            return new System.Windows.Size(availableSize.Width, availableSize.Width / ratio);
        }

        if (double.IsInfinity(availableSize.Width))
        {
            return new System.Windows.Size(availableSize.Height * ratio, availableSize.Height);
        }

        var width = Math.Min(availableSize.Width, availableSize.Height * ratio);
        return new System.Windows.Size(width, width / ratio);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        var monitor = MonitorRectangle();
        var frameRadius = FrameRadius;
        drawingContext.DrawRoundedRectangle(
            ResourceBrush("MonitorFrameBrush", MediaColor.FromRgb(23, 32, 51)), null, monitor, frameRadius, frameRadius);
        var screen = ScreenRectangle;
        var screenBrush = OverlayMode
            ? ResourceBrush("SurfaceRaisedBrush", MediaColor.FromRgb(51, 51, 51))
            : ResourceBrush("MonitorScreenBrush", MediaColor.FromRgb(244, 247, 251));
        drawingContext.DrawRoundedRectangle(screenBrush, null, screen, Math.Max(0, frameRadius - 3), Math.Max(0, frameRadius - 3));

        var number = 0;
        foreach (var zone in Zones)
        {
            number++;
            var rectangle = new Rect(
                screen.X + zone.Bounds.X * screen.Width,
                screen.Y + zone.Bounds.Y * screen.Height,
                zone.Bounds.Width * screen.Width,
                zone.Bounds.Height * screen.Height);
            rectangle.Inflate(-1.5, -1.5);
            if (rectangle.Width <= 0 || rectangle.Height <= 0)
            {
                continue;
            }

            if (OverlayMode)
            {
                DrawOverlayZone(drawingContext, rectangle, zone, number);
            }
            else
            {
                DrawEditorZone(drawingContext, rectangle, zone, number);
            }
        }
    }

    private void DrawEditorZone(DrawingContext context, Rect rectangle, ZoneDefinition zone, int number)
    {
        var highlighted = zone.Id == HighlightedZoneId;
        var fill = ResourceBrush("ZoneFillBrush", MediaColor.FromRgb(112, 112, 112)).Color;
        var border = highlighted
            ? ResourceBrush("AccentBrush", MediaColor.FromRgb(47, 111, 237)).Color
            : ResourceBrush("ZoneBorderBrush", MediaColor.FromRgb(160, 160, 160)).Color;
        context.DrawRoundedRectangle(
            new SolidColorBrush(fill) { Opacity = highlighted ? 0.32 : 0.18 },
            new System.Windows.Media.Pen(new SolidColorBrush(border), highlighted ? 2.5 : 1.5),
            rectangle, 4, 4);
        if (!ShowLabels)
        {
            return;
        }

        var label = Text($"{number} · {zone.Name}", LabelFontSize, ResourceBrush("InkBrush", MediaColor.FromRgb(23, 32, 51)), rectangle.Width - 12);
        if (label.Height + 8 <= rectangle.Height)
        {
            context.DrawText(label, new System.Windows.Point(rectangle.X + 6, rectangle.Y + 4));
        }
    }

    private void DrawOverlayZone(DrawingContext context, Rect rectangle, ZoneDefinition zone, int number)
    {
        var highlighted = zone.Id == HighlightedZoneId;
        var colour = ParseColour(OverlayColor) ?? MediaColor.FromRgb(112, 112, 112);
        var highlight = ParseColour(HighlightColor) ?? colour;
        var active = highlighted ? highlight : colour;
        var opacity = (highlighted ? HighlightOpacityPercent : OverlayOpacityPercent) / 100d;
        var thickness = Math.Max(1, highlighted ? OverlayBorderThickness + 1 : OverlayBorderThickness) * 0.75;
        var radius = OverlayCornerRadius * 0.4;
        context.DrawRoundedRectangle(
            new SolidColorBrush(active) { Opacity = Math.Clamp(opacity, 0.05, 0.95) },
            new System.Windows.Media.Pen(new SolidColorBrush(active) { Opacity = highlighted ? 0.95 : 0.62 }, thickness),
            rectangle, radius, radius);
        if (!ShowLabels)
        {
            return;
        }

        var style = OverlayStyle.Default with { LabelStyle = LabelStyle };
        var label = Text(style.Label(number, zone.Name), LabelFontSize, System.Windows.Media.Brushes.White, rectangle.Width - 14);
        var pill = new Rect(rectangle.X + 4, rectangle.Y + 4, label.Width + 10, label.Height + 4);
        if (pill.Right > rectangle.Right - 2 || pill.Bottom > rectangle.Bottom - 2)
        {
            return;
        }

        context.DrawRoundedRectangle(new SolidColorBrush(MediaColor.FromArgb(210, 32, 32, 32)), null, pill, 2, 2);
        context.DrawText(label, new System.Windows.Point(pill.X + 5, pill.Y + 2));
    }

    private FormattedText Text(string value, double size, System.Windows.Media.Brush brush, double maxWidth) => new(
        value,
        CultureInfo.CurrentUICulture,
        System.Windows.FlowDirection.LeftToRight,
        new Typeface("Segoe UI Variable Text"),
        size,
        brush,
        VisualTreeHelper.GetDpi(this).PixelsPerDip)
    {
        MaxTextWidth = Math.Max(1, maxWidth),
        Trimming = TextTrimming.CharacterEllipsis,
        MaxLineCount = 1
    };

    private Rect MonitorRectangle()
    {
        var ratio = AspectRatio > 0 ? AspectRatio : 16d / 9d;
        var width = Math.Min(Math.Max(1, ActualWidth), Math.Max(1, ActualHeight) * ratio);
        var height = width / ratio;
        return new Rect((ActualWidth - width) / 2, (ActualHeight - height) / 2, width, height);
    }

    private static MediaColor? ParseColour(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        try
        {
            return (MediaColor)System.Windows.Media.ColorConverter.ConvertFromString(value);
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private SolidColorBrush ResourceBrush(string key, MediaColor fallback) =>
        TryFindResource(key) as SolidColorBrush ?? new SolidColorBrush(fallback);
}
