using ZoneManager.Core.Models;

namespace ZoneManager.Windows.Displays;

public sealed record DisplayPathIdentity(
    string GdiDeviceName,
    string StableId,
    string FriendlyName,
    double? PhysicalWidthCentimeters = null,
    double? PhysicalHeightCentimeters = null)
{
    public static MonitorIdentity Resolve(
        string gdiDeviceName,
        string fallbackStableId,
        string fallbackFriendlyName,
        IReadOnlyDictionary<string, DisplayPathIdentity> activePaths)
    {
        if (activePaths.TryGetValue(gdiDeviceName, out var path))
        {
            return new MonitorIdentity(
                string.IsNullOrWhiteSpace(path.StableId) ? fallbackStableId : path.StableId,
                gdiDeviceName,
                string.IsNullOrWhiteSpace(path.FriendlyName) ? fallbackFriendlyName : path.FriendlyName);
        }

        return new MonitorIdentity(fallbackStableId, gdiDeviceName, fallbackFriendlyName);
    }
}
