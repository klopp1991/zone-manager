namespace SnapZones.Core.PartMonitors;

public sealed class PlacementHistory
{
    private readonly int maxDepth;
    private readonly Dictionary<WindowIdentity, List<WindowPlacementSnapshot>> entries = [];

    public PlacementHistory(int maxDepth = 30)
    {
        if (maxDepth < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDepth));
        }

        this.maxDepth = maxDepth;
    }

    public void Remember(WindowPlacementSnapshot snapshot)
    {
        if (!entries.TryGetValue(snapshot.Identity, out var history))
        {
            history = [];
            entries.Add(snapshot.Identity, history);
        }

        history.Add(snapshot);
        if (history.Count > maxDepth)
        {
            history.RemoveAt(0);
        }
    }

    public bool TryPeek(WindowIdentity identity, out WindowPlacementSnapshot snapshot)
    {
        if (entries.TryGetValue(identity, out var history) && history.Count > 0)
        {
            snapshot = history[^1];
            return true;
        }

        snapshot = null!;
        return false;
    }

    public bool DiscardTop(WindowIdentity identity)
    {
        if (!entries.TryGetValue(identity, out var history) || history.Count == 0)
        {
            return false;
        }

        history.RemoveAt(history.Count - 1);
        if (history.Count == 0)
        {
            entries.Remove(identity);
        }

        return true;
    }

    public void Remove(WindowIdentity identity) => entries.Remove(identity);
}
