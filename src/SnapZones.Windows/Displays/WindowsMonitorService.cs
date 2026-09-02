using System.ComponentModel;
using System.Runtime.InteropServices;
using SnapZones.Core.Geometry;
using SnapZones.Core.Models;
using SnapZones.Core.Monitors;
using SnapZones.Windows.Native;

namespace SnapZones.Windows.Displays;

public sealed class WindowsMonitorService : IMonitorService
{
    public IReadOnlyList<LiveMonitor> GetMonitors()
    {
        var monitors = new List<LiveMonitor>();
        var displayPaths = DisplayPathIdentityProvider.GetActiveIdentities();
        User32.MonitorEnumProc callback = (monitor, _, _, _) =>
        {
            monitors.Add(ReadMonitor(monitor, displayPaths));
            return true;
        };

        if (!User32.EnumDisplayMonitors(0, 0, callback, 0))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Die Monitore konnten nicht gelesen werden.");
        }

        return monitors;
    }

    private static LiveMonitor ReadMonitor(
        nint monitor,
        IReadOnlyDictionary<string, DisplayPathIdentity> displayPaths)
    {
        var info = new MonitorInfoEx
        {
            Size = (uint)Marshal.SizeOf<MonitorInfoEx>(),
            DeviceName = string.Empty
        };
        if (!User32.GetMonitorInfo(monitor, ref info))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Monitorinformationen konnten nicht gelesen werden.");
        }

        var device = new DisplayDevice
        {
            Size = Marshal.SizeOf<DisplayDevice>(),
            DeviceName = string.Empty,
            DeviceString = string.Empty,
            DeviceId = string.Empty,
            DeviceKey = string.Empty
        };
        var hasDevice = User32.EnumDisplayDevices(info.DeviceName, 0, ref device, User32.GetDeviceInterfaceName);
        var stableId = hasDevice
            ? string.Join('|', new[] { device.DeviceId, device.DeviceKey }.Where(value => !string.IsNullOrWhiteSpace(value)))
            : string.Empty;
        if (string.IsNullOrWhiteSpace(stableId))
        {
            stableId = info.DeviceName;
        }

        var friendlyName = hasDevice && !string.IsNullOrWhiteSpace(device.DeviceString)
            ? device.DeviceString
            : info.DeviceName;
        var dpiX = 96u;
        var dpiY = 96u;
        if (Shcore.GetDpiForMonitor(monitor, 0, out var detectedX, out var detectedY) == 0)
        {
            dpiX = Math.Max(96u, detectedX);
            dpiY = Math.Max(96u, detectedY);
        }

        var workArea = new MonitorWorkArea(
            info.Work.Left,
            info.Work.Top,
            info.Work.Right - info.Work.Left,
            info.Work.Bottom - info.Work.Top);
        var identity = DisplayPathIdentity.Resolve(
            info.DeviceName,
            stableId,
            friendlyName,
            displayPaths);
        displayPaths.TryGetValue(info.DeviceName, out var displayPath);
        var bounds = new PixelRect(
            info.Monitor.Left,
            info.Monitor.Top,
            info.Monitor.Right - info.Monitor.Left,
            info.Monitor.Bottom - info.Monitor.Top);
        return new LiveMonitor(
            identity,
            workArea,
            dpiX,
            dpiY,
            (info.Flags & User32.MonitorInfoPrimary) != 0,
            displayPath?.PhysicalWidthCentimeters,
            displayPath?.PhysicalHeightCentimeters,
            bounds);
    }
}
