using ZoneManager.Core.Models;

namespace ZoneManager.Core.Geometry;

public static class LargestFreeRectangle
{
    private const double MinimumSize = 0.04;
    private const double Epsilon = 0.0000001;

    public static NormalizedRect? Find(IReadOnlyList<NormalizedRect> occupied)
    {
        var xEdges = Edges(occupied.SelectMany(bounds => new[] { bounds.X, bounds.X + bounds.Width }));
        var yEdges = Edges(occupied.SelectMany(bounds => new[] { bounds.Y, bounds.Y + bounds.Height }));
        NormalizedRect? best = null;
        var bestArea = 0d;

        for (var leftIndex = 0; leftIndex < xEdges.Count - 1; leftIndex++)
        {
            for (var rightIndex = leftIndex + 1; rightIndex < xEdges.Count; rightIndex++)
            {
                for (var topIndex = 0; topIndex < yEdges.Count - 1; topIndex++)
                {
                    for (var bottomIndex = topIndex + 1; bottomIndex < yEdges.Count; bottomIndex++)
                    {
                        var candidate = new NormalizedRect(
                            xEdges[leftIndex],
                            yEdges[topIndex],
                            xEdges[rightIndex] - xEdges[leftIndex],
                            yEdges[bottomIndex] - yEdges[topIndex]);
                        if (candidate.Width < MinimumSize || candidate.Height < MinimumSize ||
                            occupied.Any(bounds => Overlaps(candidate, bounds)))
                        {
                            continue;
                        }

                        var area = candidate.Width * candidate.Height;
                        if (area > bestArea + Epsilon ||
                            Math.Abs(area - bestArea) <= Epsilon && IsEarlier(candidate, best))
                        {
                            best = candidate;
                            bestArea = area;
                        }
                    }
                }
            }
        }

        return best;
    }

    private static List<double> Edges(IEnumerable<double> occupiedEdges) =>
        occupiedEdges
            .Append(0)
            .Append(1)
            .Select(value => Math.Clamp(value, 0, 1))
            .Distinct()
            .Order()
            .ToList();

    private static bool Overlaps(NormalizedRect first, NormalizedRect second) =>
        first.X < second.X + second.Width - Epsilon &&
        first.X + first.Width > second.X + Epsilon &&
        first.Y < second.Y + second.Height - Epsilon &&
        first.Y + first.Height > second.Y + Epsilon;

    private static bool IsEarlier(NormalizedRect candidate, NormalizedRect? current) =>
        current is null ||
        candidate.Y < current.Y - Epsilon ||
        Math.Abs(candidate.Y - current.Y) <= Epsilon && candidate.X < current.X - Epsilon;
}

