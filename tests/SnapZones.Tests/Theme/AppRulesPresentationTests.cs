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

public sealed class AppRulesPresentationTests
{
    [Fact]
    public void Main_window_exposes_the_complete_app_rule_editor_with_neutral_labels()
    {
        WpfThemeHost.Invoke(() =>
        {
            var configuration = ConfigurationSamples.TwoLayouts();
            var window = new MainWindow();
            window.AttachViewModel(new MainViewModel(configuration, []));
            var tabs = Assert.IsType<TabControl>(Assert.IsType<Grid>(window.Content).Children[1]);
            var rulesTab = tabs.Items.OfType<TabItem>().Single(item => Equals(item.Header, "Regeln"));
            tabs.SelectedItem = rulesTab;

            var process = Assert.IsType<TextBox>(window.FindName("AppRuleProcessPathText"));
            var eventSelector = Assert.IsType<ComboBox>(window.FindName("AppRuleEventSelector"));
            var delay = Assert.IsType<TextBox>(window.FindName("AppRuleDelayText"));
            var targetLayout = Assert.IsType<ComboBox>(window.FindName("AppRuleTargetLayoutSelector"));
            var targetZone = Assert.IsType<ComboBox>(window.FindName("AppRuleTargetZoneSelector"));
            var enabled = Assert.IsType<CheckBox>(window.FindName("AppRuleEnabledCheckBox"));
            var status = Assert.IsType<TextBlock>(window.FindName("AppRuleTargetStatusText"));

            Assert.Equal("AppRules.ProcessPath", process.GetBindingExpression(TextBox.TextProperty)!.ParentBinding.Path.Path);
            Assert.Equal("AppRules.Events", eventSelector.GetBindingExpression(ItemsControl.ItemsSourceProperty)!.ParentBinding.Path.Path);
            Assert.Equal("AppRules.DelayMilliseconds", delay.GetBindingExpression(TextBox.TextProperty)!.ParentBinding.Path.Path);
            Assert.Equal("AppRules.SelectedTargetLayout", targetLayout.GetBindingExpression(Selector.SelectedItemProperty)!.ParentBinding.Path.Path);
            Assert.Equal("AppRules.SelectedTargetZone", targetZone.GetBindingExpression(Selector.SelectedItemProperty)!.ParentBinding.Path.Path);
            Assert.Equal("AppRules.IsEnabled", enabled.GetBindingExpression(ToggleButton.IsCheckedProperty)!.ParentBinding.Path.Path);
            Assert.Equal("AppRules.TargetStatus", status.GetBindingExpression(TextBlock.TextProperty)!.ParentBinding.Path.Path);
            Assert.Equal("Prozess auswählen", AutomationProperties.GetName(window.FindName("AppRuleBrowseButton") as Button));
        });
    }

    [Fact]
    public void Rule_editor_groups_its_fields_and_offers_both_ways_to_pick_a_program()
    {
        WpfThemeHost.Invoke(() =>
        {
            var window = new MainWindow();
            window.AttachViewModel(new MainViewModel(ConfigurationSamples.TwoLayouts(), []));

            // Dateidialog fuer nicht laufende Programme, Prozessliste fuer bereits laufende.
            var browse = Assert.IsType<Button>(window.FindName("AppRuleBrowseButton"));
            var running = Assert.IsType<Button>(window.FindName("AppRuleRunningProcessButton"));

            Assert.Equal("Laufenden Prozess auswählen", AutomationProperties.GetName(running));
            Assert.NotEqual(browse.Content, running.Content);

            // Jede Gruppe traegt eine erklaerende Info-Schaltflaeche mit ausformuliertem Hilfetext.
            var groupHelp = new[]
            {
                "AppRuleProcessInfoButton",
                "AppRuleTitleInfoButton",
                "AppRuleWindowClassInfoButton",
                "AppRuleEventInfoButton"
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
    public void Rule_editor_explains_the_selected_event_in_plain_language()
    {
        WpfThemeHost.Invoke(() =>
        {
            var window = new MainWindow();
            var viewModel = new MainViewModel(ConfigurationSamples.TwoLayouts(), []);
            window.AttachViewModel(viewModel);
            var description = Assert.IsType<TextBlock>(window.FindName("AppRuleEventDescriptionText"));

            Assert.Equal(
                "AppRules.SelectedEventDescription",
                description.GetBindingExpression(TextBlock.TextProperty)!.ParentBinding.Path.Path);

            foreach (var value in Enum.GetValues<SnapZones.Core.AppRules.AppRuleEvent>())
            {
                viewModel.AppRules.SelectedEvent = value;

                Assert.Equal(
                    AppRuleEditorViewModel.DescribeEvent(value),
                    viewModel.AppRules.SelectedEventDescription);
                Assert.True(
                    viewModel.AppRules.SelectedEventDescription.Length >= 120,
                    $"Das Ereignis {value} ist nicht ausreichend erklaert.");
            }
        });
    }
}
