using System.Text;
using Microsoft.Win32;

namespace SnapZones.Windows.Displays;

/// <summary>Was aus der EDID eines Monitors gebraucht wird.</summary>
/// <param name="ProductCode">Hersteller und Modell, etwa <c>GSM9EB9</c>; entspricht dem Segment im Anzeigepfad.</param>
/// <param name="SerialNumber">Die Seriennummer aus dem Beschreibungsblock, sofern der Hersteller eine eintraegt.</param>
/// <param name="PhysicalSize">Die Bildflaeche in Zentimetern, sofern angegeben.</param>
public sealed record EdidInfo(string? ProductCode, string? SerialNumber, PhysicalMonitorSize? PhysicalSize);

/// <summary>
/// Liest die EDID eines Monitors aus der Registrierung und entnimmt Modell, Seriennummer und Bildflaeche.
/// Die Seriennummer ist der Schluessel, um denselben Monitor nach einem Umstecken wiederzuerkennen.
/// </summary>
public static class EdidReader
{
    private const int DescriptorStart = 54;
    private const int DescriptorLength = 18;
    private const int DescriptorCount = 4;
    private const byte SerialDescriptorTag = 0xFF;

    public static EdidInfo? Read(string monitorDevicePath)
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

    public static EdidInfo? Decode(byte[]? edid)
    {
        if (edid is null || edid.Length < 128)
        {
            return edid is { Length: >= 23 }
                ? new EdidInfo(null, null, EdidPhysicalSizeReader.DecodeSize(edid))
                : null;
        }

        return new EdidInfo(DecodeProductCode(edid), DecodeSerialNumber(edid), EdidPhysicalSizeReader.DecodeSize(edid));
    }

    /// <summary>
    /// Bytes 8 und 9 tragen drei Buchstaben des Herstellers in je fuenf Bit, Bytes 10 und 11 den
    /// Modellcode; das Ergebnis ist dieselbe Schreibweise, die Windows im Anzeigepfad verwendet.
    /// </summary>
    private static string? DecodeProductCode(byte[] edid)
    {
        var packed = (edid[8] << 8) | edid[9];
        var first = (packed >> 10) & 0x1F;
        var second = (packed >> 5) & 0x1F;
        var third = packed & 0x1F;
        if (first is < 1 or > 26 || second is < 1 or > 26 || third is < 1 or > 26)
        {
            return null;
        }

        var manufacturer = new string([(char)('A' + first - 1), (char)('A' + second - 1), (char)('A' + third - 1)]);
        var product = edid[10] | (edid[11] << 8);
        return $"{manufacturer}{product:X4}";
    }

    private static string? DecodeSerialNumber(byte[] edid)
    {
        for (var block = 0; block < DescriptorCount; block++)
        {
            var offset = DescriptorStart + block * DescriptorLength;
            if (offset + DescriptorLength > edid.Length)
            {
                break;
            }

            if (edid[offset] != 0 || edid[offset + 1] != 0 || edid[offset + 2] != 0 || edid[offset + 3] != SerialDescriptorTag)
            {
                continue;
            }

            var text = Encoding.ASCII.GetString(edid, offset + 5, 13);
            var end = text.IndexOf('\n');
            var serial = (end < 0 ? text : text[..end]).Trim();
            return serial.Length == 0 || serial.All(character => character == '0') ? null : serial;
        }

        return null;
    }

    internal static string? RegistryPath(string monitorDevicePath)
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
