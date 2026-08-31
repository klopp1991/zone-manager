using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using SnapZones.App.ViewModels;
using SnapZones.App.Views;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.Theme;

/// <summary>
/// Die Seite «Ausschlüsse» folgt denselben Richtlinien wie die Seite «Regeln»: dieselbe Texthierarchie,
/// dieselben zwei Wege zur Programmauswahl und ausformulierte Hilfe an jeder Gruppe.
/// </summary>
public sealed class AppExclusionsPresentationTests
{
    [Fact]
    public void Main_window_exposes_the_exclusion_editor_bound_to_the_view_model()
    {
        WpfThemeHost.Invoke(() =>
        {
            var window = new MainWindow();
            window.AttachViewModel(new MainViewModel(ConfigurationSamples.TwoLayouts(), []));
            var tabs = Assert.IsType<TabControl>(Assert.IsType<Grid>(window.Content).Children[1]);
            var tab = tabs.Items.OfType<TabItem>().Single(item => Equals(item.Header, "Ausschlüsse"));
            tabs.SelectedItem = tab;

            // Die Seite steht unmittelbar hinter den Regeln; beide gehoeren fachlich zusammen.
            Assert.Equal(
                tabs.Items.OfType<TabItem>().ToList().FindIndex(item => Equals(item.Header, "Regeln")) + 1,
                tabs.Items.IndexOf(tab));

            var process = Assert.IsType<TextBox>(window.FindName("AppExclusionProcessPathText"));
            var title = Assert.IsType<TextBox>(window.FindName("AppExclusionWindowTitleText"));
            var windowClass = Assert.IsType<TextBox>(window.FindName("AppExclusionWindowClassText"));
            var enabled = Assert.IsType<CheckBox>(window.FindName("AppExclusionEnabledCheckBox"));
            var status = Assert.IsType<TextBlock>(window.FindName("AppExclusionCriteriaStatusText"));
            var list = Assert.IsType<ListBox>(window.FindName("AppExclusionList"));

            Assert.Equal(
                "AppExclusions.ProcessPath",
                process.GetBindingExpression(TextBox.TextProperty)!.ParentBinding.Path.Path);
            Assert.Equal(
                "AppExclusions.WindowTitlePattern",
                title.GetBindingExpression(TextBox.TextProperty)!.ParentBinding.Path.Path);
            Assert.Equal(
                "AppExclusions.WindowClass",
                windowClass.GetBindingExpression(TextBox.TextProperty)!.ParentBinding.Path.Path);
            Assert.Equal(
                "AppExclusions.IsEnabled",
                enabled.GetBindingExpression(ToggleButton.IsCheckedProperty)!.ParentBinding.Path.Path);
            Assert.Equal(
                "AppExclusions.CriteriaStatus",
                status.GetBindingExpression(TextBlock.TextProperty)!.ParentBinding.Path.Path);
            Assert.Equal(
                "AppExclusions.Exclusions",
                list.GetBindingExpression(ItemsControl.ItemsSourceProperty)!.ParentBinding.Path.Path);
        });
    }

    [Fact]
    public void Exclusion_editor_offers_both_ways_to_pick_a_program_and_explains_every_group()
    {
        WpfThemeHost.Invoke(() =>
        {
            var window = new MainWindow();
            window.AttachViewModel(new MainViewModel(ConfigurationSamples.TwoLayouts(), []));

            var browse = Assert.IsType<Button>(window.FindName("AppExclusionBrowseButton"));
            var running = Assert.IsType<Button>(window.FindName("AppExclusionRunningProcessButton"));

            Assert.Equal("Prozess auswählen", AutomationProperties.GetName(browse));
            Assert.Equal("Laufenden Prozess auswählen", AutomationProperties.GetName(running));
            Assert.NotEqual(browse.Content, running.Content);

            var groupHelp = new[]
            {
                "AppExclusionProcessInfoButton",
                "AppExclusionTitleInfoButton",
                "AppExclusionWindowClassInfoButton"
            };
            Assert.All(groupHelp, name =>
            {
                var button = Assert.IsType<Button>(window.FindName(name));
                var help = Assert.IsType<string>(button.ToolTip);
                Assert.True(help.Length >= 120, $"{name} erklaert das Feld nicht ausfuehrlich genug.");
                Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(button)));
            });
        });
    }

    [Fact]
    public void Add_and_delete_buttons_stay_the_same_height_and_delete_needs_a_selection()
    {
        WpfThemeHost.Invoke(() =>
        {
            var window = new MainWindow();
            window.AttachViewModel(new MainViewModel(ConfigurationSamples.TwoLayouts(), []));
            var add = Assert.IsType<Button>(window.FindName("AppExclusionAddButton"));
            var delete = Assert.IsType<Button>(window.FindName("AppExclusionDeleteButton"));

            window.Measure(new Size(1600, 1200));
            window.Arrange(new Rect(0, 0, 1600, 1200));

            Assert.Equal(add.DesiredSize.Height, delete.DesiredSize.Height, 3);
            Assert.Equal(
                "AppExclusions.CanDelete",
                delete.GetBindingExpression(UIElement.IsEnabledProperty)!.ParentBinding.Path.Path);
        });
    }

    [Fact]
    public void An_exclusion_without_criteria_is_named_as_ineffective_and_is_not_stored()
    {
        var editor = new AppExclusionEditorViewModel([]);
        var published = new List<IReadOnlyList<SnapZones.Core.AppRules.AppExclusion>>();
        editor.ExclusionsChanged += published.Add;

        Assert.False(string.IsNullOrWhiteSpace(editor.CriteriaStatus));
        Assert.Empty(editor.Exclusions);
        Assert.Empty(published);

        editor.ProcessPath = "notepad.exe";

        Assert.Equal(string.Empty, editor.CriteriaStatus);
        Assert.Equal("notepad.exe", Assert.Single(editor.Exclusions).ProcessPath);
        Assert.Single(published);
    }

    [Fact]
    public void Deleting_the_last_exclusion_clears_the_editor_and_publishes_an_empty_list()
    {
        var editor = new AppExclusionEditorViewModel([]);
        var published = new List<IReadOnlyList<SnapZones.Core.AppRules.AppExclusion>>();
        editor.ExclusionsChanged += published.Add;
        editor.ProcessPath = "notepad.exe";

        editor.DeleteSelectedExclusion();

        Assert.Empty(editor.Exclusions);
        Assert.Empty(published[^1]);
        Assert.Equal(string.Empty, editor.ProcessPath);
        Assert.False(editor.CanDelete);
    }
}
