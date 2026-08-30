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
        Assert.Equal(EdgeInsets.Uniform(8), result.Configuration.Settings.EffectiveOuterMargins);
    }
}
