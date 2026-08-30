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

    public LayoutProfile? SelectedTargetProfile
    {
        get => selectedTargetProfile;
        set
        {
            if (!SetProperty(ref selectedTargetProfile, value) || loadingRuleEditor)
            {
                return;
            }

            RefreshTargetMonitors(null, null);
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

            RefreshTargetZones(null);
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
        ArgumentNullException.ThrowIfNull(replacement);
        var selectedIdentity = SelectedItem?.Identity;
        catalog = replacement;
        OnPropertyChanged(nameof(Catalog));
        RebuildItems(selectedIdentity);
    }

    public void ReplaceRules(IReadOnlyList<WindowPlacementRule> replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        rules = replacement.ToArray();
        OnPropertyChanged(nameof(Rules));
        RebuildItems(SelectedItem?.Identity);
    }

    public void ReplaceTargets(
        IReadOnlyList<LayoutProfile> replacementProfiles,
        IReadOnlyList<MonitorChoice> replacementMonitors)
    {
        ArgumentNullException.ThrowIfNull(replacementProfiles);
        ArgumentNullException.ThrowIfNull(replacementMonitors);
        var profileId = SelectedTargetProfile?.Id;
        var monitorId = SelectedTargetMonitor?.Live.Identity.StableId;
        var zoneId = SelectedTargetZone?.Id;
        profiles = replacementProfiles.ToArray();
        monitors = replacementMonitors.ToArray();
        RebuildItems(SelectedItem?.Identity);
        RefreshTargetProfiles(profileId, monitorId, zoneId);
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

        var exactIndexes = rules
            .Select((rule, index) => (rule, index))
            .Where(candidate => WindowPlacementItemViewModel.IsExactIdentityRule(candidate.rule, item.Identity))
            .ToArray();
        var existing = exactIndexes.FirstOrDefault().rule;
        var normalizedPattern = string.IsNullOrEmpty(TitlePattern) ? null : TitlePattern;
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
            .Where(rule => !WindowPlacementItemViewModel.IsExactIdentityRule(rule, item.Identity))
            .ToList();
        insertIndex = Math.Min(insertIndex, updated.Count);
        updated.Insert(insertIndex, replacement);
        rules = updated.ToArray();
        OnPropertyChanged(nameof(Rules));
        RebuildItems(item.Identity);
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
        var rule = SelectedItem is { } item
            ? rules.FirstOrDefault(candidate => WindowPlacementItemViewModel.IsExactIdentityRule(candidate, item.Identity))
            : null;
        loadingRuleEditor = true;
        try
        {
            TitlePattern = rule?.TitlePattern ?? string.Empty;
            RefreshTargetProfiles(rule?.ProfileId, rule?.MonitorStableId, rule?.ZoneId);
        }
        finally
        {
            loadingRuleEditor = false;
        }
    }

    private void RefreshTargetProfiles(Guid? preferredProfileId, string? preferredMonitorId, Guid? preferredZoneId)
    {
        TargetProfiles.Clear();
        foreach (var profile in profiles)
        {
            TargetProfiles.Add(profile);
        }

        selectedTargetProfile = TargetProfiles.FirstOrDefault(profile => profile.Id == preferredProfileId)
            ?? TargetProfiles.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedTargetProfile));
        RefreshTargetMonitors(preferredMonitorId, preferredZoneId);
    }

    private void RefreshTargetMonitors(string? preferredMonitorId, Guid? preferredZoneId)
    {
        TargetMonitors.Clear();
        if (selectedTargetProfile is { } profile)
        {
            foreach (var monitor in monitors)
            {
                var layout = profile.Monitors.FirstOrDefault(candidate => string.Equals(
                    candidate.Monitor.StableId,
                    monitor.Live.Identity.StableId,
                    StringComparison.OrdinalIgnoreCase));
                if (layout is not null)
                {
                    TargetMonitors.Add(new MonitorChoice(monitor.Live, layout));
                }
            }
        }

        selectedTargetMonitor = TargetMonitors.FirstOrDefault(monitor => string.Equals(
            monitor.Live.Identity.StableId,
            preferredMonitorId,
            StringComparison.OrdinalIgnoreCase)) ?? TargetMonitors.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedTargetMonitor));
        RefreshTargetZones(preferredZoneId);
    }

    private void RefreshTargetZones(Guid? preferredZoneId)
    {
        TargetZones.Clear();
        if (selectedTargetMonitor is { } monitor)
        {
            foreach (var zone in monitor.Layout.Zones)
            {
                TargetZones.Add(zone);
            }
        }

        selectedTargetZone = TargetZones.FirstOrDefault(zone => zone.Id == preferredZoneId)
            ?? TargetZones.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedTargetZone));
    }
}
