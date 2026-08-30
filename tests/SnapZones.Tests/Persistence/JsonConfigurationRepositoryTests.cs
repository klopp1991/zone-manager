using SnapZones.Core.Persistence;
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
}
