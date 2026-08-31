using ZoneManager.Core.Models;

namespace ZoneManager.Tests.Support;

internal static class ConfigurationSamples
{
    public static SnapConfiguration TwoLayouts()
    {
        var monitor = new MonitorIdentity("DISPLAY-A", "\\\\.\\DISPLAY1", "Hauptmonitor");
        var workZones = new[]
        {
            new ZoneDefinition(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), "Links", new NormalizedRect(0, 0, 0.5, 1)),
            new ZoneDefinition(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), "Rechts", new NormalizedRect(0.5, 0, 0.5, 1))
        };
        var eveningZones = new[]
        {
            new ZoneDefinition(Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), "Video", new NormalizedRect(0, 0, 0.7, 1)),
            new ZoneDefinition(Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"), "Web", new NormalizedRect(0.7, 0, 0.3, 1))
        };

        return new SnapConfiguration(
            SnapConfiguration.CurrentSchemaVersion,
            AppSettings.Default(Guid.Empty),
            [
                new MonitorLayout(monitor, 3440, 1440, workZones)
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    Name = "Arbeit",
                    IsActive = true
                },
                new MonitorLayout(monitor, 3440, 1440, eveningZones)
                {
                    Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    Name = "Abend",
                    IsActive = false
                }
            ]);
    }
}
