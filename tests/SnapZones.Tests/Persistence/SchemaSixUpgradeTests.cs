using SnapZones.Core.Models;
using SnapZones.Core.Persistence;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.Persistence;

public sealed class SchemaSixUpgradeTests
{
    [Fact]
    public async Task Schema_five_gains_hardware_ids_and_empty_monitor_sets()
    {
        using var directory = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "settings.json"), SchemaFiveJson);
        var repository = new JsonConfigurationRepository(directory.Path);

        var loaded = await repository.LoadAsync(CancellationToken.None);

        Assert.Equal(SnapConfiguration.CurrentSchemaVersion, loaded.Configuration.SchemaVersion);
        var layout = Assert.Single(loaded.Configuration.Layouts);
        Assert.Equal("GSM9EB9", layout.Monitor.HardwareId);
        Assert.Empty(loaded.Configuration.MonitorSets);
    }

    [Fact]
    public async Task Monitor_sets_survive_a_round_trip_and_lose_dead_references()
    {
        using var directory = new TemporaryDirectory();
        var repository = new JsonConfigurationRepository(directory.Path);
        var configuration = ConfigurationSamples.TwoLayouts();
        configuration = configuration with
        {
            MonitorSets =
            [
                new MonitorSetSelection("hw:A", new Dictionary<string, Guid> { ["stable:DISPLAY-A"] = configuration.Layouts[1].Id })
            ]
        };

        await repository.SaveAsync(configuration, CancellationToken.None);
        var loaded = await repository.LoadAsync(CancellationToken.None);

        var set = Assert.Single(loaded.Configuration.MonitorSets);
        Assert.Equal(configuration.Layouts[1].Id, set.ActiveLayouts["stable:DISPLAY-A"]);

        var broken = configuration with
        {
            MonitorSets = [new MonitorSetSelection("hw:B", new Dictionary<string, Guid> { ["x"] = Guid.NewGuid() })]
        };
        await Assert.ThrowsAsync<InvalidDataException>(() => repository.SaveAsync(broken, CancellationToken.None));
    }

    private const string SchemaFiveJson = """
    {
      "SchemaVersion": 5,
      "Settings": {
        "StartWithWindows": false,
        "OverlayScope": "AllMonitors",
        "TriggerMode": "Immediate",
        "OuterMargin": 8,
        "ZoneGap": 0,
        "OverlayColor": "#707070",
        "OverlayOpacity": 0.24
      },
      "Layouts": [
        {
          "Monitor": {
            "StableId": "\\\\?\\DISPLAY#GSM9EB9#5&4ace297&1&UID4357#{e6f07b5f-ee97-4a90-b076-33f57bf4eaa7}",
            "DeviceName": "\\\\.\\DISPLAY1",
            "FriendlyName": "LG ULTRAFINE"
          },
          "SavedWidth": 5120,
          "SavedHeight": 2100,
          "Zones": [
            { "Id": "184a9e85-d223-4762-a502-f3ccaefb0ccb", "Name": "Voll", "Bounds": { "X": 0, "Y": 0, "Width": 1, "Height": 1 } }
          ],
          "Id": "f320c56e-5c44-40a9-b84c-d46d3313049c",
          "Name": "Standard",
          "IsActive": true
        }
      ],
      "MonitorNames": {},
      "MonitorOrder": [],
      "AppRules": [],
      "AppExclusions": []
    }
    """;
}
