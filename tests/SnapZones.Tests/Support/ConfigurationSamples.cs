using SnapZones.Core.Models;

namespace SnapZones.Tests.Support;

internal static class ConfigurationSamples
{
    public static SnapConfiguration TwoProfiles()
    {
        var workId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var eveningId = Guid.Parse("22222222-2222-2222-2222-222222222222");
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

        var profiles = new[]
        {
            new LayoutProfile(workId, "Arbeit", 1, [new MonitorLayout(monitor, 3440, 1440, workZones)]),
            new LayoutProfile(eveningId, "Abend", 2, [new MonitorLayout(monitor, 3440, 1440, eveningZones)])
        };

        return new SnapConfiguration(
            SnapConfiguration.CurrentSchemaVersion,
            AppSettings.Default(workId),
            profiles);
    }
}
