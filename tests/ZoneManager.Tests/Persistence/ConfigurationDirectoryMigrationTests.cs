using System.IO;
using ZoneManager.Core.Persistence;
using Xunit;

namespace ZoneManager.Tests.Persistence;

public sealed class ConfigurationDirectoryMigrationTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        $"ZoneManager-MigrationTest-{Guid.NewGuid():N}");

    private string Legacy => Path.Combine(root, "SnapZones");
    private string Target => Path.Combine(root, "ZoneManager");

    [Fact]
    public void Without_a_legacy_folder_nothing_is_migrated()
    {
        var result = ConfigurationDirectoryMigration.Run(Legacy, Target);

        Assert.Equal(ConfigurationMigrationStatus.NothingToMigrate, result.Status);
        Assert.Equal(0, result.CopiedFileCount);
        Assert.False(Directory.Exists(Target));
    }

    [Fact]
    public void A_legacy_folder_is_taken_over_and_kept()
    {
        WriteLegacyFile("settings.json", "{\"schemaVersion\":2}");
        WriteLegacyFile("settings.backup-1.json", "{}");

        var result = ConfigurationDirectoryMigration.Run(Legacy, Target);

        Assert.Equal(ConfigurationMigrationStatus.Migrated, result.Status);
        Assert.Equal(2, result.CopiedFileCount);
        Assert.Equal("{\"schemaVersion\":2}", File.ReadAllText(Path.Combine(Target, "settings.json")));
        Assert.True(File.Exists(Path.Combine(Legacy, "settings.json")));
    }

    [Fact]
    public void An_existing_new_configuration_takes_precedence()
    {
        WriteLegacyFile("settings.json", "alt");
        Directory.CreateDirectory(Target);
        File.WriteAllText(Path.Combine(Target, "settings.json"), "neu");

        var result = ConfigurationDirectoryMigration.Run(Legacy, Target);

        Assert.Equal(ConfigurationMigrationStatus.TargetAlreadyPresent, result.Status);
        Assert.Equal("neu", File.ReadAllText(Path.Combine(Target, "settings.json")));
    }

    [Fact]
    public void A_second_start_changes_nothing()
    {
        WriteLegacyFile("settings.json", "alt");

        var first = ConfigurationDirectoryMigration.Run(Legacy, Target);
        File.WriteAllText(Path.Combine(Target, "settings.json"), "inzwischen geändert");
        var second = ConfigurationDirectoryMigration.Run(Legacy, Target);

        Assert.Equal(ConfigurationMigrationStatus.Migrated, first.Status);
        Assert.Equal(ConfigurationMigrationStatus.TargetAlreadyPresent, second.Status);
        Assert.Equal("inzwischen geändert", File.ReadAllText(Path.Combine(Target, "settings.json")));
    }

    [Fact]
    public void A_damaged_legacy_file_is_taken_over_unchanged()
    {
        WriteLegacyFile("settings.json", "{ das ist kein JSON");
        WriteLegacyFile("settings.backup-1.json", "{\"schemaVersion\":2}");

        var result = ConfigurationDirectoryMigration.Run(Legacy, Target);

        Assert.Equal(ConfigurationMigrationStatus.Migrated, result.Status);
        Assert.Equal("{ das ist kein JSON", File.ReadAllText(Path.Combine(Target, "settings.json")));
        Assert.True(File.Exists(Path.Combine(Target, "settings.backup-1.json")));
    }

    [Fact]
    public void Subfolders_are_taken_over_as_well()
    {
        WriteLegacyFile(Path.Combine("exports", "backup.json"), "{}");

        var result = ConfigurationDirectoryMigration.Run(Legacy, Target);

        Assert.Equal(ConfigurationMigrationStatus.Migrated, result.Status);
        Assert.Equal(1, result.CopiedFileCount);
        Assert.True(File.Exists(Path.Combine(Target, "exports", "backup.json")));
    }

    private void WriteLegacyFile(string relativePath, string content)
    {
        var path = Path.Combine(Legacy, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
