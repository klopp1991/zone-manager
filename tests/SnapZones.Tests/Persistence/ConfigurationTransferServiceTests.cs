using SnapZones.Core.Models;
using SnapZones.Core.Persistence;
using SnapZones.Tests.Support;
using System.Text.Json;
using Xunit;

namespace SnapZones.Tests.Persistence;

public sealed class ConfigurationTransferServiceTests
{
    [Fact]
    public async Task Export_then_import_preserves_every_current_configuration_value()
    {
        using var directory = new TemporaryDirectory();
        var path = System.IO.Path.Combine(directory.Path, "vollbackup.swz.json");
        var original = ConfigurationSamples.TwoLayouts();
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
        Assert.Equal(original.Layouts.Count, imported.Layouts.Count);
        for (var layoutIndex = 0; layoutIndex < original.Layouts.Count; layoutIndex++)
        {
            var expectedLayout = original.Layouts[layoutIndex];
            var actualLayout = imported.Layouts[layoutIndex];
            Assert.Equal(expectedLayout.Id, actualLayout.Id);
            Assert.Equal(expectedLayout.Name, actualLayout.Name);
            Assert.Equal(expectedLayout.IsActive, actualLayout.IsActive);
            Assert.Equal(expectedLayout.Monitor, actualLayout.Monitor);
            Assert.Equal(expectedLayout.SavedWidth, actualLayout.SavedWidth);
            Assert.Equal(expectedLayout.SavedHeight, actualLayout.SavedHeight);
            Assert.Equal(expectedLayout.Zones.ToArray(), actualLayout.Zones.ToArray());
        }
    }

    [Fact]
    public async Task Export_uses_the_current_product_identity()
    {
        using var directory = new TemporaryDirectory();
        var path = System.IO.Path.Combine(directory.Path, "produktname.swz.json");
        var service = new ConfigurationTransferService();

        await service.ExportAsync(path, ConfigurationSamples.TwoLayouts(), "1.2.3", CancellationToken.None);

        var json = await File.ReadAllTextAsync(path);
        using var document = JsonDocument.Parse(json);
        Assert.Equal("Sascha’s Zone Manager", document.RootElement.GetProperty("Product").GetString());
    }

    [Fact]
    public async Task Import_accepts_an_archive_from_the_previous_product_name()
    {
        using var directory = new TemporaryDirectory();
        var path = System.IO.Path.Combine(directory.Path, "alter-produktname.swz.json");
        var original = ConfigurationSamples.TwoLayouts();
        var service = new ConfigurationTransferService();
        await service.ExportAsync(path, original, "1.2.3", CancellationToken.None);
        var json = await File.ReadAllTextAsync(path);
        await File.WriteAllTextAsync(
            path,
            json.Replace("Sascha’s Zone Manager", "Sascha Window Zones", StringComparison.Ordinal));

        var imported = await service.ImportAsync(path, CancellationToken.None);

        Assert.Equal(original.Settings, imported.Settings);
        Assert.Equal(original.Layouts.Select(layout => layout.Id), imported.Layouts.Select(layout => layout.Id));
    }

    [Fact]
    public async Task Import_rejects_an_unknown_archive_version()
    {
        using var directory = new TemporaryDirectory();
        var path = System.IO.Path.Combine(directory.Path, "zu-neu.swz.json");
        var service = new ConfigurationTransferService();
        await service.ExportAsync(path, ConfigurationSamples.TwoLayouts(), "1.2.3", CancellationToken.None);
        var json = await File.ReadAllTextAsync(path);
        await File.WriteAllTextAsync(path, json.Replace("\"ArchiveVersion\": 1", "\"ArchiveVersion\": 2", StringComparison.Ordinal));

        await Assert.ThrowsAsync<InvalidDataException>(() => service.ImportAsync(path, CancellationToken.None));
    }

    [Fact]
    public async Task Import_rejects_a_monitor_without_an_active_layout_before_any_configuration_is_applied()
    {
        using var directory = new TemporaryDirectory();
        var path = System.IO.Path.Combine(directory.Path, "ungueltig.swz.json");
        var service = new ConfigurationTransferService();
        var configuration = ConfigurationSamples.TwoLayouts();
        await service.ExportAsync(path, configuration, "1.2.3", CancellationToken.None);
        var json = await File.ReadAllTextAsync(path);
        await File.WriteAllTextAsync(path, json.Replace("\"IsActive\": true", "\"IsActive\": false", StringComparison.Ordinal));

        await Assert.ThrowsAsync<InvalidDataException>(() => service.ImportAsync(path, CancellationToken.None));
    }
}
