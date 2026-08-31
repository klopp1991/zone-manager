using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using SnapZones.App.ViewModels;
using SnapZones.Presentation.ViewModels;
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
            var rulesTab = tabs.Items.OfType<TabItem>().Single(item => Equals(item.Header, "App-Regeln"));
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
}
