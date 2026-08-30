using SnapZones.App.ViewModels;
using SnapZones.Core.Geometry;
using SnapZones.Core.Models;
using SnapZones.Core.Monitors;
using SnapZones.Core.Placement;
using Xunit;

namespace SnapZones.Tests.ViewModels;

public sealed class WindowPlacementViewModelTests
{
    [Fact]
    public void Exclude_selected_creates_one_enabled_specific_exclusion_rule()
    {
        var viewModel = CreateViewModel();
        IReadOnlyList<WindowPlacementRule>? changed = null;
        viewModel.RulesChanged += rules => changed = rules;
        viewModel.SelectedItem = viewModel.Items[0];

        viewModel.ExcludeSelected();

        var rule = Assert.Single(changed!);
        Assert.True(rule.IsEnabled);
        Assert.Equal(WindowPlacementMode.Exclude, rule.Action);
        Assert.Equal(viewModel.SelectedItem.Identity.ApplicationKey, rule.ApplicationKey);
        Assert.Equal(viewModel.SelectedItem.Identity.WindowClass, rule.WindowClass);
        Assert.Equal(viewModel.SelectedItem.Identity.Kind, rule.WindowKind);
    }

    [Fact]
    public void Repeating_the_same_rule_action_is_idempotent()
    {
        var viewModel = CreateViewModel();
        var changes = new List<IReadOnlyList<WindowPlacementRule>>();
        viewModel.RulesChanged += rules => changes.Add(rules);
        viewModel.SelectedItem = viewModel.Items[0];

        viewModel.ExcludeSelected();
        viewModel.ExcludeSelected();

        Assert.Single(changes);
        Assert.Single(viewModel.Rules);
    }

    [Fact]
    public void Rule_action_replaces_only_the_same_normalized_selector_and_preserves_other_title_rules()
    {
        var identity = Identity("editor.exe", "EditorMain");
        var report = Rule(identity, WindowPlacementMode.RememberLast) with { TitlePattern = "Report*" };
        var invoice = Rule(identity, WindowPlacementMode.FixedZone) with
        {
            TitlePattern = "Invoice*",
            ProfileId = Guid.NewGuid(),
            MonitorStableId = "DISPLAY-1",
            ZoneId = Guid.NewGuid()
        };
        var viewModel = CreateViewModel([report, invoice]);
        viewModel.SelectedItem = viewModel.Items.Single(item => item.Identity == identity);
        viewModel.TitlePattern = "  Report*  ";

        viewModel.ExcludeSelected();

        Assert.Equal(2, viewModel.Rules.Count);
        var replacement = viewModel.Rules.Single(rule => rule.TitlePattern == "Report*");
        Assert.Equal(report.Id, replacement.Id);
        Assert.Equal(WindowPlacementMode.Exclude, replacement.Action);
        Assert.Same(invoice, viewModel.Rules.Single(rule => rule.TitlePattern == "Invoice*"));
    }

