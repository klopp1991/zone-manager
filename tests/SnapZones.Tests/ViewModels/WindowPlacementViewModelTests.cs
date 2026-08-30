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
    public void Specific_rule_action_replaces_duplicates_and_preserves_other_rules()
    {
        var identity = Identity("editor.exe", "EditorMain");
        var other = Rule(Identity("other.exe", "Other"), WindowPlacementMode.Exclude);
        var duplicateOne = Rule(identity, WindowPlacementMode.RememberLast);
        var duplicateTwo = Rule(identity, WindowPlacementMode.FixedZone) with
        {
            TitlePattern = "Old*",
            ProfileId = Guid.NewGuid(),
            MonitorStableId = "OLD",
            ZoneId = Guid.NewGuid()
        };
        var viewModel = CreateViewModel([duplicateOne, other, duplicateTwo]);
        viewModel.SelectedItem = viewModel.Items.Single(item => item.Identity == identity);
        viewModel.TitlePattern = "Document*";

        viewModel.ExcludeSelected();

        Assert.Equal(2, viewModel.Rules.Count);
        Assert.Same(other, viewModel.Rules[1]);
        var replacement = viewModel.Rules[0];
        Assert.Equal(duplicateOne.Id, replacement.Id);
        Assert.Equal("Document*", replacement.TitlePattern);
        Assert.Equal(WindowPlacementMode.Exclude, replacement.Action);
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
        var replacement = Entry(selectedIdentity, "DISPLAY-1", DateTimeOffset.Parse("2026-08-30T12:00:00Z"));

        viewModel.ReplaceCatalog(new(WindowPlacementCatalog.CurrentSchemaVersion, [replacement]));

        Assert.Single(viewModel.Items);
        Assert.NotNull(viewModel.SelectedItem);
        Assert.Equal(selectedIdentity, viewModel.SelectedItem.Identity);
        Assert.Same(replacement, viewModel.SelectedItem.Entry);
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
