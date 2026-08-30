using SnapZones.Core.Models;
using SnapZones.Core.Persistence;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.Persistence;

public sealed class ConfigurationTransferServiceTests
{
    [Fact]
    public async Task Export_then_import_preserves_every_current_configuration_value()
    {
        using var directory = new TemporaryDirectory();
        var path = System.IO.Path.Combine(directory.Path, "vollbackup.swz.json");
        var original = ConfigurationSamples.TwoProfiles();
        original = original with
        {
            Settings = original.Settings with
            {
                SnappingEnabled = true,
                StartWithWindows = true,
                OverlayScope = OverlayScope.ActiveMonitor,
                TriggerMode = TriggerMode.ShiftKey,
                OuterMargin = 19,
                ZoneGap = 23,
                OverlayColor = "#123456",
                OverlayOpacity = 0.61,
                ThemeMode = ThemeMode.Dark,
                MagnetThresholdPixels = 31,
                ShowZoneNames = false,
                OuterMargins = new EdgeInsets(11, 12, 13, 14)
            }
        };
        var service = new ConfigurationTransferService();

        await service.ExportAsync(path, original, "1.2.3", CancellationToken.None);
        var imported = await service.ImportAsync(path, CancellationToken.None);

        Assert.Equal(original.SchemaVersion, imported.SchemaVersion);
        Assert.Equal(original.Settings, imported.Settings);
        Assert.Equal(original.Profiles.Count, imported.Profiles.Count);
        for (var profileIndex = 0; profileIndex < original.Profiles.Count; profileIndex++)
        {
            var expectedProfile = original.Profiles[profileIndex];
            var actualProfile = imported.Profiles[profileIndex];
            Assert.Equal(expectedProfile.Id, actualProfile.Id);
            Assert.Equal(expectedProfile.Name, actualProfile.Name);
            Assert.Equal(expectedProfile.QuickSlot, actualProfile.QuickSlot);
            Assert.Equal(expectedProfile.Monitors.Count, actualProfile.Monitors.Count);
            for (var monitorIndex = 0; monitorIndex < expectedProfile.Monitors.Count; monitorIndex++)
            {
                var expectedMonitor = expectedProfile.Monitors[monitorIndex];
                var actualMonitor = actualProfile.Monitors[monitorIndex];
                Assert.Equal(expectedMonitor.Monitor, actualMonitor.Monitor);
                Assert.Equal(expectedMonitor.SavedWidth, actualMonitor.SavedWidth);
                Assert.Equal(expectedMonitor.SavedHeight, actualMonitor.SavedHeight);
                Assert.Equal(expectedMonitor.Zones.ToArray(), actualMonitor.Zones.ToArray());
            }
        }
    }

    [Fact]
    public async Task Import_rejects_an_unknown_archive_version()
    {
        using var directory = new TemporaryDirectory();
        var path = System.IO.Path.Combine(directory.Path, "zu-neu.swz.json");
        var service = new ConfigurationTransferService();
        await service.ExportAsync(path, ConfigurationSamples.TwoProfiles(), "1.2.3", CancellationToken.None);
        var json = await File.ReadAllTextAsync(path);
        await File.WriteAllTextAsync(path, json.Replace("\"ArchiveVersion\": 1", "\"ArchiveVersion\": 2", StringComparison.Ordinal));

        await Assert.ThrowsAsync<InvalidDataException>(() => service.ImportAsync(path, CancellationToken.None));
    }

    [Fact]
    public async Task Import_rejects_a_missing_active_profile_before_any_configuration_is_applied()
    {
        using var directory = new TemporaryDirectory();
        var path = System.IO.Path.Combine(directory.Path, "ungueltig.swz.json");
        var service = new ConfigurationTransferService();
        var configuration = ConfigurationSamples.TwoProfiles();
        await service.ExportAsync(path, configuration, "1.2.3", CancellationToken.None);
        var json = await File.ReadAllTextAsync(path);
        var activeProfileId = configuration.Settings.ActiveProfileId.ToString();
        var activeProfileIndex = json.IndexOf(activeProfileId, StringComparison.OrdinalIgnoreCase);
        Assert.True(activeProfileIndex >= 0);
        await File.WriteAllTextAsync(
            path,
            string.Concat(
                json.AsSpan(0, activeProfileIndex),
                "99999999-9999-9999-9999-999999999999",
                json.AsSpan(activeProfileIndex + activeProfileId.Length)));

        await Assert.ThrowsAsync<InvalidDataException>(() => service.ImportAsync(path, CancellationToken.None));
    }
}
