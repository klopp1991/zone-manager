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

public static class ZoneMagnetism
{
    private const double MinimumSize = 0.04;

    public static NormalizedRect SnapMove(
        NormalizedRect moving,
        IReadOnlyList<NormalizedRect> otherZones,
        int thresholdPixels,
        int monitorWidth,
        int monitorHeight)
    {
        if (thresholdPixels <= 0)
        {
            return moving;
        }

        var xCandidates = new List<double> { 0, 1 - moving.Width };
        var yCandidates = new List<double> { 0, 1 - moving.Height };
        foreach (var other in otherZones)
        {
            xCandidates.Add(other.X + other.Width);
            xCandidates.Add(other.X - moving.Width);
            yCandidates.Add(other.Y + other.Height);
            yCandidates.Add(other.Y - moving.Height);
        }

        var x = Nearest(moving.X, xCandidates, thresholdPixels, monitorWidth);
        var y = Nearest(moving.Y, yCandidates, thresholdPixels, monitorHeight);
        return moving with
        {
            X = Math.Clamp(x, 0, 1 - moving.Width),
            Y = Math.Clamp(y, 0, 1 - moving.Height)
        };
    }

    public static NormalizedRect SnapResize(
        NormalizedRect resizing,
        IReadOnlyList<NormalizedRect> otherZones,
        ZoneEdges movingEdges,
        int thresholdPixels,
        int monitorWidth,
        int monitorHeight)
    {
        if (thresholdPixels <= 0 || movingEdges == ZoneEdges.None)
        {
            return resizing;
        }

        var horizontalEdges = otherZones.SelectMany(zone => new[] { zone.X, zone.X + zone.Width }).Append(0).Append(1).ToArray();
        var verticalEdges = otherZones.SelectMany(zone => new[] { zone.Y, zone.Y + zone.Height }).Append(0).Append(1).ToArray();
        var left = resizing.X;
        var top = resizing.Y;
        var right = resizing.X + resizing.Width;
        var bottom = resizing.Y + resizing.Height;

        if (movingEdges.HasFlag(ZoneEdges.Left))
        {
            left = Math.Min(Nearest(left, horizontalEdges, thresholdPixels, monitorWidth), right - MinimumSize);
        }
        if (movingEdges.HasFlag(ZoneEdges.Right))
        {
            right = Math.Max(Nearest(right, horizontalEdges, thresholdPixels, monitorWidth), left + MinimumSize);
        }
        if (movingEdges.HasFlag(ZoneEdges.Top))
        {
            top = Math.Min(Nearest(top, verticalEdges, thresholdPixels, monitorHeight), bottom - MinimumSize);
        }
        if (movingEdges.HasFlag(ZoneEdges.Bottom))
        {
            bottom = Math.Max(Nearest(bottom, verticalEdges, thresholdPixels, monitorHeight), top + MinimumSize);
        }

        left = Math.Clamp(left, 0, 1);
        top = Math.Clamp(top, 0, 1);
        right = Math.Clamp(right, 0, 1);
        bottom = Math.Clamp(bottom, 0, 1);
        return new NormalizedRect(left, top, right - left, bottom - top);
    }

    private static double Nearest(double current, IEnumerable<double> candidates, int thresholdPixels, int axisPixels)
    {
        var best = current;
        var bestDistance = double.MaxValue;
        foreach (var candidate in candidates)
        {
            var distance = Math.Abs(candidate - current) * Math.Max(1, axisPixels);
            if (distance <= thresholdPixels && distance < bestDistance)
            {
                best = candidate;
                bestDistance = distance;
            }
        }

        return best;
    }
}

