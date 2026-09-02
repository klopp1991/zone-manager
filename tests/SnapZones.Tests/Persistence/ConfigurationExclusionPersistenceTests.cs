using SnapZones.Core.AppRules;
using SnapZones.Core.Models;
using SnapZones.Core.Persistence;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.Persistence;

/// <summary>
/// Ausschluesse kamen mit Schema 5 dazu. Bestehende Staende muessen ohne Zutun weiterlaufen und dabei
/// ohne einen einzigen Ausschluss starten; ungueltige Eintraege duerfen nie in den Betrieb gelangen.
/// </summary>
public sealed class ConfigurationExclusionPersistenceTests
{
    [Fact]
    public async Task Schema_four_upgrades_to_five_with_no_exclusions_and_keeps_its_rules()
    {
        using var directory = new TemporaryDirectory();
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "settings.json"), SchemaFourJson);
        var repository = new JsonConfigurationRepository(directory.Path);

        var loaded = await repository.LoadAsync(CancellationToken.None);

        Assert.Equal(SnapConfiguration.CurrentSchemaVersion, loaded.Configuration.SchemaVersion);
        Assert.Empty(loaded.Configuration.AppExclusions);
        Assert.Equal("code.exe", Assert.Single(loaded.Configuration.AppRules).ProcessPath);
    }

    [Fact]
    public async Task Exclusions_survive_a_save_and_load_round_trip()
    {
        using var directory = new TemporaryDirectory();
        var repository = new JsonConfigurationRepository(directory.Path);
        var exclusion = new AppExclusion(Guid.NewGuid(), "notepad.exe", null, null, true);
        var configuration = ConfigurationSamples.TwoLayouts() with { AppExclusions = [exclusion] };

        await repository.SaveAsync(configuration, CancellationToken.None);
        var loaded = await repository.LoadAsync(CancellationToken.None);

        var restored = Assert.Single(loaded.Configuration.AppExclusions);
        Assert.Equal(exclusion.Id, restored.Id);
        Assert.Equal("notepad.exe", restored.ProcessPath);
        Assert.True(restored.IsEnabled);
    }

    [Fact]
    public async Task An_exclusion_without_any_criteria_is_rejected()
    {
        // Ein solcher Eintrag wuerde jedes Fenster erfassen und die Anwendung wirkungslos machen.
        using var directory = new TemporaryDirectory();
        var repository = new JsonConfigurationRepository(directory.Path);
        var configuration = ConfigurationSamples.TwoLayouts() with
        {
            AppExclusions = [new AppExclusion(Guid.NewGuid(), string.Empty, null, null, true)]
        };

        await Assert.ThrowsAsync<InvalidDataException>(
            () => repository.SaveAsync(configuration, CancellationToken.None));
    }

    [Fact]
    public async Task Two_exclusions_with_the_same_identifier_are_rejected()
    {
        using var directory = new TemporaryDirectory();
        var repository = new JsonConfigurationRepository(directory.Path);
        var id = Guid.NewGuid();
        var configuration = ConfigurationSamples.TwoLayouts() with
        {
            AppExclusions =
            [
                new AppExclusion(id, "notepad.exe", null, null, true),
                new AppExclusion(id, "calc.exe", null, null, true)
            ]
        };

        await Assert.ThrowsAsync<InvalidDataException>(
            () => repository.SaveAsync(configuration, CancellationToken.None));
    }

    private const string SchemaFourJson = """
    {
      "SchemaVersion": 4,
      "Settings": {
        "SnappingEnabled": true,
        "StartWithWindows": false,
        "OverlayScope": "AllMonitors",
        "TriggerMode": "Immediate",
        "OuterMargin": 8,
        "ZoneGap": 0,
        "OverlayColor": "#707070",
        "OverlayOpacity": 0.24,
        "ThemeMode": "System",
        "MagnetThresholdPixels": 20,
        "ShowZoneNames": true
      },
      "Layouts": [
        {
          "Id": "11111111-1111-1111-1111-111111111111",
          "Name": "Arbeit",
          "IsActive": true,
          "Monitor": { "StableId": "DISPLAY-A", "DeviceName": "DEVICE", "FriendlyName": "Monitor A" },
          "SavedWidth": 2560,
          "SavedHeight": 1440,
          "Zones": [
            { "Id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "Name": "Voll", "Bounds": { "X": 0, "Y": 0, "Width": 1, "Height": 1 } }
          ]
        }
      ],
      "AppRules": [
        {
          "Id": "33333333-3333-3333-3333-333333333333",
          "ProcessPath": "code.exe",
          "Event": "WindowCreated",
          "DelayMilliseconds": 0,
          "RetryCount": 0,
          "Priority": 50,
          "IsEnabled": true,
          "TargetLayoutId": "11111111-1111-1111-1111-111111111111",
          "TargetZoneId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"
        }
      ]
    }
    """;
}
