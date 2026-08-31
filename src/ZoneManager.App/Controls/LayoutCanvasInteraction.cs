using System.Windows;
using ZoneManager.Core.Geometry;
using ZoneManager.Core.Models;
using Point = System.Windows.Point;

namespace ZoneManager.App.Controls;

public readonly record struct SnapGuide(Point Start, Point End);
public readonly record struct SharedDividerVisual(SnapGuide Line, Rect Handle);

public enum SharedDividerOrientation
{
    Vertical,
    Horizontal
}

public sealed record SharedZoneDivider(
    ZoneDefinition BeforeZone,
    ZoneDefinition AfterZone,
    SharedDividerOrientation Orientation,
    double Boundary,
    double SegmentStart,
    double SegmentEnd);

public static class LayoutCanvasInteraction
{
    public const double ResizeHandleTolerance = 12;
    private const double MinimumZoneSize = 0.04;
    private const double SharedBoundaryEpsilon = 0.0000001;

    public static ZoneDefinition? HitTestZone(
        IReadOnlyList<ZoneDefinition> zones,
        Guid? selectedZoneId,
        Rect screen,
        Point point)
    {
        var selectedZone = selectedZoneId is { } id
            ? zones.FirstOrDefault(zone => zone.Id == id)
            : null;
        if (selectedZone is not null &&
            ContainsWithTolerance(ToCanvasRect(selectedZone.Bounds, screen), point, ResizeHandleTolerance))
        {
            return selectedZone;
        }

        return zones
            .Reverse()
            .FirstOrDefault(zone => ToCanvasRect(zone.Bounds, screen).Contains(point));
    }

    public static ZoneEdges DetectResizeEdges(Rect rectangle, Point point)
    {
        var edges = ZoneEdges.None;
        var leftDistance = Math.Abs(point.X - rectangle.Left);
        var rightDistance = Math.Abs(point.X - rectangle.Right);
        var topDistance = Math.Abs(point.Y - rectangle.Top);
        var bottomDistance = Math.Abs(point.Y - rectangle.Bottom);
        if (leftDistance <= ResizeHandleTolerance || rightDistance <= ResizeHandleTolerance)
        {
            edges |= leftDistance <= rightDistance ? ZoneEdges.Left : ZoneEdges.Right;
        }
        if (topDistance <= ResizeHandleTolerance || bottomDistance <= ResizeHandleTolerance)
        {
            edges |= topDistance <= bottomDistance ? ZoneEdges.Top : ZoneEdges.Bottom;
        }
        return edges;
    }

    public static SharedZoneDivider? FindSharedDivider(
        IReadOnlyList<ZoneDefinition> zones,
        Rect screen,
        Point point)
    {
        var candidates = new List<(SharedZoneDivider Divider, double Distance)>();
        for (var firstIndex = 0; firstIndex < zones.Count; firstIndex++)
        {
            for (var secondIndex = firstIndex + 1; secondIndex < zones.Count; secondIndex++)
            {
                AddSharedDividerCandidates(zones[firstIndex], zones[secondIndex], screen, point, candidates);
            }
        }

        return candidates
            .OrderBy(candidate => candidate.Distance)
            .ThenByDescending(candidate => candidate.Divider.SegmentEnd - candidate.Divider.SegmentStart)
            .Select(candidate => candidate.Divider)
            .FirstOrDefault();
    }

    public static IReadOnlyDictionary<Guid, NormalizedRect> ResizeSharedDivider(
        SharedZoneDivider divider,
        double delta)
    {
        var before = divider.BeforeZone.Bounds;
        var after = divider.AfterZone.Bounds;
        if (divider.Orientation == SharedDividerOrientation.Vertical)
        {
            var afterRight = after.X + after.Width;
            var boundary = Math.Clamp(
                divider.Boundary + delta,
                before.X + MinimumZoneSize,
                afterRight - MinimumZoneSize);
            return new Dictionary<Guid, NormalizedRect>
            {
                [divider.BeforeZone.Id] = before with { Width = boundary - before.X },
                [divider.AfterZone.Id] = after with { X = boundary, Width = afterRight - boundary }
            };
        }

        var afterBottom = after.Y + after.Height;
        var horizontalBoundary = Math.Clamp(
            divider.Boundary + delta,
            before.Y + MinimumZoneSize,
            afterBottom - MinimumZoneSize);
        return new Dictionary<Guid, NormalizedRect>
        {
            [divider.BeforeZone.Id] = before with { Height = horizontalBoundary - before.Y },
            [divider.AfterZone.Id] = after with { Y = horizontalBoundary, Height = afterBottom - horizontalBoundary }
        };
    }

