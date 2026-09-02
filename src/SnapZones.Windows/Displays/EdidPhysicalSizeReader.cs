namespace SnapZones.Windows.Displays;

public sealed record PhysicalMonitorSize(
    double PhysicalWidthCentimeters,
    double PhysicalHeightCentimeters);

/// <summary>Die Bildflaeche aus der EDID. Das vollstaendige Lesen uebernimmt <see cref="EdidReader"/>.</summary>
public static class EdidPhysicalSizeReader
{
    public static PhysicalMonitorSize? Read(string monitorDevicePath) => EdidReader.Read(monitorDevicePath)?.PhysicalSize;

    public static PhysicalMonitorSize? Decode(byte[]? edid) => DecodeSize(edid);

    internal static PhysicalMonitorSize? DecodeSize(byte[]? edid)
    {
        if (edid is null || edid.Length < 23 || edid[21] == 0 || edid[22] == 0)
        {
            return null;
        }

        return new PhysicalMonitorSize(edid[21], edid[22]);
    }
}
