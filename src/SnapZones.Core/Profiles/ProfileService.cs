using SnapZones.Core.Models;

namespace SnapZones.Core.Profiles;

public sealed class ProfileService
{
    public ProfileService(SnapConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (configuration.Profiles.Count == 0)
        {
            throw new ArgumentException("Mindestens ein Profil ist erforderlich.", nameof(configuration));
        }

        Configuration = configuration;
        if (configuration.Profiles.All(profile => profile.Id != configuration.Settings.ActiveProfileId))
        {
            Configuration = configuration with
            {
                Settings = configuration.Settings with { ActiveProfileId = configuration.Profiles[0].Id }
            };
        }
    }

    public SnapConfiguration Configuration { get; private set; }

    public LayoutProfile ActiveProfile =>
        Configuration.Profiles.Single(profile => profile.Id == Configuration.Settings.ActiveProfileId);

    public LayoutProfile Activate(Guid profileId)
    {
        var profile = Configuration.Profiles.FirstOrDefault(candidate => candidate.Id == profileId)
            ?? throw new KeyNotFoundException("Das Profil wurde nicht gefunden.");
        Configuration = Configuration with
        {
            Settings = Configuration.Settings with { ActiveProfileId = profile.Id }
        };
        return profile;
    }

    public LayoutProfile ActivateQuickSlot(int slot)
    {
        EnsureValidSlot(slot);
        var profile = Configuration.Profiles.FirstOrDefault(candidate => candidate.QuickSlot == slot)
            ?? throw new KeyNotFoundException($"Dem Schnellwahlplatz {slot} ist kein Profil zugeordnet.");
        return Activate(profile.Id);
    }

    public void AssignQuickSlot(Guid profileId, int slot)
    {
        EnsureValidSlot(slot);
        if (Configuration.Profiles.Any(profile => profile.Id != profileId && profile.QuickSlot == slot))
        {
            throw new InvalidOperationException($"Der Schnellwahlplatz {slot} ist bereits belegt.");
        }

        var index = Configuration.Profiles.ToList().FindIndex(profile => profile.Id == profileId);
        if (index < 0)
        {
            throw new KeyNotFoundException("Das Profil wurde nicht gefunden.");
        }

        var profiles = Configuration.Profiles.ToArray();
        profiles[index] = profiles[index] with { QuickSlot = slot };
        Configuration = Configuration with { Profiles = profiles };
    }

    private static void EnsureValidSlot(int slot)
    {
        if (slot is < 1 or > 9)
        {
            throw new ArgumentOutOfRangeException(nameof(slot), "Der Schnellwahlplatz muss zwischen 1 und 9 liegen.");
        }
    }
}
