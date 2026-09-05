using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using SnapZones.Core.Geometry;
using SnapZones.Core.Models;
using InputCursors = System.Windows.Input.Cursors;
using MediaColor = System.Windows.Media.Color;

namespace SnapZones.App.Controls;

/// <summary>Wo die Zeichenflaeche steht: im Fenster (mit Monitorrahmen) oder in echter Groesse auf dem Monitor.</summary>
public enum CanvasPresentation
{
    Embedded,
    Fullscreen
}

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

    public static readonly DependencyProperty MainZoneIdProperty = DependencyProperty.Register(
        nameof(MainZoneId),
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

    public static readonly DependencyProperty PresentationProperty = DependencyProperty.Register(
        nameof(Presentation),
        typeof(CanvasPresentation),
        typeof(LayoutCanvas),
        new FrameworkPropertyMetadata(CanvasPresentation.Embedded, FrameworkPropertyMetadataOptions.AffectsRender));

    /// <summary>
    /// Abstand zwischen den Zonen in Pixeln des Monitors, wie ihn das Overlay zeigt. Nur im Vollbild
    /// gezeichnet, wo die Zonen ihre echte Groesse haben.
    /// </summary>
    public static readonly DependencyProperty ZoneGapPixelsProperty = DependencyProperty.Register(
        nameof(ZoneGapPixels),
        typeof(int),
        typeof(LayoutCanvas),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

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

    public LayoutCanvas()
    {
        Focusable = true;
        FocusVisualStyle = null;
    }

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

    /// <summary>Die Auffangzone; sie erhält im Editor eine eigene, beschriftete Kennzeichnung.</summary>
    public Guid? MainZoneId
    {
        get => (Guid?)GetValue(MainZoneIdProperty);
        set => SetValue(MainZoneIdProperty, value);
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

    public CanvasPresentation Presentation
    {
        get => (CanvasPresentation)GetValue(PresentationProperty);
        set => SetValue(PresentationProperty, value);
    }

    public int ZoneGapPixels
    {
        get => (int)GetValue(ZoneGapPixelsProperty);
        set => SetValue(ZoneGapPixelsProperty, value);
    }

    public event EventHandler<ZoneSelectedEventArgs>? ZoneSelected;
    public event EventHandler<ZoneChangedEventArgs>? ZoneChanged;

    /// <summary>Doppelklick auf eine Zone: der Anwender will sie umbenennen.</summary>
    public event EventHandler<ZoneSelectedEventArgs>? ZoneRenameRequested;

    /// <summary>Rechtsklick auf eine Zone: der Aufrufer zeigt das Kontextmenue.</summary>
    public event EventHandler<ZoneContextMenuEventArgs>? ZoneContextMenuRequested;

    /// <summary>Entf-Taste auf der ausgewaehlten Zone.</summary>
    public event EventHandler<ZoneSelectedEventArgs>? ZoneDeleteRequested;

    /// <summary>Ein Ziehen mit der Maus beginnt; alle Aenderungen bis <see cref="DragEnded"/> gehoeren zusammen.</summary>
    public event EventHandler? DragStarted;

    /// <summary>Das Ziehen ist zu Ende, auch wenn die Maus den Fokus verloren hat.</summary>
    public event EventHandler? DragEnded;

    private bool IsFullscreen => Presentation == CanvasPresentation.Fullscreen;

    /// <summary>Die Monitorflaeche in Steuerelementkoordinaten; im Vollbild die ganze Flaeche.</summary>
    public Rect ScreenRectangle => GetScreenRectangle();

    /// <summary>Wo die Beschriftung einer Zone steht, fuer das Textfeld beim Umbenennen; null ohne die Zone.</summary>
    public Rect? GetZoneLabelRect(Guid zoneId)
    {
        var zone = Zones.FirstOrDefault(candidate => candidate.Id == zoneId);
        if (zone is null)
        {
            return null;
        }

        var rectangle = LayoutCanvasInteraction.ToCanvasRect(zone.Bounds, GetScreenRectangle());
        var height = IsFullscreen ? 34 : 28;
        var width = Math.Clamp(rectangle.Width - 20, 80, 320);
        return new Rect(rectangle.X + 10, rectangle.Y + 8, width, height);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        var screen = GetScreenRectangle();
        if (!IsFullscreen)
        {
            var monitor = GetMonitorRectangle();
            drawingContext.DrawRoundedRectangle(
                ResourceBrush("MonitorFrameBrush", MediaColor.FromRgb(23, 32, 51)),
                null,
                monitor,
                12,
                12);
            drawingContext.DrawRoundedRectangle(
                ResourceBrush("MonitorScreenBrush", MediaColor.FromRgb(244, 247, 251)),
                null,
                screen,
                7,
                7);
        }

        var validation = ZoneGeometry.Validate(Zones);
        var invalidIds = validation.Errors.Where(error => error.ZoneId.HasValue).Select(error => error.ZoneId!.Value).ToHashSet();
        foreach (var zone in Zones)
        {
            DrawZone(drawingContext, screen, zone, invalidIds.Contains(zone.Id));
        }

        DrawSnapGuides(drawingContext, screen);
        DrawSharedDivider(drawingContext, screen);
        DrawDragMeasurement(drawingContext, screen);
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs eventArgs)
    {
        base.OnMouseLeftButtonDown(eventArgs);
        Focus();
        var point = eventArgs.GetPosition(this);
        var screen = GetScreenRectangle();
        lastPointer = point;
        if (eventArgs.ClickCount == 2)
        {
            var doubleClicked = LayoutCanvasInteraction.HitTestZone(Zones, SelectedZoneId, screen, point);
            if (doubleClicked is not null)
            {
                SelectedZoneId = doubleClicked.Id;
                ZoneSelected?.Invoke(this, new ZoneSelectedEventArgs(doubleClicked.Id));
                ZoneRenameRequested?.Invoke(this, new ZoneSelectedEventArgs(doubleClicked.Id));
                eventArgs.Handled = true;
                return;
            }
        }

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
            DragStarted?.Invoke(this, EventArgs.Empty);
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
        DragStarted?.Invoke(this, EventArgs.Empty);
        CaptureMouse();
        ZoneSelected?.Invoke(this, new ZoneSelectedEventArgs(zone.Id));
        InvalidateVisual();
    }

    protected override void OnMouseRightButtonUp(MouseButtonEventArgs eventArgs)
    {
        base.OnMouseRightButtonUp(eventArgs);
        var point = eventArgs.GetPosition(this);
        var zone = LayoutCanvasInteraction.HitTestZone(Zones, SelectedZoneId, GetScreenRectangle(), point);
        if (zone is null)
        {
            return;
        }

        SelectedZoneId = zone.Id;
        ZoneSelected?.Invoke(this, new ZoneSelectedEventArgs(zone.Id));
        InvalidateVisual();
        ZoneContextMenuRequested?.Invoke(this, new ZoneContextMenuEventArgs(zone.Id, point));
        eventArgs.Handled = true;
    }

    protected override void OnKeyDown(System.Windows.Input.KeyEventArgs eventArgs)
    {
        base.OnKeyDown(eventArgs);
        if (eventArgs.Key == Key.Delete && SelectedZoneId is { } selected && Zones.Any(zone => zone.Id == selected))
        {
            ZoneDeleteRequested?.Invoke(this, new ZoneSelectedEventArgs(selected));
            eventArgs.Handled = true;
        }
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
            DragEnded?.Invoke(this, EventArgs.Empty);
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

    /// <summary>
    /// Kennzeichnet die Auffangzone mit einem beschrifteten Feld neben dem Zonennamen. Bewusst mit Text und
    /// nicht nur mit Farbe: die Kennzeichnung muss auch ohne Farbwahrnehmung erkennbar sein.
    /// </summary>
    private void DrawMainZoneBadge(DrawingContext context, Rect rectangle, double labelRight, double labelTop, double labelHeight)
    {
        var badge = Text("Auffangzone", IsFullscreen ? 12 : 11, System.Windows.Media.Brushes.White, 200);
        var area = new Rect(labelRight + 8, labelTop + (labelHeight - badge.Height - 4) / 2, badge.Width + 12, badge.Height + 4);
        if (area.Right > rectangle.Right - 6 || area.Bottom > rectangle.Bottom - 6)
        {
            return;
        }

        context.DrawRoundedRectangle(
            IsFullscreen
                ? new SolidColorBrush(MediaColor.FromArgb(200, 32, 32, 32))
                : ResourceBrush("AccentBrush", MediaColor.FromRgb(47, 111, 237)),
            null,
            area,
            4,
            4);
        context.DrawText(badge, new System.Windows.Point(area.X + 6, area.Y + 2));
    }

    /// <summary>Die Nummer einer Zone: ihre Position in der Zonenliste, ab eins gezaehlt.</summary>
    private int ZoneNumber(ZoneDefinition zone)
    {
        for (var index = 0; index < Zones.Count; index++)
        {
            if (Zones[index].Id == zone.Id)
            {
                return index + 1;
            }
        }

        return 0;
    }

    private void DrawZone(DrawingContext context, Rect screen, ZoneDefinition zone, bool invalid)
    {
        var rectangle = LayoutCanvasInteraction.ToCanvasRect(zone.Bounds, screen);
        if (IsFullscreen)
        {
            // Der halbe Zonenabstand auf jeder Seite: so liegen die Zonen genau so auseinander wie im Overlay.
            var inset = Math.Min(ZoneGapPixels / 2d * (screen.Width / Math.Max(1, MonitorPixelWidth)), Math.Min(rectangle.Width, rectangle.Height) / 4);
            rectangle.Inflate(-inset, -inset);
        }

        var selected = zone.Id == SelectedZoneId;
        var fillColour = ResourceBrush(
            invalid ? "DangerBrush" : "ZoneFillBrush",
            invalid ? MediaColor.FromRgb(198, 54, 54) : MediaColor.FromRgb(112, 112, 112)).Color;
        var borderColour = ResourceBrush(
            invalid ? "DangerBrush" : selected ? "AccentBrush" : "ZoneBorderBrush",
            invalid ? MediaColor.FromRgb(198, 54, 54) : MediaColor.FromRgb(160, 160, 160)).Color;
        var borderThickness = IsFullscreen ? (selected ? 3 : 2) : (selected ? 3 : 1.5);
        context.DrawRoundedRectangle(
            new SolidColorBrush(fillColour) { Opacity = selected ? 0.32 : IsFullscreen ? 0.20 : 0.18 },
            new System.Windows.Media.Pen(new SolidColorBrush(borderColour), borderThickness),
            rectangle,
            6,
            6);

        // Die Nummer vor dem Namen ist dieselbe wie im Overlay und im Tastenkuerzel.
        var labelText = $"{ZoneNumber(zone)} · {zone.Name}";
        if (IsFullscreen)
        {
            var label = Text(labelText, 16, System.Windows.Media.Brushes.White, rectangle.Width - 40, FontWeights.SemiBold);
            var pill = new Rect(rectangle.X + 12, rectangle.Y + 12, label.Width + 20, label.Height + 12);
            context.DrawRoundedRectangle(new SolidColorBrush(MediaColor.FromArgb(191, 0, 0, 0)), null, pill, 5, 5);
            context.DrawText(label, new System.Windows.Point(pill.X + 10, pill.Y + 6));
            if (zone.Id == MainZoneId)
            {
                DrawMainZoneBadge(context, rectangle, pill.Right, pill.Y, pill.Height);
            }

            DrawDimensionBadge(context, rectangle, zone);
        }
        else
        {
            var label = Text(labelText, selected ? 15 : 13, ResourceBrush("InkBrush", MediaColor.FromRgb(23, 32, 51)), rectangle.Width - 20);
            context.DrawText(label, new System.Windows.Point(rectangle.X + 10, rectangle.Y + 9));
            if (zone.Id == MainZoneId)
            {
                DrawMainZoneBadge(context, rectangle, rectangle.X + 10 + label.Width, rectangle.Y + 8, label.Height + 2);
            }
        }

        if (selected)
        {
            var handleSize = IsFullscreen ? 10d : 8d;
            var handleBrush = IsFullscreen ? ResourceBrush("AccentBrush", MediaColor.FromRgb(47, 111, 237)) : new SolidColorBrush(borderColour);
            var handlePen = new System.Windows.Media.Pen(System.Windows.Media.Brushes.White, IsFullscreen ? 1.5 : 1);
            foreach (var handle in HandlePoints(rectangle))
            {
                context.DrawRectangle(handleBrush, handlePen, new Rect(handle.X - handleSize / 2, handle.Y - handleSize / 2, handleSize, handleSize));
            }
        }
    }

    /// <summary>Oben rechts in jeder Zone: Groesse in Prozent und in Pixeln, live beim Ziehen.</summary>
    private void DrawDimensionBadge(DrawingContext context, Rect rectangle, ZoneDefinition zone)
    {
        var widthPercent = zone.Bounds.Width * 100;
        var heightPercent = zone.Bounds.Height * 100;
        var widthPixels = Math.Round(zone.Bounds.Width * MonitorPixelWidth);
        var heightPixels = Math.Round(zone.Bounds.Height * MonitorPixelHeight);
        var text = string.Create(
            CultureInfo.CurrentCulture,
            $"{widthPercent:0.#} × {heightPercent:0.#} % · {widthPixels:0} × {heightPixels:0} px");
        var badge = Mono(text, 13, System.Windows.Media.Brushes.White);
        var area = new Rect(rectangle.Right - badge.Width - 12 - 16, rectangle.Y + 12, badge.Width + 16, badge.Height + 8);
        if (area.X < rectangle.X + 8 || area.Bottom > rectangle.Bottom - 6)
        {
            return;
        }

        context.DrawRoundedRectangle(new SolidColorBrush(MediaColor.FromArgb(153, 0, 0, 0)), null, area, 4, 4);
        context.DrawText(badge, new System.Windows.Point(area.X + 8, area.Y + 4));
    }

    /// <summary>Beim Ziehen eines Griffs: das Mass der gezogenen Kante in Prozent und Pixeln neben dem Zeiger.</summary>
    private void DrawDragMeasurement(DrawingContext context, Rect screen)
    {
        if (!IsFullscreen || !IsMouseCaptured)
        {
            return;
        }

        string? text = null;
        if (draggedSharedDivider is not null && lastSharedBounds is not null)
        {
            var after = lastSharedBounds[draggedSharedDivider.AfterZone.Id];
            var value = draggedSharedDivider.Orientation == SharedDividerOrientation.Vertical ? after.X : after.Y;
            var pixels = draggedSharedDivider.Orientation == SharedDividerOrientation.Vertical
                ? value * MonitorPixelWidth
                : value * MonitorPixelHeight;
            text = string.Create(CultureInfo.CurrentCulture, $"{value * 100:0.#} % · {pixels:0} px");
        }
        else if (lastDragBounds is not null && resizeEdges != ZoneEdges.None)
        {
            var horizontal = resizeEdges.HasFlag(ZoneEdges.Left) || resizeEdges.HasFlag(ZoneEdges.Right);
            var value = horizontal ? lastDragBounds.Width : lastDragBounds.Height;
            var pixels = horizontal ? value * MonitorPixelWidth : value * MonitorPixelHeight;
            text = string.Create(CultureInfo.CurrentCulture, $"{value * 100:0.#} % · {pixels:0} px");
        }
        else if (lastDragBounds is not null)
        {
            text = string.Create(
                CultureInfo.CurrentCulture,
                $"{lastDragBounds.X * 100:0.#} % · {lastDragBounds.Y * 100:0.#} %");
        }

        if (text is null)
        {
            return;
        }

        var label = Mono(text, 13, System.Windows.Media.Brushes.White, FontWeights.SemiBold);
        var area = new Rect(lastPointer.X + 14, lastPointer.Y - label.Height - 14, label.Width + 14, label.Height + 8);
        if (area.Right > screen.Right)
        {
            area.X = lastPointer.X - area.Width - 14;
        }

        if (area.Y < screen.Top)
        {
            area.Y = lastPointer.Y + 14;
        }

        context.DrawRoundedRectangle(ResourceBrush("AccentBrush", MediaColor.FromRgb(47, 111, 237)), null, area, 4, 4);
        context.DrawText(label, new System.Windows.Point(area.X + 7, area.Y + 4));
    }

    private Rect GetMonitorRectangle()
    {
        if (IsFullscreen)
        {
            return new Rect(0, 0, Math.Max(1, ActualWidth), Math.Max(1, ActualHeight));
        }

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
        if (IsFullscreen)
        {
            return monitor;
        }

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

        var accent = ResourceBrush("AccentBrush", MediaColor.FromRgb(0, 120, 212)).Color;
        var haloBrush = new SolidColorBrush(accent) { Opacity = IsFullscreen ? 0.25 : 0.22 };
        var guideBrush = new SolidColorBrush(accent);
        var haloPen = new System.Windows.Media.Pen(haloBrush, IsFullscreen ? 10 : 7);
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
        var accent = ResourceBrush("AccentBrush", MediaColor.FromRgb(0, 120, 212)).Color;
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

    private FormattedText Text(string value, double size, System.Windows.Media.Brush brush, double maxWidth, FontWeight? weight = null) => new(
        value,
        CultureInfo.CurrentUICulture,
        System.Windows.FlowDirection.LeftToRight,
        new Typeface(new System.Windows.Media.FontFamily("Segoe UI Variable Text"), FontStyles.Normal, weight ?? FontWeights.Normal, FontStretches.Normal),
        size,
        brush,
        VisualTreeHelper.GetDpi(this).PixelsPerDip)
    {
        MaxTextWidth = Math.Max(1, maxWidth),
        Trimming = TextTrimming.CharacterEllipsis,
        MaxLineCount = 1
    };

    private FormattedText Mono(string value, double size, System.Windows.Media.Brush brush, FontWeight? weight = null) => new(
        value,
        CultureInfo.CurrentUICulture,
        System.Windows.FlowDirection.LeftToRight,
        new Typeface(new System.Windows.Media.FontFamily("Cascadia Mono"), FontStyles.Normal, weight ?? FontWeights.Normal, FontStretches.Normal),
        size,
        brush,
        VisualTreeHelper.GetDpi(this).PixelsPerDip);

    private SolidColorBrush ResourceBrush(string key, MediaColor fallback) =>
        TryFindResource(key) as SolidColorBrush ?? new SolidColorBrush(fallback);
}

public sealed class ZoneSelectedEventArgs(Guid zoneId) : EventArgs
{
    public Guid ZoneId { get; } = zoneId;
}

public sealed class ZoneContextMenuEventArgs(Guid zoneId, System.Windows.Point position) : EventArgs
{
    public Guid ZoneId { get; } = zoneId;
    public System.Windows.Point Position { get; } = position;
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
