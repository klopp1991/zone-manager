using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using SnapZones.App.ViewModels;
using SnapZones.App.Views;
using SnapZones.Core.AppRules;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.Theme;

/// <summary>
/// Die Seite «In Ruhe lassen» folgt der Seite «Fenster zuordnen»: dieselben Zeilen mit Schalter, dasselbe
/// aufklappbare Detail, dieselbe Hilfe an jedem Feld.
/// </summary>
public sealed class AppExclusionsPresentationTests
{
    [Fact]
    public void Main_window_lists_exclusions_as_rows_bound_to_the_view_model()
    {
        WpfThemeHost.Invoke(() =>
        {
            var configuration = ConfigurationSamples.TwoLayouts() with
            {
                AppExclusions = [new AppExclusion(Guid.NewGuid(), "calc.exe", null, null, true)]
            };
            var window = new MainWindow { Left = -10000 };
            var viewModel = new MainViewModel(configuration, []);
            window.AttachViewModel(viewModel);
            var tabs = Assert.IsType<TabControl>(Assert.IsType<Grid>(window.Content).Children[1]);
            var tab = tabs.Items.OfType<TabItem>().Single(item => Equals(item.Header, "In Ruhe lassen"));
            tabs.SelectedItem = tab;
            window.Show();
            try
            {
                window.UpdateLayout();

                // Die Seite steht unmittelbar hinter «Fenster zuordnen»; beide gehoeren fachlich zusammen.
                Assert.Equal(
                    tabs.Items.OfType<TabItem>().ToList().FindIndex(item => Equals(item.Header, "Fenster zuordnen")) + 1,
                    tabs.Items.IndexOf(tab));

                var list = Assert.IsType<ItemsControl>(window.FindName("AppExclusionList"));
                Assert.Equal("AppExclusions.ExclusionItems", list.GetBindingExpression(ItemsControl.ItemsSourceProperty)!.ParentBinding.Path.Path);
                var item = Assert.Single(viewModel.AppExclusions.ExclusionItems);
                Assert.Equal("Alle Fenster", item.Subtitle);
                Assert.Equal("Eingrenzen …", item.ActionLabel);

                var action = UiTree.VisualDescendants<Button>(list).Single(button => Equals(button.Content, "Eingrenzen …"));
                action.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                window.UpdateLayout();

                var title = UiTree.Find<TextBox>(list, "AppExclusionWindowTitleText");
                var windowClass = UiTree.Find<TextBox>(list, "AppExclusionWindowClassText");
                var status = UiTree.Find<TextBlock>(list, "AppExclusionCriteriaStatusText");
                Assert.Equal("WindowTitlePattern", title.GetBindingExpression(TextBox.TextProperty)!.ParentBinding.Path.Path);
                Assert.Equal("WindowClass", windowClass.GetBindingExpression(TextBox.TextProperty)!.ParentBinding.Path.Path);
                Assert.Equal("CriteriaStatus", status.GetBindingExpression(TextBlock.TextProperty)!.ParentBinding.Path.Path);
                Assert.Same(viewModel.AppExclusions, title.DataContext);

                title.Text = "Vorschau";

                Assert.Equal("Vorschau", Assert.Single(viewModel.Configuration.AppExclusions).WindowTitlePattern);
                Assert.Equal("Titel enthält «Vorschau»", item.Subtitle);

                foreach (var name in new[] { "AppExclusionTitleInfoButton", "AppExclusionWindowClassInfoButton" })
                {
                    var help = UiTree.Find<Button>(list, name);
                    Assert.True(Assert.IsType<string>(help.ToolTip).Length >= 120, $"{name} erklaert das Feld nicht ausfuehrlich genug.");
                    Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(help)));
                }
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Without_exclusions_the_page_shows_an_empty_state_and_hides_the_dashed_row()
    {
        WpfThemeHost.Invoke(() =>
        {
            var window = new MainWindow { Left = -10000 };
            window.AttachViewModel(new MainViewModel(ConfigurationSamples.TwoLayouts(), []));
            window.ShowPage(NavigationPage.Exclusions);
            window.Show();
            try
            {
                window.UpdateLayout();
                Assert.Equal(Visibility.Visible, Assert.IsType<Grid>(window.FindName("AppExclusionEmptyState")).Visibility);
                Assert.Equal(Visibility.Collapsed, Assert.IsType<Button>(window.FindName("AppExclusionAddButton")).Visibility);
                Assert.Same(window.FindResource("PrimaryButton"), Assert.IsType<Button>(window.FindName("AppExclusionAddFirstButton")).Style);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void An_exclusion_without_criteria_is_named_as_ineffective_and_is_not_stored()
    {
        var editor = new AppExclusionEditorViewModel([]);
        var published = new List<IReadOnlyList<AppExclusion>>();
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
    public void Removing_the_last_exclusion_publishes_an_empty_list_and_can_be_undone()
    {
        var editor = new AppExclusionEditorViewModel([]);
        var published = new List<IReadOnlyList<AppExclusion>>();
        editor.ExclusionsChanged += published.Add;
        var exclusion = editor.AddExclusion("notepad.exe");

        var index = editor.RemoveExclusion(exclusion);

        Assert.Equal(0, index);
        Assert.Empty(editor.Exclusions);
        Assert.Empty(published[^1]);
        Assert.Equal(string.Empty, editor.ProcessPath);
        Assert.False(editor.CanDelete);

        editor.RestoreExclusion(exclusion, index);

        Assert.Equal("notepad.exe", Assert.Single(editor.Exclusions).ProcessPath);
        Assert.Equal("notepad.exe", Assert.Single(editor.ExclusionItems).DisplayName);
    }

    [Fact]
    public void Switching_an_exclusion_off_keeps_it_in_the_list_but_names_the_state()
    {
        var editor = new AppExclusionEditorViewModel([new AppExclusion(Guid.NewGuid(), "calc.exe", null, null, true)]);
        var item = Assert.Single(editor.ExclusionItems);

        editor.SetEnabled(item, false);

        Assert.False(Assert.Single(editor.Exclusions).IsEnabled);
        Assert.Equal("Alle Fenster · ausgeschaltet", item.Subtitle);
        Assert.Same(item, Assert.Single(editor.ExclusionItems));
    }
}
