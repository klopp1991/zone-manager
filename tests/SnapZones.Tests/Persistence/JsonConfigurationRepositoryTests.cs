using SnapZones.Core.Persistence;
using SnapZones.Core.Models;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.Persistence;

public sealed class JsonConfigurationRepositoryTests
{
    [Fact]
    public async Task Load_migrates_each_schema_one_profile_monitor_pair_to_an_independent_layout()
    {
        using var directory = new TemporaryDirectory();
        var activeProfileId = "11111111-1111-1111-1111-111111111111";
        var inactiveProfileId = "22222222-2222-2222-2222-222222222222";
        var json = $$"""
        {
          "SchemaVersion": 1,
          "Settings": {
            "ActiveProfileId": "{{activeProfileId}}",
            "SnappingEnabled": false,
            "StartWithWindows": false,
            "OverlayScope": "AllMonitors",
            "TriggerMode": "Immediate",
            "OuterMargin": 8,
            "ZoneGap": 8,
            "OverlayColor": "#707070",
            "OverlayOpacity": 0.24
          },
          "Profiles": [
            {
              "Id": "{{activeProfileId}}",
              "Name": "Arbeit",
              "QuickSlot": 1,
              "Monitors": [{
                "Monitor": { "StableId": "DISPLAY-A", "DeviceName": "\\\\.\\DISPLAY1", "FriendlyName": "Monitor A" },
                "SavedWidth": 2560,
                "SavedHeight": 1440,
                "Zones": [{ "Id": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", "Name": "Links", "Bounds": { "X": 0, "Y": 0, "Width": 0.5, "Height": 1 } }]
              }]
            },
            {
              "Id": "{{inactiveProfileId}}",
              "Name": "Gaming",
              "QuickSlot": 2,
              "Monitors": [{
                "Monitor": { "StableId": "DISPLAY-A", "DeviceName": "\\\\.\\DISPLAY1", "FriendlyName": "Monitor A" },
                "SavedWidth": 2560,
                "SavedHeight": 1440,
                "Zones": [{ "Id": "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb", "Name": "Voll", "Bounds": { "X": 0, "Y": 0, "Width": 1, "Height": 1 } }]
              }]
            }
          ]
        }
        """;
        await File.WriteAllTextAsync(System.IO.Path.Combine(directory.Path, "settings.json"), json);

        var result = await new JsonConfigurationRepository(directory.Path).LoadAsync(CancellationToken.None);

        Assert.Equal(2, result.Configuration.SchemaVersion);
        Assert.Equal(["Arbeit", "Gaming"], result.Configuration.Layouts.Select(layout => layout.Name));
        Assert.True(result.Configuration.Layouts.Single(layout => layout.Name == "Arbeit").IsActive);
        Assert.False(result.Configuration.Layouts.Single(layout => layout.Name == "Gaming").IsActive);
        Assert.Equal("Links", result.Configuration.Layouts.Single(layout => layout.Name == "Arbeit").Zones.Single().Name);
    }

    [Fact]
    public async Task Save_then_load_preserves_layouts_and_leaves_no_temporary_file()
    {
        using var directory = new TemporaryDirectory();
        var repository = new JsonConfigurationRepository(directory.Path);
        var expected = ConfigurationSamples.TwoLayouts() with
        {
            MonitorNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["stable:DISPLAY-A"] = "Links oben"
            }
        };

        await repository.SaveAsync(expected, CancellationToken.None);
        var actual = await repository.LoadAsync(CancellationToken.None);

