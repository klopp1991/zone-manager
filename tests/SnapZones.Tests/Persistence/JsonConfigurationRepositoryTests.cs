using SnapZones.Core.Persistence;
using SnapZones.Core.Models;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.Persistence;

public sealed class JsonConfigurationRepositoryTests
{
    [Fact]
    public async Task Save_then_load_preserves_profiles_and_leaves_no_temporary_file()
    {
        using var directory = new TemporaryDirectory();
        var repository = new JsonConfigurationRepository(directory.Path);
        var expected = ConfigurationSamples.TwoProfiles();

        await repository.SaveAsync(expected, CancellationToken.None);
        var actual = await repository.LoadAsync(CancellationToken.None);

        Assert.False(actual.RecoveredFromError);
        Assert.Equal(expected.Settings, actual.Configuration.Settings);
        Assert.Equal(expected.Profiles.Select(profile => profile.Name), actual.Configuration.Profiles.Select(profile => profile.Name));
        Assert.Equal(expected.Profiles[1].Monitors[0].Zones, actual.Configuration.Profiles[1].Monitors[0].Zones);
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
        var expected = ConfigurationSamples.TwoProfiles();
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
        Assert.Equal(expected.Profiles.Select(profile => profile.Name), result.Configuration.Profiles.Select(profile => profile.Name));
        Assert.Single(Directory.GetFiles(directory.Path, "settings.invalid-*.json"));
    }

    [Fact]
    public async Task Save_keeps_the_five_most_recent_previous_configurations()
    {
        using var directory = new TemporaryDirectory();
        var repository = new JsonConfigurationRepository(directory.Path);
        var configuration = ConfigurationSamples.TwoProfiles();

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
        var expected = ConfigurationSamples.TwoProfiles();
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
        var configuration = ConfigurationSamples.TwoProfiles();
        var profiles = configuration.Profiles.ToArray();
        var monitors = profiles[0].Monitors.ToArray();
        monitors[0] = monitors[0] with { SavedWidth = 0 };
        profiles[0] = profiles[0] with { Monitors = monitors };
        var invalid = configuration with { Profiles = profiles };

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
