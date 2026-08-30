using SnapZones.Core.Models;
using SnapZones.Core.Persistence;
using SnapZones.Core.Placement;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.Persistence;

public sealed class WindowPlacementMigrationTests
{
    [Fact]
    public async Task Load_migrates_schema_one_to_two_without_treating_it_as_corrupt()
    {
        using var directory = new TemporaryDirectory();
        var profileId = "11111111-1111-1111-1111-111111111111";
        var json = $$"""
        { "SchemaVersion": 1, "Settings": {
          "ActiveProfileId": "{{profileId}}", "SnappingEnabled": false,
          "StartWithWindows": false, "OverlayScope": "AllMonitors",
          "TriggerMode": "Immediate", "OuterMargin": 8, "ZoneGap": 8,
          "OverlayColor": "#707070", "OverlayOpacity": 0.24 },
          "Profiles": [{ "Id": "{{profileId}}", "Name": "Standard", "QuickSlot": 1, "Monitors": [] }] }
        """;
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "settings.json"), json);

        var result = await new JsonConfigurationRepository(directory.Path).LoadAsync(CancellationToken.None);

        Assert.False(result.RecoveredFromError);
        Assert.Equal(2, result.Configuration.SchemaVersion);
        Assert.True(result.Configuration.Settings.RestoreWindowPlacementEnabled);
        Assert.Empty(result.Configuration.Settings.EffectiveWindowPlacementRules);
        Assert.Empty(Directory.GetFiles(directory.Path, "settings.invalid-*.json"));
    }

    [Fact]
    public async Task Load_migrates_schema_one_backup_before_validation()
    {
        using var directory = new TemporaryDirectory();
        var profileId = "11111111-1111-1111-1111-111111111111";
        var json = $$"""
        { "SchemaVersion": 1, "Settings": {
          "ActiveProfileId": "{{profileId}}", "SnappingEnabled": false,
          "StartWithWindows": false, "OverlayScope": "AllMonitors",
          "TriggerMode": "Immediate", "OuterMargin": 8, "ZoneGap": 8,
          "OverlayColor": "#707070", "OverlayOpacity": 0.24 },
          "Profiles": [{ "Id": "{{profileId}}", "Name": "Standard", "QuickSlot": 1, "Monitors": [] }] }
        """;
        await File.WriteAllTextAsync(Path.Combine(directory.Path, "settings.backup-1.json"), json);

        var result = await new JsonConfigurationRepository(directory.Path).LoadAsync(CancellationToken.None);

        Assert.True(result.RecoveredFromError);
        Assert.Equal(2, result.Configuration.SchemaVersion);
        Assert.True(result.Configuration.Settings.RestoreWindowPlacementEnabled);
        Assert.Empty(result.Configuration.Settings.EffectiveWindowPlacementRules);
    }

    [Fact]
    public async Task Save_rejects_duplicate_window_placement_rule_ids()
    {
        using var directory = new TemporaryDirectory();
        var ruleId = Guid.NewGuid();
        var rule = new WindowPlacementRule(
            ruleId,
            IsEnabled: true,
            ApplicationKey: "notepad.exe",
            WindowClass: null,
            WindowKind: null,
            TitlePattern: null,
            Action: WindowPlacementMode.RememberLast,
            ProfileId: null,
            MonitorStableId: null,
            ZoneId: null);
        var configuration = ConfigurationSamples.TwoProfiles();
        configuration = configuration with
        {
            Settings = configuration.Settings with
            {
                WindowPlacementRules = [rule, rule with { IsEnabled = false }]
            }
        };

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new JsonConfigurationRepository(directory.Path).SaveAsync(configuration, CancellationToken.None));
    }

    [Fact]
    public async Task Save_rejects_window_placement_rule_without_application_key()
    {
        using var directory = new TemporaryDirectory();
        var configuration = ConfigurationSamples.TwoProfiles();
        var rule = new WindowPlacementRule(
            Guid.NewGuid(),
            IsEnabled: true,
            ApplicationKey: " ",
            WindowClass: null,
            WindowKind: null,
            TitlePattern: null,
            Action: WindowPlacementMode.FixedZone,
            ProfileId: configuration.Profiles[0].Id,
            MonitorStableId: null,
            ZoneId: null);
        configuration = configuration with
        {
            Settings = configuration.Settings with { WindowPlacementRules = [rule] }
        };

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            new JsonConfigurationRepository(directory.Path).SaveAsync(configuration, CancellationToken.None));
    }

    [Fact]
    public async Task Save_then_load_keeps_schema_two_rules_with_missing_targets()
    {
        using var directory = new TemporaryDirectory();
        var configuration = ConfigurationSamples.TwoProfiles();
        var rule = new WindowPlacementRule(
            Guid.NewGuid(),
            IsEnabled: true,
            ApplicationKey: "notepad.exe",
            WindowClass: null,
            WindowKind: null,
            TitlePattern: null,
            Action: WindowPlacementMode.FixedZone,
            ProfileId: configuration.Profiles[0].Id,
            MonitorStableId: null,
            ZoneId: null);
        configuration = configuration with
        {
            Settings = configuration.Settings with { WindowPlacementRules = [rule] }
        };
        var repository = new JsonConfigurationRepository(directory.Path);

        await repository.SaveAsync(configuration, CancellationToken.None);
        var result = await repository.LoadAsync(CancellationToken.None);

        Assert.False(result.RecoveredFromError);
        Assert.Equal(2, result.Configuration.SchemaVersion);
        Assert.True(result.Configuration.Settings.RestoreWindowPlacementEnabled);
        Assert.Equal([rule], result.Configuration.Settings.EffectiveWindowPlacementRules);
    }
}