        Assert.False(actual.RecoveredFromError);
        Assert.Equal(expected.Settings, actual.Configuration.Settings);
        Assert.Equal(expected.MonitorNames, actual.Configuration.MonitorNames);
        Assert.Equal(expected.Layouts.Select(layout => layout.Name), actual.Configuration.Layouts.Select(layout => layout.Name));
        Assert.Equal(expected.Layouts[1].Zones, actual.Configuration.Layouts[1].Zones);
        Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
    }

    [Fact]
    public async Task Load_backs_up_invalid_json_and_returns_safe_defaults()
    {
        using var directory = new TemporaryDirectory();
        await File.WriteAllTextAsync(System.IO.Path.Combine(directory.Path, "settings.json"), "{");
        var repository = new JsonConfigurationRepository(directory.Path);

        var result = await repository.LoadAsync(CancellationToken.None);

        Assert.True(result.RecoveredFromError);
        Assert.False(result.Configuration.Settings.SnappingEnabled);
        Assert.Single(Directory.GetFiles(directory.Path, "settings.invalid-*.json"));
    }

    [Fact]
    public async Task Load_recovers_last_valid_configuration_when_primary_file_is_corrupt()
    {
        using var directory = new TemporaryDirectory();
        var repository = new JsonConfigurationRepository(directory.Path);
        var expected = ConfigurationSamples.TwoLayouts();
        await repository.SaveAsync(expected, CancellationToken.None);
        await repository.SaveAsync(
            expected with
            {
                Settings = expected.Settings with { OverlayColor = "#123456" }
            },
            CancellationToken.None);
        await File.WriteAllTextAsync(System.IO.Path.Combine(directory.Path, "settings.json"), "{");

        var result = await repository.LoadAsync(CancellationToken.None);

        Assert.True(result.RecoveredFromError);
        Assert.Equal(expected.Settings, result.Configuration.Settings);
        Assert.Equal(expected.Layouts.Select(layout => layout.Name), result.Configuration.Layouts.Select(layout => layout.Name));
        Assert.Single(Directory.GetFiles(directory.Path, "settings.invalid-*.json"));
    }

    [Fact]
    public async Task Save_keeps_the_five_most_recent_previous_configurations()
    {
        using var directory = new TemporaryDirectory();
        var repository = new JsonConfigurationRepository(directory.Path);
        var configuration = ConfigurationSamples.TwoLayouts();

        for (var index = 0; index < 7; index++)
        {
            await repository.SaveAsync(
                configuration with
                {
                    Settings = configuration.Settings with { MagnetThresholdPixels = index }
                },
                CancellationToken.None);
        }

        Assert.Equal(5, Directory.GetFiles(directory.Path, "settings.backup-*.json").Length);
    }

    [Fact]
    public async Task Save_rejects_invalid_settings_without_replacing_the_last_valid_configuration()
    {
        using var directory = new TemporaryDirectory();
        var repository = new JsonConfigurationRepository(directory.Path);
        var expected = ConfigurationSamples.TwoLayouts();
        await repository.SaveAsync(expected, CancellationToken.None);
        var invalid = expected with
        {
            Settings = expected.Settings with { OverlayColor = "#12" }
        };

        await Assert.ThrowsAsync<InvalidDataException>(() => repository.SaveAsync(invalid, CancellationToken.None));
        var loaded = await repository.LoadAsync(CancellationToken.None);

        Assert.Equal(expected.Settings, loaded.Configuration.Settings);
    }

    [Fact]
    public async Task Save_rejects_an_incomplete_monitor_layout()
    {
        using var directory = new TemporaryDirectory();
        var repository = new JsonConfigurationRepository(directory.Path);
        var configuration = ConfigurationSamples.TwoLayouts();
        var layouts = configuration.Layouts.ToArray();
        layouts[0] = layouts[0] with { SavedWidth = 0 };
        var invalid = configuration with { Layouts = layouts };

        await Assert.ThrowsAsync<InvalidDataException>(() => repository.SaveAsync(invalid, CancellationToken.None));
    }

    [Fact]
    public async Task Load_applies_new_safe_defaults_to_existing_schema_one_settings()
    {
        using var directory = new TemporaryDirectory();
        var profileId = "11111111-1111-1111-1111-111111111111";
        var json = $$"""
        {
          "SchemaVersion": 1,
          "Settings": {
            "ActiveProfileId": "{{profileId}}",
            "SnappingEnabled": false,
            "StartWithWindows": false,
            "OverlayScope": "AllMonitors",
            "TriggerMode": "Immediate",
            "OuterMargin": 8,
            "ZoneGap": 8,
            "OverlayColor": "#2F6FED",
            "OverlayOpacity": 0.24
          },
          "Profiles": [
            { "Id": "{{profileId}}", "Name": "Standard", "QuickSlot": 1, "Monitors": [] }
          ]
        }
        """;
        await File.WriteAllTextAsync(System.IO.Path.Combine(directory.Path, "settings.json"), json);
        var repository = new JsonConfigurationRepository(directory.Path);

        var result = await repository.LoadAsync(CancellationToken.None);

        Assert.False(result.RecoveredFromError);
        Assert.Equal(ThemeMode.System, result.Configuration.Settings.ThemeMode);
        Assert.Equal(10, result.Configuration.Settings.MagnetThresholdPixels);
        Assert.True(result.Configuration.Settings.ShowZoneNames);
        Assert.Equal("#707070", result.Configuration.Settings.OverlayColor);
        Assert.Equal(EdgeInsets.Uniform(8), result.Configuration.Settings.EffectiveOuterMargins);
    }
}
