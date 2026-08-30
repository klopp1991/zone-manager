using SnapZones.Core.Profiles;
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
}
