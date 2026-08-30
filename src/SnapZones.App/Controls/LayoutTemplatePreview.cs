using System.Windows;
using System.Windows.Media;
using SnapZones.Core.Editor;
using MediaColor = System.Windows.Media.Color;

namespace SnapZones.App.Controls;

public sealed class LayoutTemplatePreview : FrameworkElement
{
    public static readonly DependencyProperty SuggestionProperty = DependencyProperty.Register(
        nameof(Suggestion),
        typeof(LayoutSuggestion),
        typeof(LayoutTemplatePreview),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public LayoutSuggestion? Suggestion
    {
        get => (LayoutSuggestion?)GetValue(SuggestionProperty);
        set => SetValue(SuggestionProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (Suggestion is not { } suggestion || ActualWidth <= 2 || ActualHeight <= 2)
        {
            return;
        }

        var aspectRatio = Math.Clamp(suggestion.MonitorAspectRatio, 0.35, 4.0);
        var availableWidth = ActualWidth - 2;
        var availableHeight = ActualHeight - 2;
        var previewWidth = Math.Min(availableWidth, availableHeight * aspectRatio);
        var previewHeight = previewWidth / aspectRatio;
        var preview = new Rect(
            (ActualWidth - previewWidth) / 2,
            (ActualHeight - previewHeight) / 2,
            previewWidth,
            previewHeight);

        var surface = ResourceColour("SurfaceRaisedBrush", MediaColor.FromRgb(51, 51, 51));
        var zoneFill = ResourceColour("ZoneFillBrush", MediaColor.FromRgb(112, 112, 112));
        var zoneBorder = ResourceColour("AccentBrush", MediaColor.FromRgb(160, 160, 160));
        var outerBorder = ResourceColour("ControlBorderBrush", MediaColor.FromRgb(130, 130, 130));
        drawingContext.DrawRoundedRectangle(
            new SolidColorBrush(surface),
            new System.Windows.Media.Pen(new SolidColorBrush(outerBorder), 1),
            preview,
            3,
            3);

        foreach (var zone in suggestion.Zones)
        {
            var rectangle = new Rect(
                preview.X + zone.Bounds.X * preview.Width + 1.5,
                preview.Y + zone.Bounds.Y * preview.Height + 1.5,
                Math.Max(0, zone.Bounds.Width * preview.Width - 3),
                Math.Max(0, zone.Bounds.Height * preview.Height - 3));
            drawingContext.DrawRoundedRectangle(
                new SolidColorBrush(zoneFill) { Opacity = 0.55 },
                new System.Windows.Media.Pen(new SolidColorBrush(zoneBorder), 1),
                rectangle,
                1.5,
                1.5);
        }
    }

    private MediaColor ResourceColour(string key, MediaColor fallback) =>
        TryFindResource(key) is SolidColorBrush brush ? brush.Color : fallback;
}
