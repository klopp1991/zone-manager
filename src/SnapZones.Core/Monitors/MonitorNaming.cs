using SnapZones.Core.Models;

namespace SnapZones.Core.Monitors;

public static class MonitorNaming
{
    public const int MaximumCustomNameLength = 60;

    public static string KeyFor(MonitorIdentity monitor)
    {
        ArgumentNullException.ThrowIfNull(monitor);
        if (!string.IsNullOrWhiteSpace(monitor.StableId))
        {
            return $"stable:{monitor.StableId}";
        }

        if (!string.IsNullOrWhiteSpace(monitor.DeviceName))
        {
            return $"device:{monitor.DeviceName}";
        }

        throw new ArgumentException("Der Monitor besitzt keine stabile Identität.", nameof(monitor));
    }

    public static int ResolveDisplayNumber(MonitorIdentity monitor, int fallbackNumber)
    {
        ArgumentNullException.ThrowIfNull(monitor);
        var deviceName = monitor.DeviceName ?? string.Empty;
        var firstDigit = deviceName.Length;
        while (firstDigit > 0 && char.IsDigit(deviceName[firstDigit - 1]))
        {
            firstDigit--;
        }

        return firstDigit < deviceName.Length &&
               int.TryParse(deviceName.AsSpan(firstDigit), out var displayNumber) &&
               displayNumber > 0
            ? displayNumber
            : Math.Max(1, fallbackNumber);
    }

    public static string? CustomNameFor(SnapConfiguration configuration, MonitorIdentity monitor)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var key = KeyFor(monitor);
        var entry = configuration.MonitorNames.FirstOrDefault(candidate =>
            string.Equals(candidate.Key, key, StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrWhiteSpace(entry.Value) ? null : entry.Value.Trim();
    }

    public static string UserFacingName(string? customName, int displayNumber) =>
        string.IsNullOrWhiteSpace(customName)
            ? $"Monitor {displayNumber}"
            : customName.Trim();
}
