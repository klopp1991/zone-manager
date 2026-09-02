using SnapZones.Core.Persistence;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.Persistence;

public sealed class StaleTemporaryFilesTests
{
    [Fact]
    public void Old_temporary_files_are_removed_and_fresh_ones_kept()
    {
        using var directory = new TemporaryDirectory();
        var stale = Path.Combine(directory.Path, "settings.aaaa.tmp");
        var fresh = Path.Combine(directory.Path, "settings.bbbb.tmp");
        var unrelated = Path.Combine(directory.Path, "settings.json");
        File.WriteAllText(stale, "{}");
        File.WriteAllText(fresh, "{}");
        File.WriteAllText(unrelated, "{}");
        File.SetLastWriteTimeUtc(stale, DateTime.UtcNow.AddHours(-3));

        var removed = StaleTemporaryFiles.Remove(directory.Path, "settings.*.tmp");

        Assert.Equal(1, removed);
        Assert.False(File.Exists(stale));
        Assert.True(File.Exists(fresh));
        Assert.True(File.Exists(unrelated));
    }

    [Fact]
    public void Missing_directory_is_not_an_error()
    {
        Assert.Equal(0, StaleTemporaryFiles.Remove(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), "*.tmp"));
    }

    [Fact]
    public async Task Loading_the_configuration_cleans_up_leftovers()
    {
        using var directory = new TemporaryDirectory();
        var stale = Path.Combine(directory.Path, "settings.cccc.tmp");
        File.WriteAllText(stale, "{}");
        File.SetLastWriteTimeUtc(stale, DateTime.UtcNow.AddDays(-2));
        var repository = new JsonConfigurationRepository(directory.Path);

        _ = await repository.LoadAsync(CancellationToken.None);

        Assert.False(File.Exists(stale));
    }
}
