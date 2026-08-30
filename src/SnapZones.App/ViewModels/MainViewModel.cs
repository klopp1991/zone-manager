using System.Collections.ObjectModel;
using SnapZones.Core.Models;
using SnapZones.Core.Monitors;
using SnapZones.Core.Profiles;

namespace SnapZones.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly ProfileService profileService;
    private readonly IReadOnlyList<LiveMonitor> liveMonitors;
    private LayoutProfile selectedProfile;
    private MonitorChoice? selectedMonitor;
    private LayoutEditorViewModel? editor;
    private string statusMessage = "Bereit";

    public MainViewModel(SnapConfiguration configuration, IReadOnlyList<LiveMonitor> monitors)
    {
        profileService = new ProfileService(configuration);
        liveMonitors = monitors;
        selectedProfile = profileService.ActiveProfile;
        Settings = new SettingsViewModel(profileService.Configuration.Settings);
        Profiles = new ObservableCollection<LayoutProfile>(profileService.Configuration.Profiles);
        Monitors = [];
        RefreshMonitors();
    }

    public event Action<SnapConfiguration>? SaveRequested;

    public ObservableCollection<LayoutProfile> Profiles { get; }
    public ObservableCollection<MonitorChoice> Monitors { get; }
    public SettingsViewModel Settings { get; }

    public LayoutProfile SelectedProfile
    {
        get => selectedProfile;
        set
        {
            if (value is null || value.Id == selectedProfile.Id)
            {
                return;
            }

            StoreValidDraft();
            profileService.Activate(value.Id);
            selectedProfile = profileService.ActiveProfile;
            OnPropertyChanged();
            RefreshMonitors();
        }
    }

    public MonitorChoice? SelectedMonitor
    {
        get => selectedMonitor;
        set
        {
            if (value == selectedMonitor)
            {
                return;
            }

            StoreValidDraft();
            selectedMonitor = value;
            editor = value is null ? null : new LayoutEditorViewModel(value.Layout);
            OnPropertyChanged();
            OnPropertyChanged(nameof(Editor));
        }
    }

    public LayoutEditorViewModel? Editor => editor;

    public string StatusMessage
    {
        get => statusMessage;
        set => SetProperty(ref statusMessage, value);
    }

    public SnapConfiguration Configuration => profileService.Configuration;

    public void Save()
    {
        if (Editor is not null)
        {
            profileService.UpdateMonitorLayout(Editor.CreateSnapshot());
        }

        profileService.UpdateSettings(Settings.CreateSettings(profileService.ActiveProfile.Id));
        RefreshProfiles();
        StatusMessage = "Änderungen gespeichert";
        SaveRequested?.Invoke(profileService.Configuration);
    }

    public void AddProfile()
    {
        StoreValidDraft();
        var number = 1;
        string name;
        do
        {
            name = $"Profil {number++}";
        }
        while (profileService.Configuration.Profiles.Any(profile => profile.Name == name));

        profileService.AddProfile(name);
        RefreshProfiles();
        selectedProfile = profileService.ActiveProfile;
        OnPropertyChanged(nameof(SelectedProfile));
        RefreshMonitors();
        StatusMessage = $"{name} erstellt";
    }

    public void RenameSelectedProfile(string name)
    {
        profileService.RenameProfile(SelectedProfile.Id, name);
        RefreshProfiles();
        selectedProfile = profileService.ActiveProfile;
        OnPropertyChanged(nameof(SelectedProfile));
    }

    public void DeleteSelectedProfile()
    {
        profileService.DeleteProfile(SelectedProfile.Id);
        RefreshProfiles();
        selectedProfile = profileService.ActiveProfile;
        OnPropertyChanged(nameof(SelectedProfile));
        RefreshMonitors();
        StatusMessage = "Profil gelöscht";
    }

    public void ActivateProfile(Guid profileId)
    {
        StoreValidDraft();
        profileService.Activate(profileId);
        RefreshProfiles();
        selectedProfile = profileService.ActiveProfile;
        OnPropertyChanged(nameof(SelectedProfile));
        RefreshMonitors();
        StatusMessage = $"Profil «{selectedProfile.Name}» aktiv";
    }

    public void DisableSnappingForSafety(string reason)
    {
        Settings.SnappingEnabled = false;
        profileService.UpdateSettings(Settings.CreateSettings(profileService.ActiveProfile.Id));
        StatusMessage = reason;
    }

    private void StoreValidDraft()
    {
        if (Editor is not null && Editor.CanSave)
        {
            profileService.UpdateMonitorLayout(Editor.CreateSnapshot());
        }
    }

    private void RefreshProfiles()
    {
        Profiles.Clear();
        foreach (var profile in profileService.Configuration.Profiles)
        {
            Profiles.Add(profile);
        }
    }

    private void RefreshMonitors()
    {
        Monitors.Clear();
        foreach (var live in liveMonitors)
        {
            var layout = profileService.ActiveProfile.Monitors.FirstOrDefault(saved =>
                string.Equals(saved.Monitor.StableId, live.Identity.StableId, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(saved.Monitor.DeviceName, live.Identity.DeviceName, StringComparison.OrdinalIgnoreCase));
            layout ??= new MonitorLayout(
                live.Identity,
                live.WorkArea.Width,
                live.WorkArea.Height,
                [new ZoneDefinition(Guid.NewGuid(), "Voll", NormalizedRect.Full)]);
            Monitors.Add(new MonitorChoice(live, layout));
        }

        selectedMonitor = Monitors.FirstOrDefault();
        editor = selectedMonitor is null ? null : new LayoutEditorViewModel(selectedMonitor.Layout);
        OnPropertyChanged(nameof(SelectedMonitor));
        OnPropertyChanged(nameof(Editor));
    }
}
