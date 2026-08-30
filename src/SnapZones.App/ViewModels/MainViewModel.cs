using System.Collections.ObjectModel;
using SnapZones.Core.Models;
using SnapZones.Core.Monitors;
using SnapZones.Core.Profiles;
using SnapZones.Core.Placement;

namespace SnapZones.App.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private ProfileService profileService;
    private readonly IReadOnlyList<LiveMonitor> liveMonitors;
    private LayoutProfile selectedProfile;
    private MonitorChoice? selectedMonitor;
    private LayoutEditorViewModel? editor;
    private string statusMessage = "Bereit";
    private bool suppressPersistence;
    private WindowPlacementViewModel? windowPlacement;

    public MainViewModel(SnapConfiguration configuration, IReadOnlyList<LiveMonitor> monitors)
    {
        profileService = new ProfileService(configuration);
        liveMonitors = monitors;
        selectedProfile = profileService.ActiveProfile;
        Settings = new SettingsViewModel(profileService.Configuration.Settings);
        Settings.PropertyChanged += Settings_PropertyChanged;
        Profiles = new ObservableCollection<LayoutProfile>(profileService.Configuration.Profiles);
        Monitors = [];
        RefreshMonitors();
        windowPlacement = new WindowPlacementViewModel(
            WindowPlacementCatalog.Empty,
            Settings.WindowPlacementRules,
            Profiles,
            Monitors);
        windowPlacement.RulesChanged += WindowPlacement_RulesChanged;
    }

    public event Action<SnapConfiguration>? SaveRequested;

    public ObservableCollection<LayoutProfile> Profiles { get; }
    public ObservableCollection<MonitorChoice> Monitors { get; }
    public SettingsViewModel Settings { get; }
    public WindowPlacementViewModel WindowPlacement => windowPlacement!;

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
            RequestPersistence();
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
            ReplaceEditor(value is null ? null : new LayoutEditorViewModel(value.Layout));
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
        StatusMessage = "Wird gespeichert …";
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
        RequestPersistence();
    }

    public void RenameSelectedProfile(string name)
    {
        profileService.RenameProfile(SelectedProfile.Id, name);
        RefreshProfiles();
        selectedProfile = profileService.ActiveProfile;
        OnPropertyChanged(nameof(SelectedProfile));
        RequestPersistence();
    }

    public void DeleteSelectedProfile()
    {
        profileService.DeleteProfile(SelectedProfile.Id);
        RefreshProfiles();
        selectedProfile = profileService.ActiveProfile;
        OnPropertyChanged(nameof(SelectedProfile));
        RefreshMonitors();
        StatusMessage = "Profil gelöscht";
        RequestPersistence();
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
        RequestPersistence();
    }

    public void DisableSnappingForSafety(string reason)
    {
        suppressPersistence = true;
        try
        {
            Settings.RestoreWindowPlacementEnabled = false;
            Settings.SnappingEnabled = false;
        }
        finally
        {
            suppressPersistence = false;
        }

        profileService.UpdateSettings(Settings.CreateSettings(profileService.ActiveProfile.Id));
        StatusMessage = reason;
    }

    public void ReplaceConfiguration(SnapConfiguration replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        suppressPersistence = true;
        try
        {
            profileService = new ProfileService(replacement);
            Settings.Apply(profileService.Configuration.Settings);
            RefreshProfiles();
            selectedProfile = profileService.ActiveProfile;
            OnPropertyChanged(nameof(SelectedProfile));
            RefreshMonitors();
            RefreshWindowPlacement(reloadRuleEditor: true);
            StatusMessage = "Importierte Konfiguration geladen";
        }
        finally
        {
            suppressPersistence = false;
        }
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
        var selectedProfileId = selectedProfile.Id;
        Profiles.Clear();
        foreach (var profile in profileService.Configuration.Profiles)
        {
            Profiles.Add(profile);
        }

        selectedProfile = Profiles.FirstOrDefault(profile => profile.Id == selectedProfileId)
            ?? Profiles.First(profile => profile.Id == profileService.ActiveProfile.Id);
        OnPropertyChanged(nameof(SelectedProfile));

        RefreshWindowPlacement();
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
        ReplaceEditor(selectedMonitor is null ? null : new LayoutEditorViewModel(selectedMonitor.Layout));
        OnPropertyChanged(nameof(SelectedMonitor));
        OnPropertyChanged(nameof(Editor));
        RefreshWindowPlacement();
    }

    private void ReplaceEditor(LayoutEditorViewModel? replacement)
    {
        if (editor is not null)
        {
            editor.ConfigurationChanged -= Editor_ConfigurationChanged;
        }

        editor = replacement;
        if (editor is not null)
        {
            editor.ConfigurationChanged += Editor_ConfigurationChanged;
        }
    }

    private void Editor_ConfigurationChanged()
    {
        if (editor is null || !editor.IsValid)
        {
            return;
        }

        profileService.UpdateMonitorLayout(editor.CreateSnapshot());
        RefreshProfiles();
        RequestPersistence();
    }

    private void Settings_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        if (suppressPersistence)
        {
            return;
        }

        var settings = Settings.CreateSettings(profileService.ActiveProfile.Id);
        if (!IsValidOverlayColor(settings.OverlayColor))
        {
            StatusMessage = "Ungültige Eingabe";
            return;
        }

        profileService.UpdateSettings(settings);
        RefreshWindowPlacement();
        RequestPersistence();
    }

    private void WindowPlacement_RulesChanged(IReadOnlyList<WindowPlacementRule> rules) =>
        Settings.ReplaceWindowPlacementRules(rules);

    private void RefreshWindowPlacement(bool reloadRuleEditor = false)
    {
        if (windowPlacement is null)
        {
            return;
        }

        if (reloadRuleEditor)
        {
            windowPlacement.RefreshAndReloadRuleEditor(
                windowPlacement.Catalog,
                Settings.WindowPlacementRules,
                profileService.Configuration.Profiles,
                Monitors);
            return;
        }

        windowPlacement.Refresh(
            windowPlacement.Catalog,
            Settings.WindowPlacementRules,
            profileService.Configuration.Profiles,
            Monitors);
    }

    private void RequestPersistence()
    {
        StatusMessage = "Wird gespeichert …";
        SaveRequested?.Invoke(profileService.Configuration);
    }

    private static bool IsValidOverlayColor(string value) =>
        !string.IsNullOrEmpty(value) &&
        value.Length == 7 &&
        value[0] == '#' &&
        value.AsSpan(1).ToString().All(Uri.IsHexDigit);
}
