using SnapZones.Core.Models;

namespace SnapZones.Core.Monitors;

public static class MonitorMatcher
{
    public static IReadOnlyList<MonitorMatch> Match(
        IReadOnlyList<MonitorLayout> savedLayouts,
        IReadOnlyList<LiveMonitor> liveMonitors)
    {
        var available = new HashSet<int>(Enumerable.Range(0, liveMonitors.Count));
        var matches = new MonitorMatch?[savedLayouts.Count];

        MatchStage(MonitorMatchQuality.StableId, (saved, live) =>
            EqualsIgnoreCase(saved.Monitor.StableId, live.Identity.StableId));
        MatchStage(MonitorMatchQuality.DeviceName, (saved, live) =>
            EqualsIgnoreCase(saved.Monitor.DeviceName, live.Identity.DeviceName));
        MatchStage(MonitorMatchQuality.Resolution, (saved, live) =>
            saved.SavedWidth == live.WorkArea.Width && saved.SavedHeight == live.WorkArea.Height);
        MatchStage(MonitorMatchQuality.PrimaryFallback, (_, live) => live.IsPrimary);

        for (var index = 0; index < matches.Length; index++)
        {
            matches[index] ??= new MonitorMatch(savedLayouts[index], null, MonitorMatchQuality.Missing);
        }

        return matches.Select(match => match!).ToArray();

        void MatchStage(
            MonitorMatchQuality quality,
            Func<MonitorLayout, LiveMonitor, bool> predicate)
        {
            for (var savedIndex = 0; savedIndex < savedLayouts.Count; savedIndex++)
            {
                if (matches[savedIndex] is not null)
                {
                    continue;
                }

                var liveIndex = available
                    .OrderBy(index => liveMonitors[index].IsPrimary ? 0 : 1)
                    .FirstOrDefault(index => predicate(savedLayouts[savedIndex], liveMonitors[index]), -1);
                if (liveIndex < 0)
                {
                    continue;
                }

                matches[savedIndex] = new MonitorMatch(savedLayouts[savedIndex], liveMonitors[liveIndex], quality);
                available.Remove(liveIndex);
            }
        }
    }

    private static bool EqualsIgnoreCase(string first, string second) =>
        !string.IsNullOrWhiteSpace(first) &&
        string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
}