    public static SharedDividerVisual GetSharedDividerVisual(
        SharedZoneDivider divider,
        Rect screen,
        Point pointer)
    {
        const double handleThickness = 14;
        const double handleLength = 40;
        if (divider.Orientation == SharedDividerOrientation.Vertical)
        {
            var boundary = screen.X + (divider.Boundary * screen.Width);
            var segmentStart = screen.Y + (divider.SegmentStart * screen.Height);
            var segmentEnd = screen.Y + (divider.SegmentEnd * screen.Height);
            var actualLength = Math.Min(handleLength, segmentEnd - segmentStart);
            var centre = Math.Clamp(pointer.Y, segmentStart + (actualLength / 2), segmentEnd - (actualLength / 2));
            return new SharedDividerVisual(
                new SnapGuide(new Point(boundary, segmentStart), new Point(boundary, segmentEnd)),
                new Rect(
                    boundary - (handleThickness / 2),
                    centre - (actualLength / 2),
                    handleThickness,
                    actualLength));
        }

        var horizontalBoundary = screen.Y + (divider.Boundary * screen.Height);
        var horizontalStart = screen.X + (divider.SegmentStart * screen.Width);
        var horizontalEnd = screen.X + (divider.SegmentEnd * screen.Width);
        var horizontalLength = Math.Min(handleLength, horizontalEnd - horizontalStart);
        var horizontalCentre = Math.Clamp(
            pointer.X,
            horizontalStart + (horizontalLength / 2),
            horizontalEnd - (horizontalLength / 2));
        return new SharedDividerVisual(
            new SnapGuide(
                new Point(horizontalStart, horizontalBoundary),
                new Point(horizontalEnd, horizontalBoundary)),
            new Rect(
                horizontalCentre - (horizontalLength / 2),
                horizontalBoundary - (handleThickness / 2),
                horizontalLength,
                handleThickness));
    }

    public static NormalizedRect Transform(
        NormalizedRect original,
        double deltaX,
        double deltaY,
        ZoneEdges resizeEdges)
    {
        if (resizeEdges == ZoneEdges.None)
        {
            return new NormalizedRect(
                Math.Clamp(original.X + deltaX, 0, 1 - original.Width),
                Math.Clamp(original.Y + deltaY, 0, 1 - original.Height),
                original.Width,
                original.Height);
        }

        var left = original.X;
        var top = original.Y;
        var right = original.X + original.Width;
        var bottom = original.Y + original.Height;
        if (resizeEdges.HasFlag(ZoneEdges.Left))
        {
            left = Math.Clamp(left + deltaX, 0, right - MinimumZoneSize);
        }
        if (resizeEdges.HasFlag(ZoneEdges.Right))
        {
            right = Math.Clamp(right + deltaX, left + MinimumZoneSize, 1);
        }
        if (resizeEdges.HasFlag(ZoneEdges.Top))
        {
            top = Math.Clamp(top + deltaY, 0, bottom - MinimumZoneSize);
        }
        if (resizeEdges.HasFlag(ZoneEdges.Bottom))
        {
            bottom = Math.Clamp(bottom + deltaY, top + MinimumZoneSize, 1);
        }
        return new NormalizedRect(left, top, right - left, bottom - top);
    }

    public static ZoneSnapResult ApplyDrag(
        NormalizedRect original,
        double deltaX,
        double deltaY,
        ZoneEdges resizeEdges,
        IReadOnlyList<NormalizedRect> otherZones,
        int thresholdPixels,
        int monitorWidth,
        int monitorHeight,
        bool pauseMagnetism)
    {
        var transformed = Transform(original, deltaX, deltaY, resizeEdges);
        if (pauseMagnetism)
        {
            return new ZoneSnapResult(transformed, ZoneEdges.None);
        }

        return resizeEdges == ZoneEdges.None
            ? ZoneMagnetism.SnapMoveWithResult(
                transformed,
                otherZones,
                thresholdPixels,
                monitorWidth,
                monitorHeight)
            : ZoneMagnetism.SnapResizeWithResult(
                transformed,
                otherZones,
                resizeEdges,
                thresholdPixels,
                monitorWidth,
                monitorHeight);
    }

