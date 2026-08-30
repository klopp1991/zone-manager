using SnapZones.Core.Models;

namespace SnapZones.Core.Geometry;

[Flags]
public enum ZoneEdges
{
    None = 0,
    Left = 1,
    Top = 2,
    Right = 4,
    Bottom = 8
}

public readonly record struct ZoneSnapResult(NormalizedRect Bounds, ZoneEdges SnappedEdges);

public static class ZoneMagnetism
{
    private const double MinimumSize = 0.04;

    public static NormalizedRect SnapMove(
        NormalizedRect moving,
        IReadOnlyList<NormalizedRect> otherZones,
        int thresholdPixels,
        int monitorWidth,
        int monitorHeight) =>
        SnapMoveWithResult(moving, otherZones, thresholdPixels, monitorWidth, monitorHeight).Bounds;

    public static ZoneSnapResult SnapMoveWithResult(
        NormalizedRect moving,
        IReadOnlyList<NormalizedRect> otherZones,
        int thresholdPixels,
        int monitorWidth,
        int monitorHeight)
    {
        if (thresholdPixels <= 0)
        {
            return new ZoneSnapResult(moving, ZoneEdges.None);
        }

        var xCandidates = new List<SnapCandidate>
        {
            new(0, ZoneEdges.Left),
            new(1 - moving.Width, ZoneEdges.Right)
        };
        var yCandidates = new List<SnapCandidate>
        {
            new(0, ZoneEdges.Top),
            new(1 - moving.Height, ZoneEdges.Bottom)
        };
        foreach (var other in otherZones)
        {
            xCandidates.Add(new SnapCandidate(other.X + other.Width, ZoneEdges.Left));
            xCandidates.Add(new SnapCandidate(other.X - moving.Width, ZoneEdges.Right));
            yCandidates.Add(new SnapCandidate(other.Y + other.Height, ZoneEdges.Top));
            yCandidates.Add(new SnapCandidate(other.Y - moving.Height, ZoneEdges.Bottom));
        }

        var x = Nearest(moving.X, xCandidates, thresholdPixels, monitorWidth);
        var y = Nearest(moving.Y, yCandidates, thresholdPixels, monitorHeight);
        var bounds = moving with
        {
            X = Math.Clamp(x.Value, 0, 1 - moving.Width),
            Y = Math.Clamp(y.Value, 0, 1 - moving.Height)
        };
        return new ZoneSnapResult(bounds, x.Edge | y.Edge);
    }

    public static NormalizedRect SnapResize(
        NormalizedRect resizing,
        IReadOnlyList<NormalizedRect> otherZones,
        ZoneEdges movingEdges,
        int thresholdPixels,
        int monitorWidth,
        int monitorHeight) =>
        SnapResizeWithResult(
            resizing,
            otherZones,
            movingEdges,
            thresholdPixels,
            monitorWidth,
            monitorHeight).Bounds;

    public static ZoneSnapResult SnapResizeWithResult(
        NormalizedRect resizing,
        IReadOnlyList<NormalizedRect> otherZones,
        ZoneEdges movingEdges,
        int thresholdPixels,
        int monitorWidth,
        int monitorHeight)
    {
        if (thresholdPixels <= 0 || movingEdges == ZoneEdges.None)
        {
            return new ZoneSnapResult(resizing, ZoneEdges.None);
        }

        var horizontalEdges = otherZones
            .SelectMany(zone => new[] { zone.X, zone.X + zone.Width })
            .Append(0)
            .Append(1)
            .Select(value => new SnapCandidate(value, ZoneEdges.None))
            .ToArray();
        var verticalEdges = otherZones
            .SelectMany(zone => new[] { zone.Y, zone.Y + zone.Height })
            .Append(0)
            .Append(1)
            .Select(value => new SnapCandidate(value, ZoneEdges.None))
            .ToArray();
        var left = resizing.X;
        var top = resizing.Y;
        var right = resizing.X + resizing.Width;
        var bottom = resizing.Y + resizing.Height;
        var snappedEdges = ZoneEdges.None;

        if (movingEdges.HasFlag(ZoneEdges.Left))
        {
            var snap = Nearest(left, horizontalEdges, thresholdPixels, monitorWidth);
            left = Math.Min(snap.Value, right - MinimumSize);
            if (snap.Snapped && NearlyEqual(left, snap.Value)) snappedEdges |= ZoneEdges.Left;
        }
        if (movingEdges.HasFlag(ZoneEdges.Right))
        {
            var snap = Nearest(right, horizontalEdges, thresholdPixels, monitorWidth);
            right = Math.Max(snap.Value, left + MinimumSize);
            if (snap.Snapped && NearlyEqual(right, snap.Value)) snappedEdges |= ZoneEdges.Right;
        }
        if (movingEdges.HasFlag(ZoneEdges.Top))
        {
            var snap = Nearest(top, verticalEdges, thresholdPixels, monitorHeight);
            top = Math.Min(snap.Value, bottom - MinimumSize);
            if (snap.Snapped && NearlyEqual(top, snap.Value)) snappedEdges |= ZoneEdges.Top;
        }
        if (movingEdges.HasFlag(ZoneEdges.Bottom))
        {
            var snap = Nearest(bottom, verticalEdges, thresholdPixels, monitorHeight);
            bottom = Math.Max(snap.Value, top + MinimumSize);
            if (snap.Snapped && NearlyEqual(bottom, snap.Value)) snappedEdges |= ZoneEdges.Bottom;
        }

        left = Math.Clamp(left, 0, 1);
        top = Math.Clamp(top, 0, 1);
        right = Math.Clamp(right, 0, 1);
        bottom = Math.Clamp(bottom, 0, 1);
        return new ZoneSnapResult(
            new NormalizedRect(left, top, right - left, bottom - top),
            snappedEdges);
    }

    private static SnapMatch Nearest(
        double current,
        IEnumerable<SnapCandidate> candidates,
        int thresholdPixels,
        int axisPixels)
    {
        var best = new SnapMatch(current, ZoneEdges.None, false);
        var bestDistance = double.MaxValue;
        foreach (var candidate in candidates)
        {
            var distance = Math.Abs(candidate.Value - current) * Math.Max(1, axisPixels);
            if (distance <= thresholdPixels && distance < bestDistance)
            {
                best = new SnapMatch(candidate.Value, candidate.Edge, true);
                bestDistance = distance;
            }
        }

        return best;
    }

    private static bool NearlyEqual(double first, double second) => Math.Abs(first - second) <= 0.0000001;

    private readonly record struct SnapCandidate(double Value, ZoneEdges Edge);
    private readonly record struct SnapMatch(double Value, ZoneEdges Edge, bool Snapped);
}
