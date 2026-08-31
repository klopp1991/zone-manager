using Microsoft.Win32;

namespace ZoneManager.Windows.Displays;

public sealed record PhysicalMonitorSize(
    double PhysicalWidthCentimeters,
    double PhysicalHeightCentimeters);

public static class EdidPhysicalSizeReader
{
    public static PhysicalMonitorSize? Read(string monitorDevicePath)
    {
        var registryPath = RegistryPath(monitorDevicePath);
        if (registryPath is null)
        {
            return null;
        }

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(registryPath);
            return Decode(key?.GetValue("EDID") as byte[]);
        }
        catch (Exception exception) when (
            exception is System.IO.IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return null;
        }
    }

    public static PhysicalMonitorSize? Decode(byte[]? edid)
    {
        if (edid is null || edid.Length < 23 || edid[21] == 0 || edid[22] == 0)
        {
            return null;
        }

        return new PhysicalMonitorSize(edid[21], edid[22]);
    }

    private static string? RegistryPath(string monitorDevicePath)
    {
        const string marker = "DISPLAY#";
        var markerIndex = monitorDevicePath.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return null;
        }

        var components = monitorDevicePath[(markerIndex + marker.Length)..].Split('#');
        if (components.Length < 2 ||
            string.IsNullOrWhiteSpace(components[0]) ||
            string.IsNullOrWhiteSpace(components[1]))
        {
            return null;
        }

        return $@"SYSTEM\CurrentControlSet\Enum\DISPLAY\{components[0]}\{components[1]}\Device Parameters";
    }
}