    public static IReadOnlyList<SnapGuide> GetSnapGuides(
        NormalizedRect bounds,
        Rect screen,
        ZoneEdges snappedEdges)
    {
        if (snappedEdges == ZoneEdges.None)
        {
            return [];
        }

        var rectangle = ToCanvasRect(bounds, screen);
        var guides = new List<SnapGuide>(4);
        if (snappedEdges.HasFlag(ZoneEdges.Left))
        {
            guides.Add(new SnapGuide(
                new Point(rectangle.Left, screen.Top),
                new Point(rectangle.Left, screen.Bottom)));
        }
        if (snappedEdges.HasFlag(ZoneEdges.Right))
        {
            guides.Add(new SnapGuide(
                new Point(rectangle.Right, screen.Top),
                new Point(rectangle.Right, screen.Bottom)));
        }
        if (snappedEdges.HasFlag(ZoneEdges.Top))
        {
            guides.Add(new SnapGuide(
                new Point(screen.Left, rectangle.Top),
                new Point(screen.Right, rectangle.Top)));
        }
        if (snappedEdges.HasFlag(ZoneEdges.Bottom))
        {
            guides.Add(new SnapGuide(
                new Point(screen.Left, rectangle.Bottom),
                new Point(screen.Right, rectangle.Bottom)));
        }
        return guides;
    }

    public static Rect ToCanvasRect(NormalizedRect bounds, Rect screen) => new(
        screen.X + (bounds.X * screen.Width),
        screen.Y + (bounds.Y * screen.Height),
        bounds.Width * screen.Width,
        bounds.Height * screen.Height);

    private static void AddSharedDividerCandidates(
        ZoneDefinition first,
        ZoneDefinition second,
        Rect screen,
        Point point,
        ICollection<(SharedZoneDivider Divider, double Distance)> candidates)
    {
        AddVerticalCandidate(first, second, screen, point, candidates);
        AddVerticalCandidate(second, first, screen, point, candidates);
        AddHorizontalCandidate(first, second, screen, point, candidates);
        AddHorizontalCandidate(second, first, screen, point, candidates);
    }

    private static void AddVerticalCandidate(
        ZoneDefinition left,
        ZoneDefinition right,
        Rect screen,
        Point point,
        ICollection<(SharedZoneDivider Divider, double Distance)> candidates)
    {
        var boundary = left.Bounds.X + left.Bounds.Width;
        if (!NearlyEqual(boundary, right.Bounds.X))
        {
            return;
        }

        var segmentStart = Math.Max(left.Bounds.Y, right.Bounds.Y);
        var segmentEnd = Math.Min(left.Bounds.Y + left.Bounds.Height, right.Bounds.Y + right.Bounds.Height);
        var canvasBoundary = screen.X + (boundary * screen.Width);
        var canvasStart = screen.Y + (segmentStart * screen.Height);
        var canvasEnd = screen.Y + (segmentEnd * screen.Height);
        var distance = Math.Abs(point.X - canvasBoundary);
        if (segmentEnd - segmentStart <= SharedBoundaryEpsilon ||
            distance > ResizeHandleTolerance ||
            point.Y < canvasStart ||
            point.Y > canvasEnd)
        {
            return;
        }

        candidates.Add((
            new SharedZoneDivider(
                left,
                right,
                SharedDividerOrientation.Vertical,
                boundary,
                segmentStart,
                segmentEnd),
            distance));
    }

    private static void AddHorizontalCandidate(
        ZoneDefinition top,
        ZoneDefinition bottom,
        Rect screen,
        Point point,
        ICollection<(SharedZoneDivider Divider, double Distance)> candidates)
    {
        var boundary = top.Bounds.Y + top.Bounds.Height;
        if (!NearlyEqual(boundary, bottom.Bounds.Y))
        {
            return;
        }

        var segmentStart = Math.Max(top.Bounds.X, bottom.Bounds.X);
        var segmentEnd = Math.Min(top.Bounds.X + top.Bounds.Width, bottom.Bounds.X + bottom.Bounds.Width);
        var canvasBoundary = screen.Y + (boundary * screen.Height);
        var canvasStart = screen.X + (segmentStart * screen.Width);
        var canvasEnd = screen.X + (segmentEnd * screen.Width);
        var distance = Math.Abs(point.Y - canvasBoundary);
        if (segmentEnd - segmentStart <= SharedBoundaryEpsilon ||
            distance > ResizeHandleTolerance ||
            point.X < canvasStart ||
            point.X > canvasEnd)
        {
            return;
        }

        candidates.Add((
            new SharedZoneDivider(
                top,
                bottom,
                SharedDividerOrientation.Horizontal,
                boundary,
                segmentStart,
                segmentEnd),
            distance));
    }

    private static bool NearlyEqual(double first, double second) =>
        Math.Abs(first - second) <= SharedBoundaryEpsilon;

    private static bool ContainsWithTolerance(Rect rectangle, Point point, double tolerance)
    {
        rectangle.Inflate(tolerance, tolerance);
        return rectangle.Contains(point);
    }
}
