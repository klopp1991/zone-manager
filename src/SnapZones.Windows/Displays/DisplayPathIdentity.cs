using SnapZones.Core.Models;
using SnapZones.Core.Monitors;

namespace SnapZones.Windows.Displays;

public sealed record DisplayPathIdentity(
    string GdiDeviceName,
    string StableId,
    string FriendlyName,
    double? PhysicalWidthCentimeters = null,
    double? PhysicalHeightCentimeters = null,
    string HardwareId = "")
{
    public static MonitorIdentity Resolve(
        string gdiDeviceName,
        string fallbackStableId,
        string fallbackFriendlyName,
        IReadOnlyDictionary<string, DisplayPathIdentity> activePaths)
    {
        if (activePaths.TryGetValue(gdiDeviceName, out var path))
        {
            var stableId = string.IsNullOrWhiteSpace(path.StableId) ? fallbackStableId : path.StableId;
            return new MonitorIdentity(
                stableId,
                gdiDeviceName,
                string.IsNullOrWhiteSpace(path.FriendlyName) ? fallbackFriendlyName : path.FriendlyName,
                string.IsNullOrWhiteSpace(path.HardwareId) ? MonitorHardwareId.FromDevicePath(stableId) : path.HardwareId);
        }

        return new MonitorIdentity(
            fallbackStableId,
            gdiDeviceName,
            fallbackFriendlyName,
            MonitorHardwareId.FromDevicePath(fallbackStableId));
    }
}
