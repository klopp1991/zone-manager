using SnapZones.Core.Profiles;
using SnapZones.Core.Models;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.Profiles;

public sealed class ProfileServiceTests
{
    [Fact]
    public void ActivateQuickSlot_changes_active_profile()
    {
        var service = new ProfileService(ConfigurationSamples.TwoProfiles());

        var evening = service.ActivateQuickSlot(2);

        Assert.Equal("Abend", evening.Name);
        Assert.Equal(evening.Id, service.Configuration.Settings.ActiveProfileId);
    }

    [Fact]
    public void AssignQuickSlot_rejects_duplicate_slot()
    {
        var service = new ProfileService(ConfigurationSamples.TwoProfiles());

        Assert.Throws<InvalidOperationException>(() =>
            service.AssignQuickSlot(service.Configuration.Profiles[0].Id, 2));
    }

    [Fact]
    public void Constructor_falls_back_to_first_profile_when_active_id_is_missing()
    {
        var configuration = ConfigurationSamples.TwoProfiles();
        configuration = configuration with
        {
            Settings = configuration.Settings with { ActiveProfileId = Guid.NewGuid() }
        };

        var service = new ProfileService(configuration);

        Assert.Equal(configuration.Profiles[0].Id, service.ActiveProfile.Id);
        Assert.Equal(configuration.Profiles[0].Id, service.Configuration.Settings.ActiveProfileId);
    }

    [Fact]
    public void UpdateMonitorLayout_replaces_matching_monitor_in_active_profile()
    {
        var service = new ProfileService(ConfigurationSamples.TwoProfiles());
        var original = service.ActiveProfile.Monitors[0];
        var changed = original with
        {
            Zones = [new ZoneDefinition(Guid.NewGuid(), "Voll", NormalizedRect.Full)]
        };

        service.UpdateMonitorLayout(changed);

        Assert.Single(service.ActiveProfile.Monitors[0].Zones);
        Assert.Equal("Voll", service.ActiveProfile.Monitors[0].Zones[0].Name);
    }

    [Fact]
    public void AddProfile_activates_new_profile_and_delete_rejects_last_profile()
    {
        var service = new ProfileService(SnapConfiguration.CreateDefault());

        var added = service.AddProfile("Abend");
        service.DeleteProfile(service.Configuration.Profiles.Single(profile => profile.Name == "Standard").Id);

        Assert.Equal(added.Id, service.ActiveProfile.Id);
        Assert.Throws<InvalidOperationException>(() => service.DeleteProfile(added.Id));
    }

    [Fact]
    public void RenameProfile_updates_name_and_rejects_duplicate_name()
    {
        var service = new ProfileService(ConfigurationSamples.TwoProfiles());

        service.RenameProfile(service.ActiveProfile.Id, "Büro");

        Assert.Equal("Büro", service.ActiveProfile.Name);
        Assert.Throws<InvalidOperationException>(() => service.RenameProfile(service.ActiveProfile.Id, "Abend"));
    }

    [Fact]
    public void UpdateSettings_keeps_profiles_and_applies_safe_toggle()
    {
        var service = new ProfileService(ConfigurationSamples.TwoProfiles());
        var count = service.Configuration.Profiles.Count;

        service.UpdateSettings(service.Configuration.Settings with { SnappingEnabled = true });

        Assert.True(service.Configuration.Settings.SnappingEnabled);
        Assert.Equal(count, service.Configuration.Profiles.Count);
    }
}
