using System.Globalization;
using System.Windows;
using System.Windows.Media;
using SnapZones.Core.Geometry;
using SnapZones.Core.Models;
using SnapZones.Core.Settings;
using MediaColor = System.Windows.Media.Color;
using MediaPen = System.Windows.Media.Pen;
using MediaColorConverter = System.Windows.Media.ColorConverter;
using MediaSize = System.Windows.Size;
using MediaFlowDirection = System.Windows.FlowDirection;

namespace SnapZones.App.Controls;

/// <summary>
/// Live preview of the drag overlay for the settings page.
/// <para>
/// It runs the same <see cref="ZoneGeometry.ToPixels(NormalizedRect, MonitorWorkArea, LayoutMetrics)"/>
/// calculation and the same fill, border and inset rules as
/// <c>MonitorOverlayWindow</c>, so what the user sees here is what the overlay
/// will actually look like while dragging.
/// </para>
/// </summary>
public sealed class OverlayAppearancePreview : FrameworkElement
{
    /// <summary>Work area of the simulated monitor, in device pixels.</summary>
    private static readonly MonitorWorkArea SampleMonitor = new(0, 0, 1920, 1080);

    private static readonly (string Name, NormalizedRect Bounds)[] SampleZones =
    [
        ("Links", new NormalizedRect(0, 0, 0.5, 1)),
        ("Oben rechts", new NormalizedRect(0.5, 0, 0.5, 0.5)),
        ("Unten rechts", new NormalizedRect(0.5, 0.5, 0.5, 0.5))
    ];

    /// <summary>The zone the sample cursor is over, drawn in the highlighted state.</summary>
    private const int HighlightedZoneIndex = 1;

