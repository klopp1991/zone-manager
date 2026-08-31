using System.Runtime.InteropServices;
using ZoneManager.Windows.Native;

namespace ZoneManager.Windows.Displays;

internal static class DisplayPathIdentityProvider
{
    private const uint QueryOnlyActivePaths = 0x00000002;
    private const int ErrorSuccess = 0;
    private const int ErrorInsufficientBuffer = 122;

    public static IReadOnlyDictionary<string, DisplayPathIdentity> GetActiveIdentities()
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var result = User32.GetDisplayConfigBufferSizes(QueryOnlyActivePaths, out var pathCount, out var modeCount);
            if (result != ErrorSuccess)
            {
                return new Dictionary<string, DisplayPathIdentity>(StringComparer.OrdinalIgnoreCase);
            }

            var paths = new DisplayConfigPathInfo[pathCount];
            var modes = new DisplayConfigModeInfo[modeCount];
            result = User32.QueryDisplayConfig(
                QueryOnlyActivePaths,
                ref pathCount,
                paths,
                ref modeCount,
                modes,
                0);
            if (result == ErrorInsufficientBuffer)
            {
                continue;
            }
            if (result != ErrorSuccess)
            {
                return new Dictionary<string, DisplayPathIdentity>(StringComparer.OrdinalIgnoreCase);
            }

            return ReadIdentities(paths.Take((int)pathCount));
        }

        return new Dictionary<string, DisplayPathIdentity>(StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<string, DisplayPathIdentity> ReadIdentities(
        IEnumerable<DisplayConfigPathInfo> paths)
    {
        var result = new Dictionary<string, DisplayPathIdentity>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            var source = new DisplayConfigSourceDeviceName
            {
                Header = new DisplayConfigDeviceInfoHeader
                {
                    Type = 1,
                    Size = (uint)Marshal.SizeOf<DisplayConfigSourceDeviceName>(),
                    AdapterId = path.SourceInfo.AdapterId,
                    Id = path.SourceInfo.Id
                },
                ViewGdiDeviceName = string.Empty
            };
            var target = new DisplayConfigTargetDeviceName
            {
                Header = new DisplayConfigDeviceInfoHeader
                {
                    Type = 2,
                    Size = (uint)Marshal.SizeOf<DisplayConfigTargetDeviceName>(),
                    AdapterId = path.TargetInfo.AdapterId,
                    Id = path.TargetInfo.Id
                },
                MonitorFriendlyDeviceName = string.Empty,
                MonitorDevicePath = string.Empty
            };

            if (User32.DisplayConfigGetSourceDeviceInfo(ref source) != ErrorSuccess ||
                User32.DisplayConfigGetTargetDeviceInfo(ref target) != ErrorSuccess ||
                string.IsNullOrWhiteSpace(source.ViewGdiDeviceName))
            {
                continue;
            }

            var physicalSize = EdidPhysicalSizeReader.Read(target.MonitorDevicePath);
            result[source.ViewGdiDeviceName] = new DisplayPathIdentity(
                source.ViewGdiDeviceName,
                target.MonitorDevicePath,
                target.MonitorFriendlyDeviceName,
                physicalSize?.PhysicalWidthCentimeters,
                physicalSize?.PhysicalHeightCentimeters);
        }

        return result;
    }
}
