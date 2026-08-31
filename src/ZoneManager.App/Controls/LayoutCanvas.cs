using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using ZoneManager.Core.Geometry;
using ZoneManager.Core.Models;
using InputCursors = System.Windows.Input.Cursors;

namespace ZoneManager.App.Controls;

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
    private NormalizedRect? lastDragBounds;
    private Guid? draggedZoneId;
    private ZoneEdges resizeEdges;
    private ZoneEdges activeSnapEdges;
    private SharedZoneDivider? hoveredSharedDivider;
    private SharedZoneDivider? draggedSharedDivider;
    private IReadOnlyDictionary<Guid, NormalizedRect>? lastSharedBounds;
    private System.Windows.Point lastPointer;

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

        DrawSnapGuides(drawingContext, screen);
        DrawSharedDivider(drawingContext, screen);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs eventArgs)
    {
        base.OnMouseLeftButtonDown(eventArgs);
        var point = eventArgs.GetPosition(this);
        var screen = GetScreenRectangle();
        lastPointer = point;
        var sharedDivider = LayoutCanvasInteraction.FindSharedDivider(Zones, screen, point);
        if (sharedDivider is not null)
        {
            var selectedZoneId = SelectedZoneId == sharedDivider.BeforeZone.Id ||
                                 SelectedZoneId == sharedDivider.AfterZone.Id
                ? SelectedZoneId.Value
                : sharedDivider.BeforeZone.Id;
            SelectedZoneId = selectedZoneId;
            draggedZoneId = null;
            originalBounds = null;
            lastDragBounds = null;
            resizeEdges = ZoneEdges.None;
            draggedSharedDivider = sharedDivider;
            hoveredSharedDivider = sharedDivider;
            lastSharedBounds = new Dictionary<Guid, NormalizedRect>
            {
                [sharedDivider.BeforeZone.Id] = sharedDivider.BeforeZone.Bounds,
                [sharedDivider.AfterZone.Id] = sharedDivider.AfterZone.Bounds
            };
            dragStart = point;
            activeSnapEdges = ZoneEdges.None;
            CaptureMouse();
            ZoneSelected?.Invoke(this, new ZoneSelectedEventArgs(selectedZoneId));
            InvalidateVisual();
            return;
        }

        hoveredSharedDivider = null;
        draggedSharedDivider = null;
        lastSharedBounds = null;
        var zone = LayoutCanvasInteraction.HitTestZone(Zones, SelectedZoneId, screen, point);
        if (zone is null)
        {
            return;
        }

        SelectedZoneId = zone.Id;
        draggedZoneId = zone.Id;
        originalBounds = zone.Bounds;
        lastDragBounds = zone.Bounds;
        dragStart = point;
        resizeEdges = LayoutCanvasInteraction.DetectResizeEdges(
            LayoutCanvasInteraction.ToCanvasRect(zone.Bounds, screen),
            point);
        activeSnapEdges = ZoneEdges.None;
        CaptureMouse();
        ZoneSelected?.Invoke(this, new ZoneSelectedEventArgs(zone.Id));
        InvalidateVisual();
    }

    protected override void OnMouseMove(System.Windows.Input.MouseEventArgs eventArgs)
    {
        base.OnMouseMove(eventArgs);
        var point = eventArgs.GetPosition(this);
        lastPointer = point;
        if (!IsMouseCaptured)
        {
            UpdatePointerInteraction(point);
            return;
        }

        if (draggedSharedDivider is not null)
        {
            UpdateSharedDividerDrag(point, false);
        }
        else if (draggedZoneId is not null && originalBounds is not null)
        {
            UpdateDrag(point, IsMagnetismPaused(), false);
        }
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs eventArgs)
    {
        base.OnMouseLeftButtonUp(eventArgs);
        var point = eventArgs.GetPosition(this);
        lastPointer = point;
        if (IsMouseCaptured && draggedSharedDivider is not null)
        {
            UpdateSharedDividerDrag(point, true);
        }
        else if (IsMouseCaptured && draggedZoneId is not null && originalBounds is not null)
        {
            UpdateDrag(point, IsMagnetismPaused(), true);
        }

        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }

        draggedZoneId = null;
        originalBounds = null;
        lastDragBounds = null;
        resizeEdges = ZoneEdges.None;
        activeSnapEdges = ZoneEdges.None;
        draggedSharedDivider = null;
        lastSharedBounds = null;
        UpdatePointerInteraction(point);
    }

    protected override void OnMouseLeave(System.Windows.Input.MouseEventArgs eventArgs)
    {
        base.OnMouseLeave(eventArgs);
        if (!IsMouseCaptured)
        {
            hoveredSharedDivider = null;
            Cursor = InputCursors.Arrow;
            InvalidateVisual();
        }
    }

    private void DrawZone(DrawingContext context, Rect screen, ZoneDefinition zone, bool invalid)
    {
        var rectangle = LayoutCanvasInteraction.ToCanvasRect(zone.Bounds, screen);
        var selected = zone.Id == SelectedZoneId;
        var fillColour = ResourceBrush(
            invalid ? "DangerBrush" : "ZoneFillBrush",
            invalid ? System.Windows.Media.Color.FromRgb(198, 54, 54) : System.Windows.Media.Color.FromRgb(112, 112, 112)).Color;
        var borderColour = ResourceBrush(
            invalid ? "DangerBrush" : selected ? "AccentBrush" : "ZoneBorderBrush",
            invalid ? System.Windows.Media.Color.FromRgb(198, 54, 54) : System.Windows.Media.Color.FromRgb(160, 160, 160)).Color;
        context.DrawRoundedRectangle(
            new SolidColorBrush(fillColour) { Opacity = selected ? 0.32 : 0.18 },
            new System.Windows.Media.Pen(new SolidColorBrush(borderColour), selected ? 3 : 1.5),
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
                context.DrawRectangle(new SolidColorBrush(borderColour), new System.Windows.Media.Pen(System.Windows.Media.Brushes.White, 1), new Rect(handle.X - 4, handle.Y - 4, 8, 8));
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

    private void UpdateDrag(System.Windows.Point point, bool pauseMagnetism, bool forceNotification)
    {
        if (draggedZoneId is null || originalBounds is null)
        {
            return;
        }

        var screen = GetScreenRectangle();
        var deltaX = (point.X - dragStart.X) / screen.Width;
        var deltaY = (point.Y - dragStart.Y) / screen.Height;
        var otherBounds = Zones
            .Where(zone => zone.Id != draggedZoneId.Value)
            .Select(zone => zone.Bounds)
            .ToArray();
        var snapResult = LayoutCanvasInteraction.ApplyDrag(
            originalBounds,
            deltaX,
            deltaY,
            resizeEdges,
            otherBounds,
            MagnetThresholdPixels,
            MonitorPixelWidth,
            MonitorPixelHeight,
            pauseMagnetism);

        var boundsChanged = lastDragBounds is null || snapResult.Bounds != lastDragBounds;
        lastDragBounds = snapResult.Bounds;
        activeSnapEdges = snapResult.SnappedEdges;
        InvalidateVisual();
        if (forceNotification || boundsChanged)
        {
            ZoneChanged?.Invoke(this, new ZoneChangedEventArgs(draggedZoneId.Value, snapResult.Bounds));
        }
    }

    private void UpdatePointerInteraction(System.Windows.Point point)
    {
        var screen = GetScreenRectangle();
        hoveredSharedDivider = LayoutCanvasInteraction.FindSharedDivider(Zones, screen, point);
        if (hoveredSharedDivider is not null)
        {
            Cursor = hoveredSharedDivider.Orientation == SharedDividerOrientation.Vertical
                ? InputCursors.SizeWE
                : InputCursors.SizeNS;
            InvalidateVisual();
            return;
        }

        var zone = LayoutCanvasInteraction.HitTestZone(Zones, SelectedZoneId, screen, point);
        if (zone is null)
        {
            Cursor = InputCursors.Arrow;
            InvalidateVisual();
            return;
        }

        var edges = LayoutCanvasInteraction.DetectResizeEdges(
            LayoutCanvasInteraction.ToCanvasRect(zone.Bounds, screen),
            point);
        Cursor = edges switch
        {
            ZoneEdges.Left | ZoneEdges.Top or ZoneEdges.Right | ZoneEdges.Bottom => InputCursors.SizeNWSE,
            ZoneEdges.Right | ZoneEdges.Top or ZoneEdges.Left | ZoneEdges.Bottom => InputCursors.SizeNESW,
            ZoneEdges.Left or ZoneEdges.Right => InputCursors.SizeWE,
            ZoneEdges.Top or ZoneEdges.Bottom => InputCursors.SizeNS,
            _ => InputCursors.SizeAll
        };
        InvalidateVisual();
    }

    private void UpdateSharedDividerDrag(System.Windows.Point point, bool forceNotification)
    {
        if (draggedSharedDivider is null)
        {
            return;
        }

        var screen = GetScreenRectangle();
        var delta = draggedSharedDivider.Orientation == SharedDividerOrientation.Vertical
            ? (point.X - dragStart.X) / screen.Width
            : (point.Y - dragStart.Y) / screen.Height;
        var changedBounds = LayoutCanvasInteraction.ResizeSharedDivider(draggedSharedDivider, delta);
        var boundsChanged = lastSharedBounds is null || !BoundsEqual(lastSharedBounds, changedBounds);
        lastSharedBounds = changedBounds;
        InvalidateVisual();
        if (forceNotification || boundsChanged)
        {
            var selectedZoneId = SelectedZoneId == draggedSharedDivider.BeforeZone.Id ||
                                 SelectedZoneId == draggedSharedDivider.AfterZone.Id
                ? SelectedZoneId.Value
                : draggedSharedDivider.BeforeZone.Id;
            ZoneChanged?.Invoke(this, new ZoneChangedEventArgs(selectedZoneId, changedBounds));
        }
    }

    private void DrawSnapGuides(DrawingContext context, Rect screen)
    {
        if (lastDragBounds is null || activeSnapEdges == ZoneEdges.None)
        {
            return;
        }

        var accent = ResourceBrush("AccentBrush", System.Windows.Media.Color.FromRgb(0, 120, 212)).Color;
        var haloBrush = new SolidColorBrush(accent) { Opacity = 0.22 };
        var guideBrush = new SolidColorBrush(accent);
        var haloPen = new System.Windows.Media.Pen(haloBrush, 7);
        var guidePen = new System.Windows.Media.Pen(guideBrush, 2)
        {
            DashStyle = new DashStyle([6d, 4d], 0)
        };

        foreach (var guide in LayoutCanvasInteraction.GetSnapGuides(lastDragBounds, screen, activeSnapEdges))
        {
            DrawGuide(context, guide.Start, guide.End, haloPen, guidePen);
        }
    }

    private void DrawSharedDivider(DrawingContext context, Rect screen)
    {
        var divider = draggedSharedDivider ?? hoveredSharedDivider;
        if (divider is null)
        {
            return;
        }

        if (draggedSharedDivider is not null && lastSharedBounds is not null)
        {
            divider = divider with
            {
                BeforeZone = divider.BeforeZone with { Bounds = lastSharedBounds[divider.BeforeZone.Id] },
                AfterZone = divider.AfterZone with { Bounds = lastSharedBounds[divider.AfterZone.Id] },
                Boundary = divider.Orientation == SharedDividerOrientation.Vertical
                    ? lastSharedBounds[divider.AfterZone.Id].X
                    : lastSharedBounds[divider.AfterZone.Id].Y
            };
        }

        var visual = LayoutCanvasInteraction.GetSharedDividerVisual(divider, screen, lastPointer);
        var accent = ResourceBrush("AccentBrush", System.Windows.Media.Color.FromRgb(0, 120, 212)).Color;
        var haloBrush = new SolidColorBrush(accent) { Opacity = 0.2 };
        var accentBrush = new SolidColorBrush(accent);
        context.DrawLine(new System.Windows.Media.Pen(haloBrush, 9), visual.Line.Start, visual.Line.End);
        context.DrawLine(new System.Windows.Media.Pen(accentBrush, 2.5), visual.Line.Start, visual.Line.End);
        context.DrawRoundedRectangle(
            accentBrush,
            new System.Windows.Media.Pen(System.Windows.Media.Brushes.White, 1.5),
            visual.Handle,
            6,
            6);

        var handleCentre = new System.Windows.Point(
            visual.Handle.Left + (visual.Handle.Width / 2),
            visual.Handle.Top + (visual.Handle.Height / 2));
        var handlePen = new System.Windows.Media.Pen(System.Windows.Media.Brushes.White, 1);
        if (divider.Orientation == SharedDividerOrientation.Vertical)
        {
            context.DrawLine(handlePen, new System.Windows.Point(handleCentre.X - 2, handleCentre.Y - 8), new System.Windows.Point(handleCentre.X - 2, handleCentre.Y + 8));
            context.DrawLine(handlePen, new System.Windows.Point(handleCentre.X + 2, handleCentre.Y - 8), new System.Windows.Point(handleCentre.X + 2, handleCentre.Y + 8));
        }
        else
        {
            context.DrawLine(handlePen, new System.Windows.Point(handleCentre.X - 8, handleCentre.Y - 2), new System.Windows.Point(handleCentre.X + 8, handleCentre.Y - 2));
            context.DrawLine(handlePen, new System.Windows.Point(handleCentre.X - 8, handleCentre.Y + 2), new System.Windows.Point(handleCentre.X + 8, handleCentre.Y + 2));
        }
    }

    private static void DrawGuide(
        DrawingContext context,
        System.Windows.Point start,
        System.Windows.Point end,
        System.Windows.Media.Pen haloPen,
        System.Windows.Media.Pen guidePen)
    {
        context.DrawLine(haloPen, start, end);
        context.DrawLine(guidePen, start, end);
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

    private static bool IsMagnetismPaused() =>
        Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt);

    private static bool BoundsEqual(
        IReadOnlyDictionary<Guid, NormalizedRect> first,
        IReadOnlyDictionary<Guid, NormalizedRect> second) =>
        first.Count == second.Count &&
        first.All(entry => second.TryGetValue(entry.Key, out var bounds) && bounds == entry.Value);

    private SolidColorBrush ResourceBrush(string key, System.Windows.Media.Color fallback) =>
        TryFindResource(key) as SolidColorBrush ?? new SolidColorBrush(fallback);
}

public sealed class ZoneSelectedEventArgs(Guid zoneId) : EventArgs
{
    public Guid ZoneId { get; } = zoneId;
}

public sealed class ZoneChangedEventArgs : EventArgs
{
    public ZoneChangedEventArgs(Guid zoneId, NormalizedRect bounds)
        : this(zoneId, new Dictionary<Guid, NormalizedRect> { [zoneId] = bounds })
    {
    }

    public ZoneChangedEventArgs(Guid selectedZoneId, IReadOnlyDictionary<Guid, NormalizedRect> changedBounds)
    {
        SelectedZoneId = selectedZoneId;
        ChangedBounds = changedBounds;
    }

    public Guid SelectedZoneId { get; }
    public IReadOnlyDictionary<Guid, NormalizedRect> ChangedBounds { get; }
}