    [Fact]
    public void Distinct_title_patterns_are_reported_neutrally_without_a_current_window_title()
    {
        var identity = Identity("editor.exe", "EditorMain");
        var viewModel = CreateViewModel([
            Rule(identity, WindowPlacementMode.Exclude) with { TitlePattern = "Report*" },
            Rule(identity, WindowPlacementMode.RememberLast) with { TitlePattern = "Invoice*" }
        ]);

        var status = viewModel.Items.Single(item => item.Identity == identity).RuleStatusText;

        Assert.DoesNotContain("Konflikt", status, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Titel", status, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Fixed_zone_uses_the_selected_profile_monitor_zone_and_optional_title_pattern()
    {
        var viewModel = CreateViewModel();
        viewModel.SelectedItem = viewModel.Items[0];
        viewModel.SelectedTargetProfile = viewModel.TargetProfiles[0];
        viewModel.SelectedTargetMonitor = viewModel.TargetMonitors[0];
        viewModel.SelectedTargetZone = viewModel.TargetZones[0];
        viewModel.TitlePattern = "Report ?";

        viewModel.FixSelectedToZone();

        var rule = Assert.Single(viewModel.Rules);
        Assert.Equal(WindowPlacementMode.FixedZone, rule.Action);
        Assert.Equal(viewModel.SelectedTargetProfile.Id, rule.ProfileId);
        Assert.Equal(viewModel.SelectedTargetMonitor.Live.Identity.StableId, rule.MonitorStableId);
        Assert.Equal(viewModel.SelectedTargetZone.Id, rule.ZoneId);
        Assert.Equal("Report ?", rule.TitlePattern);
    }

    [Fact]
    public void Forget_selected_raises_the_exact_window_identity()
    {
        var viewModel = CreateViewModel();
        WindowIdentity? forgotten = null;
        viewModel.ForgetRequested += identity => forgotten = identity;
        viewModel.SelectedItem = viewModel.Items[0];

        viewModel.ForgetSelected();

        Assert.Equal(viewModel.SelectedItem.Identity, forgotten);
    }

    [Fact]
    public void Apply_selected_now_raises_the_exact_window_identity()
    {
        var viewModel = CreateViewModel();
        WindowIdentity? applied = null;
        viewModel.ApplyNowRequested += identity => applied = identity;
        viewModel.SelectedItem = viewModel.Items[0];

        viewModel.ApplySelectedNow();

        Assert.Equal(viewModel.SelectedItem.Identity, applied);
    }

    [Fact]
    public void Catalog_replacement_preserves_selection_by_identity_without_keeping_stale_items()
    {
        var viewModel = CreateViewModel();
        var selectedIdentity = viewModel.Items[0].Identity;
        viewModel.SelectedItem = viewModel.Items[0];
        var selectedProfileId = viewModel.SelectedTargetProfile!.Id;
        var selectedMonitorId = viewModel.SelectedTargetMonitor!.Live.Identity.StableId;
        var selectedZoneId = viewModel.SelectedTargetZone!.Id;
        viewModel.TitlePattern = "Ungespeichert*";
        var replacement = Entry(selectedIdentity, "DISPLAY-1", DateTimeOffset.Parse("2026-08-30T12:00:00Z"));

        viewModel.ReplaceCatalog(new(WindowPlacementCatalog.CurrentSchemaVersion, [replacement]));

        Assert.Single(viewModel.Items);
        Assert.NotNull(viewModel.SelectedItem);
        Assert.Equal(selectedIdentity, viewModel.SelectedItem.Identity);
        Assert.Same(replacement, viewModel.SelectedItem.Entry);
        Assert.Equal("Ungespeichert*", viewModel.TitlePattern);
        Assert.Equal(selectedProfileId, viewModel.SelectedTargetProfile!.Id);
        Assert.Equal(selectedMonitorId, viewModel.SelectedTargetMonitor!.Live.Identity.StableId);
        Assert.Equal(selectedZoneId, viewModel.SelectedTargetZone!.Id);
    }

    [Fact]
    public void Missing_fixed_target_and_equal_specificity_conflict_are_visible()
    {
        var identity = Identity("editor.exe", "EditorMain");
        var missing = Rule(identity, WindowPlacementMode.FixedZone) with
        {
            ProfileId = Guid.NewGuid(),
            MonitorStableId = "MISSING",
            ZoneId = Guid.NewGuid()
        };
        var missingViewModel = CreateViewModel([missing]);
        var conflictingViewModel = CreateViewModel([
            Rule(identity, WindowPlacementMode.Exclude),
            Rule(identity, WindowPlacementMode.RememberLast)
        ]);

        Assert.Contains("nicht verfügbar", missingViewModel.Items[0].RuleStatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Konflikt", conflictingViewModel.Items[0].RuleStatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Item_exposes_readable_and_technical_values_without_losing_the_entry()
    {
        var viewModel = CreateViewModel();
        var item = viewModel.Items[0];

        Assert.Equal("editor", item.DisplayName);
        Assert.Equal("Hauptfenster", item.WindowKindText);
        Assert.Contains("Monitor", item.PlacementText, StringComparison.Ordinal);
        Assert.Contains("Voll", item.PlacementText, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(item.LastUpdatedText));
        Assert.Same(item.Entry, viewModel.Catalog.Entries[0]);
    }

    [Fact]
    public void Refresh_resolves_saved_monitor_by_device_name_and_requires_a_current_monitor_for_fixed_targets()
    {
        var zone = new ZoneDefinition(Guid.NewGuid(), "Arbeit", NormalizedRect.Full);
        var savedIdentity = new MonitorIdentity("OLD-ID", "DISPLAY1", "Gespeichert");
        var liveIdentity = new MonitorIdentity("NEW-ID", "DISPLAY1", "Aktueller Monitor");
        var profile = new LayoutProfile(
            Guid.NewGuid(),
            "Standard",
            1,
            [new MonitorLayout(savedIdentity, 1920, 1080, [zone])]);
        var live = new LiveMonitor(liveIdentity, new MonitorWorkArea(0, 0, 1920, 1040), 96, 96, true);
        var monitor = new MonitorChoice(live, profile.Monitors[0]);
        var identity = Identity("editor.exe", "EditorMain");
        var entry = Entry(identity, liveIdentity.StableId, DateTimeOffset.UtcNow) with { ZoneId = zone.Id };
        var fixedRule = Rule(identity, WindowPlacementMode.FixedZone) with
        {
            ProfileId = profile.Id,
            MonitorStableId = liveIdentity.StableId,
            ZoneId = zone.Id
        };
        var viewModel = new WindowPlacementViewModel(
            new(WindowPlacementCatalog.CurrentSchemaVersion, [entry]),
            [fixedRule],
            [profile],
            [monitor]);

        Assert.Single(viewModel.TargetMonitors);
        Assert.Contains("Arbeit", viewModel.Items[0].PlacementText, StringComparison.Ordinal);
        Assert.DoesNotContain("nicht verfügbar", viewModel.Items[0].RuleStatusText, StringComparison.OrdinalIgnoreCase);

        viewModel.Refresh(viewModel.Catalog, viewModel.Rules, [profile], []);

        Assert.Empty(viewModel.TargetMonitors);
        Assert.Contains("nicht verfügbar", viewModel.Items[0].RuleStatusText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Obsolete_fixed_rule_monitor_stays_missing_like_the_engine_but_can_be_retargeted_by_device()
    {
        var zone = new ZoneDefinition(Guid.NewGuid(), "Arbeit", NormalizedRect.Full);
        var savedIdentity = new MonitorIdentity("OLD-ID", "DISPLAY1", "Gespeichert");
        var liveIdentity = new MonitorIdentity("NEW-ID", "DISPLAY1", "Aktueller Monitor");
        var profile = new LayoutProfile(
            Guid.NewGuid(),
            "Standard",
            1,
            [new MonitorLayout(savedIdentity, 1920, 1080, [zone])]);
        var live = new LiveMonitor(liveIdentity, new MonitorWorkArea(0, 0, 1920, 1040), 96, 96, true);
        var monitor = new MonitorChoice(live, profile.Monitors[0]);
        var identity = Identity("editor.exe", "EditorMain");
        var fixedRule = Rule(identity, WindowPlacementMode.FixedZone) with
        {
            ProfileId = profile.Id,
            MonitorStableId = savedIdentity.StableId,
            ZoneId = zone.Id
        };
        var viewModel = new WindowPlacementViewModel(
            new(WindowPlacementCatalog.CurrentSchemaVersion, [
                Entry(identity, liveIdentity.StableId, DateTimeOffset.UtcNow) with { ZoneId = zone.Id }
            ]),
            [fixedRule],
            [profile],
            [monitor]);

        viewModel.SelectedItem = viewModel.Items[0];

        Assert.Contains("nicht verfügbar", viewModel.SelectedItem.RuleStatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Null(viewModel.SelectedTargetMonitor);
        var retarget = Assert.Single(viewModel.TargetMonitors);
        viewModel.SelectedTargetMonitor = retarget;
        viewModel.SelectedTargetZone = Assert.Single(viewModel.TargetZones);

        viewModel.FixSelectedToZone();

        Assert.Equal(liveIdentity.StableId, Assert.Single(viewModel.Rules).MonitorStableId);
    }

    [Fact]
    public void Fixed_rule_monitor_comparison_is_case_sensitive_like_the_engine()
    {
        var identity = Identity("editor.exe", "EditorMain");
        var baseline = CreateViewModel();
        var profile = Assert.Single(baseline.TargetProfiles);
        var zone = Assert.Single(profile.Monitors[0].Zones);
        var rule = Rule(identity, WindowPlacementMode.FixedZone) with
        {
            ProfileId = profile.Id,
            MonitorStableId = "display-1",
            ZoneId = zone.Id
        };
        var viewModel = CreateViewModel([rule]);

        viewModel.SelectedItem = viewModel.Items.Single(item => item.Identity == identity);

        Assert.Contains("nicht verfügbar", viewModel.SelectedItem.RuleStatusText, StringComparison.OrdinalIgnoreCase);
        Assert.Null(viewModel.SelectedTargetMonitor);
    }

    [Fact]
    public void Refresh_replaces_all_sources_and_preserves_item_and_target_selection_by_stable_ids()
    {
        var viewModel = CreateViewModel();
        viewModel.SelectedItem = viewModel.Items[0];
        var identity = viewModel.SelectedItem.Identity;
        var profileId = viewModel.SelectedTargetProfile!.Id;
        var monitorId = viewModel.SelectedTargetMonitor!.Live.Identity.StableId;
        var zoneId = viewModel.SelectedTargetZone!.Id;
        var refreshedEntry = viewModel.Catalog.Entries[0] with { LastUpdatedUtc = DateTimeOffset.UtcNow };
        var refreshedRule = Rule(identity, WindowPlacementMode.Exclude);

        viewModel.Refresh(
            new(WindowPlacementCatalog.CurrentSchemaVersion, [refreshedEntry]),
            [refreshedRule],
            viewModel.TargetProfiles.ToArray(),
            viewModel.TargetMonitors.ToArray());

        Assert.Same(refreshedEntry, viewModel.SelectedItem!.Entry);
        Assert.Equal(profileId, viewModel.SelectedTargetProfile!.Id);
        Assert.Equal(monitorId, viewModel.SelectedTargetMonitor!.Live.Identity.StableId);
        Assert.Equal(zoneId, viewModel.SelectedTargetZone!.Id);
        Assert.Same(refreshedRule, Assert.Single(viewModel.Rules));
    }

    private static WindowPlacementViewModel CreateViewModel(IReadOnlyList<WindowPlacementRule>? rules = null)
    {
        var monitorIdentity = new MonitorIdentity("DISPLAY-1", "DISPLAY1", "Monitor");
        var zone = new ZoneDefinition(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "Voll",
            NormalizedRect.Full);
        var profile = new LayoutProfile(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "Standard",
            1,
            [new MonitorLayout(monitorIdentity, 1920, 1080, [zone])]);
        var monitor = new LiveMonitor(
            monitorIdentity,
            new MonitorWorkArea(0, 0, 1920, 1040),
            96,
            96,
            true);
        var choices = new[] { new MonitorChoice(monitor, profile.Monitors[0]) };
        var catalog = new WindowPlacementCatalog(WindowPlacementCatalog.CurrentSchemaVersion, [
            Entry(Identity("editor.exe", "EditorMain"), monitorIdentity.StableId, DateTimeOffset.Parse("2026-08-30T10:00:00Z")),
            Entry(Identity("other.exe", "Other"), monitorIdentity.StableId, DateTimeOffset.Parse("2026-08-30T09:00:00Z"))
        ]);
        return new WindowPlacementViewModel(catalog, rules ?? [], [profile], choices);
    }

    private static WindowIdentity Identity(string applicationKey, string windowClass) =>
        new(applicationKey, windowClass, WindowKind.MainWindow);

    private static WindowPlacementRule Rule(WindowIdentity identity, WindowPlacementMode action) => new(
        Guid.NewGuid(),
        true,
        identity.ApplicationKey,
        identity.WindowClass,
        identity.Kind,
        null,
        action,
        null,
        null,
        null);

    private static WindowPlacementEntry Entry(
        WindowIdentity identity,
        string monitorStableId,
        DateTimeOffset updated) => new(
            identity,
            monitorStableId,
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            new MonitorWorkArea(0, 0, 1920, 1040),
            new PixelRect(0, 0, 1920, 1040),
            NormalizedRect.Full,
            false,
            updated);
}
