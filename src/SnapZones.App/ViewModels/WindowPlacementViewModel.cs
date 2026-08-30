using System.Collections.ObjectModel;
using SnapZones.Core.Models;
using SnapZones.Core.Placement;

namespace SnapZones.App.ViewModels;

public sealed class WindowPlacementViewModel : ViewModelBase
{
    private IReadOnlyList<WindowPlacementRule> rules;
    private IReadOnlyList<LayoutProfile> profiles;
    private IReadOnlyList<MonitorChoice> monitors;
    private WindowPlacementCatalog catalog;
    private WindowPlacementItemViewModel? selectedItem;
    private LayoutProfile? selectedTargetProfile;
    private MonitorChoice? selectedTargetMonitor;
    private ZoneDefinition? selectedTargetZone;
    private WindowPlacementMode? selectedRuleMode;
    private string titlePattern = string.Empty;
    private bool loadingRuleEditor;

    public WindowPlacementViewModel(
        WindowPlacementCatalog catalog,
        IReadOnlyList<WindowPlacementRule> rules,
        IReadOnlyList<LayoutProfile> profiles,
        IReadOnlyList<MonitorChoice> monitors)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.rules = rules?.ToArray() ?? throw new ArgumentNullException(nameof(rules));
        this.profiles = profiles?.ToArray() ?? throw new ArgumentNullException(nameof(profiles));
        this.monitors = monitors?.ToArray() ?? throw new ArgumentNullException(nameof(monitors));
        RebuildItems(null);
        RefreshTargetProfiles(null, null, null);
    }

    public ObservableCollection<WindowPlacementItemViewModel> Items { get; } = [];
    public ObservableCollection<LayoutProfile> TargetProfiles { get; } = [];
    public ObservableCollection<MonitorChoice> TargetMonitors { get; } = [];
    public ObservableCollection<ZoneDefinition> TargetZones { get; } = [];
    public IReadOnlyList<WindowPlacementRule> Rules => rules;
    public WindowPlacementCatalog Catalog => catalog;

    public WindowPlacementItemViewModel? SelectedItem
    {
        get => selectedItem;
        set
        {
            if (!SetProperty(ref selectedItem, value))
            {
                return;
            }

            OnPropertyChanged(nameof(HasSelection));
            LoadRuleEditor();
        }
    }

    public bool HasSelection => SelectedItem is not null;

    public WindowPlacementMode? SelectedRuleMode
    {
        get => selectedRuleMode;
        private set => SetProperty(ref selectedRuleMode, value);
    }

    public LayoutProfile? SelectedTargetProfile
    {
        get => selectedTargetProfile;
        set
        {
            if (!SetProperty(ref selectedTargetProfile, value) || loadingRuleEditor)
            {
                return;
            }

            RefreshTargetMonitors(null, null, requirePreferredTarget: false);
        }
    }

    public MonitorChoice? SelectedTargetMonitor
    {
        get => selectedTargetMonitor;
        set
        {
            if (!SetProperty(ref selectedTargetMonitor, value) || loadingRuleEditor)
            {
                return;
            }

            RefreshTargetZones(null, requirePreferredTarget: false);
        }
    }

    public ZoneDefinition? SelectedTargetZone
    {
        get => selectedTargetZone;
        set => SetProperty(ref selectedTargetZone, value);
    }

    public string TitlePattern
    {
        get => titlePattern;
        set => SetProperty(ref titlePattern, value ?? string.Empty);
    }

    public event Action<IReadOnlyList<WindowPlacementRule>>? RulesChanged;
    public event Action<WindowIdentity>? ForgetRequested;
    public event Action<WindowIdentity>? ApplyNowRequested;
    public event Action? SelectWindowRequested;

    public void ExcludeSelected() => ReplaceSpecificRule(WindowPlacementMode.Exclude, null, null, null);

    public void RememberSelected() => ReplaceSpecificRule(WindowPlacementMode.RememberLast, null, null, null);

    public void FixSelectedToZone()
    {
        var profile = SelectedTargetProfile;
        var monitor = SelectedTargetMonitor;
        var zone = SelectedTargetZone;
        if (profile is null || monitor is null || zone is null ||
            !TargetProfiles.Any(candidate => candidate.Id == profile.Id) ||
            !TargetMonitors.Any(candidate => string.Equals(
                candidate.Live.Identity.StableId,
                monitor.Live.Identity.StableId,
                StringComparison.OrdinalIgnoreCase)) ||
            !TargetZones.Any(candidate => candidate.Id == zone.Id))
        {
            throw new InvalidOperationException("Profil, Monitor und Zone müssen vollständig ausgewählt sein.");
        }

        ReplaceSpecificRule(
            WindowPlacementMode.FixedZone,
            profile.Id,
            monitor.Live.Identity.StableId,
            zone.Id);
    }

    public void ForgetSelected()
    {
        if (SelectedItem is { } item)
        {
            ForgetRequested?.Invoke(item.Identity);
        }
    }

    public void ApplySelectedNow()
    {
        if (SelectedItem is { } item)
        {
            ApplyNowRequested?.Invoke(item.Identity);
        }
    }

    public void RequestWindowSelection() => SelectWindowRequested?.Invoke();

    public bool SelectIdentity(WindowIdentity identity)
    {
        var item = Items.FirstOrDefault(candidate => candidate.Identity == identity);
        SelectedItem = item;
        return item is not null;
    }

    public void ReplaceCatalog(WindowPlacementCatalog replacement)
    {
        Refresh(replacement, rules, profiles, monitors);
    }

    public void ReplaceRules(IReadOnlyList<WindowPlacementRule> replacement)
    {
        Refresh(catalog, replacement, profiles, monitors);
    }

    public void ReplaceTargets(
        IReadOnlyList<LayoutProfile> replacementProfiles,
        IReadOnlyList<MonitorChoice> replacementMonitors)
    {
        Refresh(catalog, rules, replacementProfiles, replacementMonitors);
    }

    public void Refresh(
        WindowPlacementCatalog replacementCatalog,
        IReadOnlyList<WindowPlacementRule> replacementRules,
        IReadOnlyList<LayoutProfile> replacementProfiles,
        IReadOnlyList<MonitorChoice> replacementMonitors)
    {
        RefreshCore(
            replacementCatalog,
            replacementRules,
            replacementProfiles,
            replacementMonitors,
            reloadRuleEditor: false);
    }

    public void RefreshAndReloadRuleEditor(
        WindowPlacementCatalog replacementCatalog,
        IReadOnlyList<WindowPlacementRule> replacementRules,
        IReadOnlyList<LayoutProfile> replacementProfiles,
        IReadOnlyList<MonitorChoice> replacementMonitors)
    {
        RefreshCore(
            replacementCatalog,
            replacementRules,
            replacementProfiles,
            replacementMonitors,
            reloadRuleEditor: true);
    }

    private void RefreshCore(
        WindowPlacementCatalog replacementCatalog,
        IReadOnlyList<WindowPlacementRule> replacementRules,
        IReadOnlyList<LayoutProfile> replacementProfiles,
        IReadOnlyList<MonitorChoice> replacementMonitors,
        bool reloadRuleEditor)
    {
        ArgumentNullException.ThrowIfNull(replacementCatalog);
        ArgumentNullException.ThrowIfNull(replacementRules);
        ArgumentNullException.ThrowIfNull(replacementProfiles);
        ArgumentNullException.ThrowIfNull(replacementMonitors);
        var selectedIdentity = SelectedItem?.Identity;
        var profileId = SelectedTargetProfile?.Id;
        var monitorId = SelectedTargetMonitor?.Live.Identity.StableId;
        var zoneId = SelectedTargetZone?.Id;
        catalog = replacementCatalog;
        rules = replacementRules.ToArray();
        profiles = replacementProfiles.ToArray();
        monitors = replacementMonitors.ToArray();
        OnPropertyChanged(nameof(Catalog));
        OnPropertyChanged(nameof(Rules));
        RebuildItems(selectedIdentity);
        if (reloadRuleEditor)
        {
            LoadRuleEditor();
        }
        else
        {
            RefreshTargetProfiles(
                profileId,
                monitorId,
                zoneId,
                requirePreferredTarget: profileId is not null || monitorId is not null || zoneId is not null);
        }
    }

    private void ReplaceSpecificRule(
        WindowPlacementMode action,
        Guid? profileId,
        string? monitorStableId,
        Guid? zoneId)
    {
        if (SelectedItem is not { } item)
        {
            return;
        }

        var normalizedPattern = WindowPlacementItemViewModel.NormalizeTitlePattern(TitlePattern);
        var exactIndexes = rules
            .Select((rule, index) => (rule, index))
            .Where(candidate => WindowPlacementItemViewModel.IsSameSelector(
                candidate.rule,
                item.Identity,
                normalizedPattern))
            .ToArray();
        var existing = exactIndexes.FirstOrDefault().rule;
        var replacement = new WindowPlacementRule(
            existing?.Id ?? Guid.NewGuid(),
            true,
            item.Identity.ApplicationKey,
            item.Identity.WindowClass,
            item.Identity.Kind,
            normalizedPattern,
            action,
            profileId,
            monitorStableId,
            zoneId);
        if (exactIndexes.Length == 1 && existing == replacement)
        {
            return;
        }

        var insertIndex = exactIndexes.Length == 0 ? rules.Count : exactIndexes[0].index;
        var updated = rules
            .Where(rule => !WindowPlacementItemViewModel.IsSameSelector(rule, item.Identity, normalizedPattern))
            .ToList();
        insertIndex = Math.Min(insertIndex, updated.Count);
        updated.Insert(insertIndex, replacement);
        rules = updated.ToArray();
        OnPropertyChanged(nameof(Rules));
        RebuildItems(item.Identity);
        SelectedRuleMode = action;
        RulesChanged?.Invoke(rules);
    }

    private void RebuildItems(WindowIdentity? selectedIdentity)
    {
        Items.Clear();
        foreach (var entry in catalog.Entries.OrderByDescending(entry => entry.LastUpdatedUtc))
        {
            Items.Add(new WindowPlacementItemViewModel(entry, rules, profiles, monitors));
        }

        selectedItem = selectedIdentity is null
            ? null
            : Items.FirstOrDefault(item => item.Identity == selectedIdentity);
        OnPropertyChanged(nameof(SelectedItem));
        OnPropertyChanged(nameof(HasSelection));
    }

    private void LoadRuleEditor()
    {
        var matchingRules = SelectedItem is { } item
            ? rules.Where(candidate => WindowPlacementItemViewModel.IsExactIdentityRule(candidate, item.Identity)).ToArray()
            : [];
        var rule = matchingRules.Length == 1 ? matchingRules[0] : null;
        loadingRuleEditor = true;
        try
        {
            SelectedRuleMode = rule?.Action;
            TitlePattern = rule?.TitlePattern ?? string.Empty;
            RefreshTargetProfiles(
                rule?.ProfileId,
                rule?.MonitorStableId,
                rule?.ZoneId,
                requirePreferredTarget: rule?.Action == WindowPlacementMode.FixedZone);
        }
        finally
        {
            loadingRuleEditor = false;
        }
    }

    private void RefreshTargetProfiles(
        Guid? preferredProfileId,
        string? preferredMonitorId,
        Guid? preferredZoneId,
        bool requirePreferredTarget = false)
    {
        TargetProfiles.Clear();
        foreach (var profile in profiles)
        {
            TargetProfiles.Add(profile);
        }

        selectedTargetProfile = TargetProfiles.FirstOrDefault(profile => profile.Id == preferredProfileId);
        if (selectedTargetProfile is null && !(requirePreferredTarget && preferredProfileId is not null))
        {
            selectedTargetProfile = TargetProfiles.FirstOrDefault();
        }

        OnPropertyChanged(nameof(SelectedTargetProfile));
        RefreshTargetMonitors(preferredMonitorId, preferredZoneId, requirePreferredTarget);
    }

    private void RefreshTargetMonitors(
        string? preferredMonitorId,
        Guid? preferredZoneId,
        bool requirePreferredTarget)
    {
        TargetMonitors.Clear();
        if (selectedTargetProfile is { } profile)
        {
            foreach (var monitor in monitors)
            {
                var layout = profile.Monitors.FirstOrDefault(candidate =>
                    string.Equals(candidate.Monitor.StableId, monitor.Live.Identity.StableId, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(candidate.Monitor.DeviceName, monitor.Live.Identity.DeviceName, StringComparison.OrdinalIgnoreCase));
                if (layout is not null)
                {
                    TargetMonitors.Add(new MonitorChoice(monitor.Live, layout));
                }
            }
        }

        selectedTargetMonitor = TargetMonitors.FirstOrDefault(monitor =>
            requirePreferredTarget
                ? monitor.Live.Identity.StableId == preferredMonitorId
                : string.Equals(
                    monitor.Live.Identity.StableId,
                    preferredMonitorId,
                    StringComparison.OrdinalIgnoreCase));
        if (selectedTargetMonitor is null && !(requirePreferredTarget && preferredMonitorId is not null))
        {
            selectedTargetMonitor = TargetMonitors.FirstOrDefault();
        }

        OnPropertyChanged(nameof(SelectedTargetMonitor));
        RefreshTargetZones(preferredZoneId, requirePreferredTarget);
    }

    private void RefreshTargetZones(Guid? preferredZoneId, bool requirePreferredTarget)
    {
        TargetZones.Clear();
        if (selectedTargetMonitor is { } monitor)
        {
            foreach (var zone in monitor.Layout.Zones)
            {
                TargetZones.Add(zone);
            }
        }

        selectedTargetZone = TargetZones.FirstOrDefault(zone => zone.Id == preferredZoneId);
        if (selectedTargetZone is null && !(requirePreferredTarget && preferredZoneId is not null))
        {
            selectedTargetZone = TargetZones.FirstOrDefault();
        }

        OnPropertyChanged(nameof(SelectedTargetZone));
    }
}
