using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using SnapZones.Core.Geometry;
using SnapZones.Core.Models;

namespace SnapZones.App.Controls;

public sealed class LayoutCanvas : FrameworkElement
{
    public static readonly DependencyProperty ZonesProperty = DependencyProperty.Register(
        nameof(Zones),
        typeof(IReadOnlyList<ZoneDefinition>),
        typeof(LayoutCanvas),
        new FrameworkPropertyMetadata(Array.Empty<ZoneDefinition>(), FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty SelectedZoneIdProperty = DependencyProperty.Register(
        nameof(SelectedZoneId),
        typeof(Guid?),
        typeof(LayoutCanvas),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MonitorAspectRatioProperty = DependencyProperty.Register(
        nameof(MonitorAspectRatio),
        typeof(double),
        typeof(LayoutCanvas),
        new FrameworkPropertyMetadata(16d / 9d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty MonitorPixelWidthProperty = DependencyProperty.Register(
        nameof(MonitorPixelWidth),
        typeof(int),
        typeof(LayoutCanvas),
        new FrameworkPropertyMetadata(1920));

    public static readonly DependencyProperty MonitorPixelHeightProperty = DependencyProperty.Register(
        nameof(MonitorPixelHeight),
        typeof(int),
        typeof(LayoutCanvas),
        new FrameworkPropertyMetadata(1080));

    public static readonly DependencyProperty MagnetThresholdPixelsProperty = DependencyProperty.Register(
        nameof(MagnetThresholdPixels),
        typeof(int),
        typeof(LayoutCanvas),
        new FrameworkPropertyMetadata(10));

    private System.Windows.Point dragStart;
    private NormalizedRect? originalBounds;
    private Guid? draggedZoneId;
    private ResizeEdges resizeEdges;

    public IReadOnlyList<ZoneDefinition> Zones
    {
        get => (IReadOnlyList<ZoneDefinition>)GetValue(ZonesProperty);
        set => SetValue(ZonesProperty, value);
    }

    public Guid? SelectedZoneId
    {
        get => (Guid?)GetValue(SelectedZoneIdProperty);
        set => SetValue(SelectedZoneIdProperty, value);
    }

    public double MonitorAspectRatio
    {
        get => (double)GetValue(MonitorAspectRatioProperty);
        set => SetValue(MonitorAspectRatioProperty, value);
    }

    public int MonitorPixelWidth
    {
        get => (int)GetValue(MonitorPixelWidthProperty);
        set => SetValue(MonitorPixelWidthProperty, value);
    }

    public int MonitorPixelHeight
    {
        get => (int)GetValue(MonitorPixelHeightProperty);
        set => SetValue(MonitorPixelHeightProperty, value);
    }

    public int MagnetThresholdPixels
    {
        get => (int)GetValue(MagnetThresholdPixelsProperty);
        set => SetValue(MagnetThresholdPixelsProperty, value);
    }

    public event EventHandler<ZoneSelectedEventArgs>? ZoneSelected;
    public event EventHandler<ZoneChangedEventArgs>? ZoneChanged;

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        var monitor = GetMonitorRectangle();
        drawingContext.DrawRoundedRectangle(
            ResourceBrush("MonitorFrameBrush", System.Windows.Media.Color.FromRgb(23, 32, 51)),
            null,
            monitor,
            12,
            12);
        var screen = new Rect(monitor.X + 8, monitor.Y + 8, Math.Max(1, monitor.Width - 16), Math.Max(1, monitor.Height - 16));
        drawingContext.DrawRoundedRectangle(
            ResourceBrush("MonitorScreenBrush", System.Windows.Media.Color.FromRgb(244, 247, 251)),
            null,
            screen,
            7,
            7);

        var validation = ZoneGeometry.Validate(Zones);
        var invalidIds = validation.Errors.Where(error => error.ZoneId.HasValue).Select(error => error.ZoneId!.Value).ToHashSet();
        foreach (var zone in Zones)
        {
            DrawZone(drawingContext, screen, zone, invalidIds.Contains(zone.Id));
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs eventArgs)
    {
        base.OnMouseLeftButtonDown(eventArgs);
        var point = eventArgs.GetPosition(this);
        var screen = GetScreenRectangle();
        var zone = Zones.Reverse().FirstOrDefault(candidate => ToCanvasRect(candidate.Bounds, screen).Contains(point));
        if (zone is null)
        {
            return;
        }

        SelectedZoneId = zone.Id;
        draggedZoneId = zone.Id;
        originalBounds = zone.Bounds;
        dragStart = point;
        resizeEdges = DetectResizeEdges(ToCanvasRect(zone.Bounds, screen), point);
        CaptureMouse();
        ZoneSelected?.Invoke(this, new ZoneSelectedEventArgs(zone.Id));
        InvalidateVisual();
    }

    protected override void OnMouseMove(System.Windows.Input.MouseEventArgs eventArgs)
    {
        base.OnMouseMove(eventArgs);
        if (!IsMouseCaptured || draggedZoneId is null || originalBounds is null)
        {
            return;
        }

        var screen = GetScreenRectangle();
        var point = eventArgs.GetPosition(this);
        var deltaX = (point.X - dragStart.X) / screen.Width;
        var deltaY = (point.Y - dragStart.Y) / screen.Height;
        var changed = resizeEdges == ResizeEdges.None
            ? Move(originalBounds, deltaX, deltaY)
            : Resize(originalBounds, deltaX, deltaY, resizeEdges);
        if (!Keyboard.IsKeyDown(Key.LeftAlt) && !Keyboard.IsKeyDown(Key.RightAlt))
        {
            var otherBounds = Zones
                .Where(zone => zone.Id != draggedZoneId.Value)
                .Select(zone => zone.Bounds)
                .ToArray();
            changed = resizeEdges == ResizeEdges.None
                ? ZoneMagnetism.SnapMove(
                    changed,
                    otherBounds,
                    MagnetThresholdPixels,
                    MonitorPixelWidth,
                    MonitorPixelHeight)
                : ZoneMagnetism.SnapResize(
                    changed,
                    otherBounds,
                    ToZoneEdges(resizeEdges),
                    MagnetThresholdPixels,
                    MonitorPixelWidth,
                    MonitorPixelHeight);
        }
        ZoneChanged?.Invoke(this, new ZoneChangedEventArgs(draggedZoneId.Value, changed));
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs eventArgs)
    {
        base.OnMouseLeftButtonUp(eventArgs);
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }

        draggedZoneId = null;
        originalBounds = null;
        resizeEdges = ResizeEdges.None;
    }

    private void DrawZone(DrawingContext context, Rect screen, ZoneDefinition zone, bool invalid)
    {
        var rectangle = ToCanvasRect(zone.Bounds, screen);
        var selected = zone.Id == SelectedZoneId;
        var colour = ResourceBrush(
            invalid ? "DangerBrush" : "AccentBrush",
            invalid ? System.Windows.Media.Color.FromRgb(198, 54, 54) : System.Windows.Media.Color.FromRgb(47, 111, 237)).Color;
        context.DrawRoundedRectangle(
            new SolidColorBrush(colour) { Opacity = selected ? 0.32 : 0.16 },
            new System.Windows.Media.Pen(new SolidColorBrush(colour), selected ? 3 : 1.5),
            rectangle,
            6,
            6);
        var text = new FormattedText(
            zone.Name,
            System.Globalization.CultureInfo.CurrentUICulture,
            System.Windows.FlowDirection.LeftToRight,
            new Typeface("Segoe UI Variable Text"),
            selected ? 15 : 13,
            ResourceBrush("InkBrush", System.Windows.Media.Color.FromRgb(23, 32, 51)),
            VisualTreeHelper.GetDpi(this).PixelsPerDip)
        {
            MaxTextWidth = Math.Max(1, rectangle.Width - 20),
            Trimming = TextTrimming.CharacterEllipsis
        };
        context.DrawText(text, new System.Windows.Point(rectangle.X + 10, rectangle.Y + 9));

        if (selected)
        {
            foreach (var handle in HandlePoints(rectangle))
            {
                context.DrawRectangle(new SolidColorBrush(colour), new System.Windows.Media.Pen(System.Windows.Media.Brushes.White, 1), new Rect(handle.X - 4, handle.Y - 4, 8, 8));
            }
        }
    }

    private Rect GetMonitorRectangle()
    {
        const double padding = 24;
        var availableWidth = Math.Max(1, ActualWidth - (2 * padding));
        var availableHeight = Math.Max(1, ActualHeight - (2 * padding));
        var ratio = MonitorAspectRatio > 0 ? MonitorAspectRatio : 16d / 9d;
        var width = Math.Min(availableWidth, availableHeight * ratio);
        var height = width / ratio;
        return new Rect((ActualWidth - width) / 2, (ActualHeight - height) / 2, width, height);
    }

    private Rect GetScreenRectangle()
    {
        var monitor = GetMonitorRectangle();
        return new Rect(monitor.X + 8, monitor.Y + 8, Math.Max(1, monitor.Width - 16), Math.Max(1, monitor.Height - 16));
    }

    private static Rect ToCanvasRect(NormalizedRect bounds, Rect screen) => new(
        screen.X + (bounds.X * screen.Width),
        screen.Y + (bounds.Y * screen.Height),
        bounds.Width * screen.Width,
        bounds.Height * screen.Height);

    private static NormalizedRect Move(NormalizedRect original, double deltaX, double deltaY) => new(
        Math.Clamp(original.X + deltaX, 0, 1 - original.Width),
        Math.Clamp(original.Y + deltaY, 0, 1 - original.Height),
        original.Width,
        original.Height);

    private static NormalizedRect Resize(NormalizedRect original, double deltaX, double deltaY, ResizeEdges edges)
    {
        const double minimum = 0.04;
        var left = original.X;
        var top = original.Y;
        var right = original.X + original.Width;
        var bottom = original.Y + original.Height;
        if (edges.HasFlag(ResizeEdges.Left)) left = Math.Clamp(left + deltaX, 0, right - minimum);
        if (edges.HasFlag(ResizeEdges.Right)) right = Math.Clamp(right + deltaX, left + minimum, 1);
        if (edges.HasFlag(ResizeEdges.Top)) top = Math.Clamp(top + deltaY, 0, bottom - minimum);
        if (edges.HasFlag(ResizeEdges.Bottom)) bottom = Math.Clamp(bottom + deltaY, top + minimum, 1);
        return new NormalizedRect(left, top, right - left, bottom - top);
    }

    private static ResizeEdges DetectResizeEdges(Rect rectangle, System.Windows.Point point)
    {
        const double tolerance = 12;
        var edges = ResizeEdges.None;
        if (Math.Abs(point.X - rectangle.Left) <= tolerance) edges |= ResizeEdges.Left;
        if (Math.Abs(point.X - rectangle.Right) <= tolerance) edges |= ResizeEdges.Right;
        if (Math.Abs(point.Y - rectangle.Top) <= tolerance) edges |= ResizeEdges.Top;
        if (Math.Abs(point.Y - rectangle.Bottom) <= tolerance) edges |= ResizeEdges.Bottom;
        return edges;
    }

    private static IEnumerable<System.Windows.Point> HandlePoints(Rect rectangle)
    {
        var centerX = rectangle.Left + rectangle.Width / 2;
        var centerY = rectangle.Top + rectangle.Height / 2;
        yield return rectangle.TopLeft;
        yield return new System.Windows.Point(centerX, rectangle.Top);
        yield return rectangle.TopRight;
        yield return new System.Windows.Point(rectangle.Right, centerY);
        yield return rectangle.BottomRight;
        yield return new System.Windows.Point(centerX, rectangle.Bottom);
        yield return rectangle.BottomLeft;
        yield return new System.Windows.Point(rectangle.Left, centerY);
    }

    private static ZoneEdges ToZoneEdges(ResizeEdges edges)
    {
        var result = ZoneEdges.None;
        if (edges.HasFlag(ResizeEdges.Left)) result |= ZoneEdges.Left;
        if (edges.HasFlag(ResizeEdges.Top)) result |= ZoneEdges.Top;
        if (edges.HasFlag(ResizeEdges.Right)) result |= ZoneEdges.Right;
        if (edges.HasFlag(ResizeEdges.Bottom)) result |= ZoneEdges.Bottom;
        return result;
    }

    private SolidColorBrush ResourceBrush(string key, System.Windows.Media.Color fallback) =>
        TryFindResource(key) as SolidColorBrush ?? new SolidColorBrush(fallback);

    [Flags]
    private enum ResizeEdges
    {
        None = 0,
        Left = 1,
        Top = 2,
        Right = 4,
        Bottom = 8
    }
}

public sealed class ZoneSelectedEventArgs(Guid zoneId) : EventArgs
{
    public Guid ZoneId { get; } = zoneId;
}

public sealed class ZoneChangedEventArgs(Guid zoneId, NormalizedRect bounds) : EventArgs
{
    public Guid ZoneId { get; } = zoneId;
    public NormalizedRect Bounds { get; } = bounds;
}
