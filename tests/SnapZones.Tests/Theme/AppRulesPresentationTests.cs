using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using SnapZones.App.ViewModels;
using SnapZones.App.Views;
using SnapZones.Core.AppRules;
using SnapZones.Core.Models;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.Theme;

/// <summary>
/// Die Seite «Fenster zuordnen»: eine Zeile je Zuordnung mit Schalter, das Detail klappt darunter auf und
/// bindet den Editor des ViewModels.
/// </summary>
public sealed class AppRulesPresentationTests
{
    [Fact]
    public void The_list_shows_each_rule_with_target_switch_and_action()
    {
        WpfThemeHost.Invoke(() =>
        {
            var (window, viewModel) = Open(ExistingRule(out var configuration), configuration);
            try
            {
                var list = Assert.IsType<ItemsControl>(window.FindName("AppRuleList"));
                Assert.Single(list.Items);
                var item = Assert.Single(viewModel.AppRules.RuleItems);
                Assert.Equal("Links", item.TargetLabel);
                Assert.StartsWith("Beim Öffnen · Monitor 1 › Arbeit", item.Subtitle, StringComparison.Ordinal);
                Assert.Equal("Bearbeiten", item.ActionLabel);
                Assert.False(item.IsPaused);

                var toggle = UiTree.VisualDescendants<CheckBox>(list).Single();
                Assert.Same(window.FindResource("ToggleSwitch"), toggle.Style);
                Assert.True(toggle.IsChecked);
                toggle.IsChecked = false;
                toggle.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

                Assert.False(Assert.Single(viewModel.AppRules.Rules).IsEnabled);
                Assert.Equal("Abgeschaltet", Assert.Single(viewModel.AppRules.RuleItems).Warning);
                Assert.Equal(Visibility.Collapsed, Assert.IsType<Grid>(window.FindName("AppRuleEmptyState")).Visibility);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void The_detail_binds_the_editor_and_explains_every_field()
    {
        WpfThemeHost.Invoke(() =>
        {
            var (window, viewModel) = Open(ExistingRule(out var configuration), configuration);
            try
            {
                var list = Assert.IsType<ItemsControl>(window.FindName("AppRuleList"));
                var action = UiTree.VisualDescendants<Button>(list).Single(button => Equals(button.Content, "Bearbeiten"));
                action.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                window.UpdateLayout();

                Assert.True(Assert.Single(viewModel.AppRules.RuleItems).IsExpanded);
                var process = UiTree.Find<TextBox>(list, "AppRuleProcessPathText");
                var eventSelector = UiTree.Find<ComboBox>(list, "AppRuleEventSelector");
                var targetLayout = UiTree.Find<ComboBox>(list, "AppRuleTargetLayoutSelector");
                var targetZone = UiTree.Find<ComboBox>(list, "AppRuleTargetZoneSelector");
                var status = UiTree.Find<TextBlock>(list, "AppRuleTargetStatusText");

                Assert.Equal("ProcessPath", process.GetBindingExpression(TextBox.TextProperty)!.ParentBinding.Path.Path);
                Assert.Equal("Events", eventSelector.GetBindingExpression(ItemsControl.ItemsSourceProperty)!.ParentBinding.Path.Path);
                Assert.Equal("SelectedTargetLayout", targetLayout.GetBindingExpression(Selector.SelectedItemProperty)!.ParentBinding.Path.Path);
                Assert.Equal("SelectedTargetZone", targetZone.GetBindingExpression(Selector.SelectedItemProperty)!.ParentBinding.Path.Path);
                Assert.Equal("TargetStatus", status.GetBindingExpression(TextBlock.TextProperty)!.ParentBinding.Path.Path);
                Assert.Same(viewModel.AppRules, process.DataContext);
                Assert.Equal("Discord.exe", process.Text);
                Assert.Equal("Prozess auswählen", AutomationProperties.GetName(UiTree.Find<Button>(list, "AppRuleBrowseButton")));

                // Eingrenzen und Feinheiten sind zugeklappt; aufgeklappt tragen alle Felder ihr «?».
                foreach (var expander in UiTree.VisualDescendants<Expander>(list))
                {
                    expander.IsExpanded = true;
                }

                window.UpdateLayout();
                foreach (var name in new[] { "AppRuleTitleInfoButton", "AppRuleWindowClassInfoButton", "AppRuleDelayInfoButton" })
                {
                    var help = UiTree.Find<Button>(list, name);
                    Assert.True(Assert.IsType<string>(help.ToolTip).Length >= 120, $"{name} erklaert das Feld nicht ausfuehrlich genug.");
                    Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(help)));
                }

                Assert.Equal("Schliessen", action.Content);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void A_rule_without_a_target_is_marked_paused_and_offers_to_fix_it()
    {
        WpfThemeHost.Invoke(() =>
        {
            var rule = ExistingRule(out var configuration) with { TargetZoneId = Guid.NewGuid() };
            var (window, viewModel) = Open(rule, configuration);
            try
            {
                var item = Assert.Single(viewModel.AppRules.RuleItems);
                Assert.True(item.IsPaused);
                Assert.Equal("Ziel fehlt – Zuordnung pausiert", item.TargetLabel);
                Assert.Equal("Beheben", item.ActionLabel);
                Assert.Equal(1, viewModel.PausedRuleCount);
                Assert.Equal("1 pausiert – Ziel fehlt", viewModel.RuleCountHint);

                viewModel.AppRules.ToggleExpanded(item);
                Assert.True(viewModel.AppRules.IsTargetMissing);
                Assert.Contains("Zielzone gibt es im Layout «Arbeit» nicht mehr", viewModel.AppRules.MissingTargetExplanation, StringComparison.Ordinal);

                viewModel.AppRules.SelectedTargetZone = viewModel.AppRules.TargetZones[0];
                Assert.False(viewModel.AppRules.IsTargetMissing);
                Assert.False(Assert.Single(viewModel.AppRules.RuleItems).IsPaused);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Without_rules_the_page_shows_an_empty_state_with_one_primary_action()
    {
        WpfThemeHost.Invoke(() =>
        {
            var window = new MainWindow();
            window.AttachViewModel(new MainViewModel(ConfigurationSamples.TwoLayouts(), []));
            var empty = Assert.IsType<Grid>(window.FindName("AppRuleEmptyState"));

            Assert.Equal(Visibility.Visible, empty.Visibility);
            Assert.Contains(UiTree.LogicalDescendants<TextBlock>(empty), block => block.Text == "Noch kein Fenster zugeordnet");
            var first = Assert.IsType<Button>(window.FindName("AppRuleAddFirstButton"));
            Assert.Same(window.FindResource("PrimaryButton"), first.Style);
            Assert.DoesNotContain(UiTree.LogicalDescendants<Image>(empty), _ => true);
        });
    }

    [Fact]
    public void Rule_editor_explains_the_selected_event_in_plain_language()
    {
        var viewModel = new MainViewModel(ConfigurationSamples.TwoLayouts(), []);
        foreach (var value in Enum.GetValues<AppRuleEvent>())
        {
            viewModel.AppRules.SelectedEvent = value;

            Assert.Equal(AppRuleEditorViewModel.DescribeEvent(value), viewModel.AppRules.SelectedEventDescription);
            Assert.True(
                viewModel.AppRules.SelectedEventDescription.Length >= 120,
                $"Das Ereignis {value} ist nicht ausreichend erklaert.");
        }
    }

    internal static AppRule ExistingRule(out SnapConfiguration configuration)
    {
        configuration = ConfigurationSamples.TwoLayouts();
        return new AppRule(
            Guid.NewGuid(),
            "Discord.exe",
            null,
            null,
            AppRuleEvent.WindowCreated,
            0,
            0,
            50,
            true,
            configuration.Layouts[0].Id,
            configuration.Layouts[0].Zones[0].Id);
    }

    internal static (MainWindow Window, MainViewModel ViewModel) Open(AppRule rule, SnapConfiguration configuration)
    {
        var window = new MainWindow { Left = -10000 };
        var viewModel = new MainViewModel(configuration with { AppRules = [rule] }, []);
        window.AttachViewModel(viewModel);
        var tabs = Assert.IsType<TabControl>(Assert.IsType<Grid>(window.Content).Children[1]);
        tabs.SelectedItem = tabs.Items.OfType<TabItem>().Single(item => Equals(item.Header, "Fenster zuordnen"));
        window.Show();
        window.UpdateLayout();
        return (window, viewModel);
    }
}
