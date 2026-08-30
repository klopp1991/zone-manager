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

    public void UpdateMonitorLayout(MonitorLayout layout)
    {
        var profileIndex = Configuration.Profiles.ToList().FindIndex(profile => profile.Id == ActiveProfile.Id);
        var profile = Configuration.Profiles[profileIndex];
        var monitorLayouts = profile.Monitors.ToList();
        var monitorIndex = monitorLayouts.FindIndex(existing =>
            string.Equals(existing.Monitor.StableId, layout.Monitor.StableId, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(existing.Monitor.DeviceName, layout.Monitor.DeviceName, StringComparison.OrdinalIgnoreCase));
        if (monitorIndex >= 0)
        {
            monitorLayouts[monitorIndex] = layout;
        }
        else
        {
            monitorLayouts.Add(layout);
        }

        ReplaceProfile(profileIndex, profile with { Monitors = monitorLayouts.ToArray() });
    }

    public LayoutProfile AddProfile(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var trimmedName = name.Trim();
        if (Configuration.Profiles.Any(profile => string.Equals(profile.Name, trimmedName, StringComparison.CurrentCultureIgnoreCase)))
        {
            throw new InvalidOperationException("Ein Profil mit diesem Namen ist bereits vorhanden.");
        }

        var copiedMonitors = ActiveProfile.Monitors.Select(monitor => monitor with
        {
            Zones = monitor.Zones.Select(zone => zone with { Id = Guid.NewGuid() }).ToArray()
        }).ToArray();
        var profile = new LayoutProfile(Guid.NewGuid(), trimmedName, null, copiedMonitors);
        Configuration = Configuration with { Profiles = [.. Configuration.Profiles, profile] };
        return Activate(profile.Id);
    }

    public void DeleteProfile(Guid profileId)
    {
        if (Configuration.Profiles.Count == 1)
        {
            throw new InvalidOperationException("Das letzte Profil kann nicht gelöscht werden.");
        }

        var profiles = Configuration.Profiles.Where(profile => profile.Id != profileId).ToArray();
        if (profiles.Length == Configuration.Profiles.Count)
        {
            throw new KeyNotFoundException("Das Profil wurde nicht gefunden.");
        }

        var activeId = Configuration.Settings.ActiveProfileId == profileId
            ? profiles[0].Id
            : Configuration.Settings.ActiveProfileId;
        Configuration = Configuration with
        {
            Profiles = profiles,
            Settings = Configuration.Settings with { ActiveProfileId = activeId }
        };
    }

    public void RenameProfile(Guid profileId, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var trimmedName = name.Trim();
        if (Configuration.Profiles.Any(profile =>
            profile.Id != profileId &&
            string.Equals(profile.Name, trimmedName, StringComparison.CurrentCultureIgnoreCase)))
        {
            throw new InvalidOperationException("Ein Profil mit diesem Namen ist bereits vorhanden.");
        }

        var index = Configuration.Profiles.ToList().FindIndex(profile => profile.Id == profileId);
        if (index < 0)
        {
            throw new KeyNotFoundException("Das Profil wurde nicht gefunden.");
        }

        ReplaceProfile(index, Configuration.Profiles[index] with { Name = trimmedName });
    }

    public void UpdateSettings(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Configuration = Configuration with { Settings = settings };
    }

    private void ReplaceProfile(int index, LayoutProfile profile)
    {
        var profiles = Configuration.Profiles.ToArray();
        profiles[index] = profile;
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