    public static readonly DependencyProperty OverlayColorProperty = DependencyProperty.Register(
        nameof(OverlayColor),
        typeof(string),
        typeof(OverlayAppearancePreview),
        new FrameworkPropertyMetadata(
            SettingsCatalog.DefaultOverlayColor,
            FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty OverlayOpacityPercentProperty = DependencyProperty.Register(
        nameof(OverlayOpacityPercent),
        typeof(double),
        typeof(OverlayAppearancePreview),
        new FrameworkPropertyMetadata(
            SettingsCatalog.OverlayOpacityRange.Default,
            FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ZoneGapProperty = DependencyProperty.Register(
        nameof(ZoneGap),
        typeof(int),
        typeof(OverlayAppearancePreview),
        new FrameworkPropertyMetadata(
            (int)SettingsCatalog.ZoneGapRange.Default,
            FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ShowZoneNamesProperty = DependencyProperty.Register(
        nameof(ShowZoneNames),
        typeof(bool),
        typeof(OverlayAppearancePreview),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>Overlay colour as a hex string. An unparsable value falls back to the default.</summary>
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

    public int ZoneGap
    {
        get => (int)GetValue(ZoneGapProperty);
        set => SetValue(ZoneGapProperty, value);
    }

    public bool ShowZoneNames
    {
        get => (bool)GetValue(ShowZoneNamesProperty);
        set => SetValue(ShowZoneNamesProperty, value);
    }

    /// <summary>
    /// Parses a <c>#RRGGBB</c> string, returning the default overlay colour
    /// while the user is midway through typing an invalid value.
    /// </summary>
    internal static MediaColor ParseColour(string? value)
    {
        var fallback = (MediaColor)MediaColorConverter.ConvertFromString(SettingsCatalog.DefaultOverlayColor)!;
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        try
        {
            return MediaColorConverter.ConvertFromString(value) is MediaColor parsed ? parsed : fallback;
        }
        catch (FormatException)
        {
            return fallback;
        }
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (ActualWidth <= 4 || ActualHeight <= 4)
        {
            return;
        }

        // Fit the 16:9 sample monitor into the control.
        var aspectRatio = (double)SampleMonitor.Width / SampleMonitor.Height;
        var frameWidth = Math.Min(ActualWidth, ActualHeight * aspectRatio);
        var frameHeight = frameWidth / aspectRatio;
        var frame = new Rect(
            (ActualWidth - frameWidth) / 2,
            (ActualHeight - frameHeight) / 2,
            frameWidth,
            frameHeight);

        var desktop = ResourceBrush("MonitorScreenBrush", MediaColor.FromRgb(244, 247, 251));
        var frameBorder = ResourceBrush("ControlBorderBrush", MediaColor.FromRgb(116, 129, 150));
        drawingContext.DrawRoundedRectangle(
            new SolidColorBrush(desktop),
            new MediaPen(new SolidColorBrush(frameBorder), 1),
            frame,
            4,
            4);

        var colour = ParseColour(OverlayColor);
        var opacity = Math.Clamp(OverlayOpacityPercent / 100d, 0, 1);
        var metrics = new LayoutMetrics(EdgeInsets.Uniform(0), Math.Max(0, ZoneGap));
        var scale = frame.Width / SampleMonitor.Width;

        for (var index = 0; index < SampleZones.Length; index++)
        {
            var (name, bounds) = SampleZones[index];
            var pixels = ZoneGeometry.ToPixels(bounds, SampleMonitor, metrics);
            var isHighlighted = index == HighlightedZoneIndex;

            var rawWidth = pixels.Width * scale;
            var rawHeight = pixels.Height * scale;

            // The overlay always keeps a small visual inset, scaled down here
            // to stay proportional inside the preview.
            var inset = SettingsCatalog.OverlayMinimumVisualGap * scale;
            var insetX = Math.Min(inset, Math.Max(0, (rawWidth - 1) / 2));
            var insetY = Math.Min(inset, Math.Max(0, (rawHeight - 1) / 2));

            var rectangle = new Rect(
                frame.X + (pixels.X - SampleMonitor.X) * scale + insetX,
                frame.Y + (pixels.Y - SampleMonitor.Y) * scale + insetY,
                Math.Max(1, rawWidth - insetX * 2),
                Math.Max(1, rawHeight - insetY * 2));

            var fill = new SolidColorBrush(colour)
            {
                Opacity = isHighlighted ? Math.Min(0.55, opacity + 0.12) : opacity
            };
            var border = new SolidColorBrush(colour) { Opacity = isHighlighted ? 0.95 : 0.62 };

            drawingContext.DrawRoundedRectangle(
                fill,
                new MediaPen(border, isHighlighted ? 2 : 1),
                rectangle,
                3,
                3);

            if (ShowZoneNames)
            {
                DrawZoneName(drawingContext, rectangle, name);
            }
        }
    }

    private void DrawZoneName(DrawingContext drawingContext, Rect zone, string name)
    {
        var text = new FormattedText(
            name,
            CultureInfo.CurrentUICulture,
            MediaFlowDirection.LeftToRight,
            new Typeface(
                new System.Windows.Media.FontFamily("Segoe UI Variable Text"),
                FontStyles.Normal,
                FontWeights.Medium,
                FontStretches.Normal),
            10.5,
            System.Windows.Media.Brushes.White,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

        var padding = new MediaSize(6, 3);
        var badge = new Rect(
            zone.X + 5,
            zone.Y + 5,
            text.Width + padding.Width * 2,
            text.Height + padding.Height * 2);

        // Skip the label rather than let it spill out of a small zone.
        if (badge.Width > zone.Width - 6 || badge.Height > zone.Height - 6)
        {
            return;
        }

        drawingContext.DrawRoundedRectangle(
            new SolidColorBrush(MediaColor.FromArgb(210, 32, 32, 32)),
            null,
            badge,
            2,
            2);
        drawingContext.DrawText(text, new System.Windows.Point(badge.X + padding.Width, badge.Y + padding.Height));
    }

    private MediaColor ResourceBrush(string key, MediaColor fallback) =>
        TryFindResource(key) is SolidColorBrush brush ? brush.Color : fallback;
}
