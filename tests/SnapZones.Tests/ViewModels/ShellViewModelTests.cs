using SnapZones.App.ViewModels;
using SnapZones.Core.Editor;
using SnapZones.Core.Layouts;
using SnapZones.Core.Models;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.ViewModels;

/// <summary>
/// Die Bausteine der Oberflaeche v2 im Hauptmodell: Suche, Rueckgaengig-Toast, Zeitpunkt der letzten
/// Speicherung, Loeschen und Wiederherstellen eines Layouts, Verbinden zweier Zonen.
/// </summary>
public sealed class ShellViewModelTests
{
    [Fact]
    public void The_search_index_finds_settings_by_label_path_and_synonym()
    {
        Assert.Contains(SettingsSearchIndex.Search("Deckkraft"), result => result.Label == "Deckkraft der Zonen" && result.BehaviourTab == 1);
        Assert.Contains(SettingsSearchIndex.Search("Darstellung"), result => result.Page == NavigationPage.Behaviour);
        Assert.Contains(SettingsSearchIndex.Search("dunkel"), result => result.Label.StartsWith("Erscheinungsbild", StringComparison.Ordinal) && result.Page == NavigationPage.Program);
        Assert.Contains(SettingsSearchIndex.Search("Hauptzone"), result => result.Label == "Auffangzone");
        Assert.Empty(SettingsSearchIndex.Search("   "));
        Assert.Empty(SettingsSearchIndex.Search("xyzzy"));
        Assert.True(SettingsSearchIndex.Search("e").Count <= SettingsSearchIndex.MaximumResults);
    }

    [Fact]
    public void A_toast_carries_its_undo_and_a_new_toast_replaces_the_old_one()
    {
        var viewModel = new MainViewModel(ConfigurationSamples.TwoLayouts(), []);
        var undone = 0;

        viewModel.ShowToast("Zuordnung entfernt.", () => undone++);
        Assert.True(viewModel.IsToastVisible);
        Assert.True(viewModel.CanUndoToast);
        Assert.Equal("Zuordnung entfernt.", viewModel.ToastText);

        viewModel.ShowToast("Nur ein Hinweis.");
        Assert.False(viewModel.CanUndoToast);

        viewModel.UndoToast();
        Assert.Equal(0, undone);
        Assert.False(viewModel.IsToastVisible);

        viewModel.ShowToast("Zone entfernt.", () => undone++);
        viewModel.UndoToast();
        Assert.Equal(1, undone);
        Assert.False(viewModel.IsToastVisible);
    }

    [Fact]
    public void Marking_a_save_names_the_action_and_the_time_and_feeds_the_overview()
    {
        var viewModel = new MainViewModel(ConfigurationSamples.TwoLayouts(), []);
        Assert.Equal("In dieser Sitzung noch nichts geändert", viewModel.LastSavedText);

        viewModel.RenameSelectedLayout("Büro");
        Assert.Equal("Wird gespeichert …", viewModel.StatusMessage);

        viewModel.MarkSaved();

        Assert.StartsWith("✓ Gespeichert · Layout in «Büro» umbenannt (", viewModel.StatusMessage, StringComparison.Ordinal);
        Assert.Equal("Zuletzt gespeichert gerade eben", viewModel.LastSavedText);
        var now = new DateTimeOffset(2026, 9, 5, 10, 0, 0, TimeSpan.Zero);
        Assert.Equal("Zuletzt gespeichert vor 2 Minuten", MainViewModel.DescribeLastSaved(now.AddMinutes(-2), now));
        Assert.Equal("Zuletzt gespeichert vor einer Stunde", MainViewModel.DescribeLastSaved(now.AddMinutes(-70), now));
    }

    [Fact]
    public void Deleting_a_layout_returns_it_so_the_toast_can_restore_it()
    {
        var viewModel = new MainViewModel(ConfigurationSamples.TwoLayouts(), []);
        viewModel.EditLayout(viewModel.Layouts.Single(layout => layout.Name == "Abend").Id);

        var deleted = viewModel.DeleteSelectedLayout();

        Assert.NotNull(deleted);
        Assert.Equal("Abend", deleted.Name);
        Assert.Single(viewModel.Layouts);

        viewModel.RestoreLayout(deleted);

        Assert.Equal(["Arbeit", "Abend"], viewModel.Layouts.Select(layout => layout.Name));
        Assert.Equal("Abend", viewModel.SelectedLayout?.Name);
        Assert.False(viewModel.SelectedLayout!.IsActive);
        Assert.Equal(2, viewModel.LayoutCount);
    }

    [Fact]
    public void Restoring_a_layout_whose_name_is_taken_appends_a_suffix_and_activates_when_nothing_is_active()
    {
        var configuration = ConfigurationSamples.TwoLayouts();
        var service = new LayoutService(configuration);
        var copy = configuration.Layouts[1] with { Id = Guid.NewGuid() };

        var restored = service.RestoreLayout(copy);

        Assert.Equal("Abend (2)", restored.Name);
        Assert.False(restored.IsActive);

        var lonely = new LayoutService(configuration with { Layouts = [] });
        Assert.True(lonely.RestoreLayout(configuration.Layouts[1]).IsActive);
    }

    [Fact]
    public void Editing_switches_the_layout_without_activating_it_while_activation_follows_the_overview()
    {
        var viewModel = new MainViewModel(ConfigurationSamples.TwoLayouts(), []);
        var evening = viewModel.Layouts.Single(layout => layout.Name == "Abend");

        viewModel.EditLayout(evening.Id);

        Assert.Equal("Abend", viewModel.SelectedLayout?.Name);
        Assert.True(viewModel.Configuration.Layouts.Single(layout => layout.Name == "Arbeit").IsActive);

        viewModel.ActivateLayout(evening.Id);

        Assert.True(viewModel.Configuration.Layouts.Single(layout => layout.Name == "Abend").IsActive);
        Assert.Equal("Abend", viewModel.SelectedLayout?.Name);
    }

    [Fact]
    public void Two_zones_sharing_a_full_edge_can_be_merged_into_one()
    {
        var viewModel = new MainViewModel(ConfigurationSamples.TwoLayouts(), []);
        var editor = viewModel.Editor!;
        var left = editor.Zones[0];
        var right = editor.Zones[1];

        Assert.Equal([right.Id], editor.MergeableNeighbours(left.Id).Select(zone => zone.Id));
        Assert.True(editor.MergeZones(left.Id, right.Id));

        var merged = Assert.Single(editor.Zones);
        Assert.Equal(NormalizedRect.Full, merged.Bounds);
        Assert.Equal("Links", merged.Name);
        Assert.True(editor.CanUndo);

        Assert.True(editor.Undo());
        Assert.Equal(2, editor.Zones.Count);
        Assert.Empty(editor.MergeableNeighbours(Guid.NewGuid()));
    }

    [Fact]
    public void Adding_from_a_template_creates_a_numbered_layout_with_the_template_zones()
    {
        var viewModel = new MainViewModel(ConfigurationSamples.TwoLayouts(), []);

        viewModel.AddLayoutFromTemplate(LayoutTemplate.ThreeColumns);

        Assert.Equal("Layout 1", viewModel.SelectedLayout?.Name);
        Assert.Equal(3, viewModel.Editor!.Zones.Count);

        viewModel.AddEmptyLayout();

        Assert.Equal("Layout 2", viewModel.SelectedLayout?.Name);
        Assert.Equal(NormalizedRect.Full, Assert.Single(viewModel.Editor!.Zones).Bounds);
    }
}
