using System.Windows;
using System.Windows.Controls;
using SnapZones.App.ViewModels;
using SnapZones.App.Views;
using SnapZones.Core.AppRules;
using SnapZones.Core.Models;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.Theme;

/// <summary>
/// Prüft den Regeleditor über die echte Oberfläche, weil beide gemeldeten Fehler erst im
/// Zusammenspiel von Liste, Textfeld und Speicherung sichtbar wurden.
/// </summary>
public sealed class AppRuleEditingUiTests
{
    [Fact]
    public void The_add_button_creates_a_visible_second_rule_next_to_an_existing_one()
    {
        WpfThemeHost.Invoke(() =>
        {
            var (window, viewModel) = OpenRulesTab(ExistingRule(out var configuration), configuration);
            var list = Assert.IsType<ListBox>(window.FindName("AppRuleList"));
            var addButton = Assert.IsType<Button>(window.FindName("AppRuleAddButton"));
            var processText = Assert.IsType<TextBox>(window.FindName("AppRuleProcessPathText"));

            addButton.RaiseEvent(new RoutedEventArgs(ButtonBase_ClickEvent));

            Assert.Equal(2, viewModel.AppRules.Rules.Count);
            Assert.Equal(1, list.SelectedIndex);
            Assert.Equal(string.Empty, processText.Text);
            Assert.Contains("mindestens eines", viewModel.AppRules.CriteriaStatus, StringComparison.Ordinal);

            processText.Text = "notepad.exe";

            Assert.Equal(["Discord.exe", "notepad.exe"], viewModel.AppRules.Rules.Select(rule => rule.ProcessPath));
            Assert.Empty(viewModel.AppRules.CriteriaStatus);
            window.Close();
        });
    }

    [Fact]
    public void The_process_path_can_be_cleared_in_favour_of_the_title_pattern()
    {
        WpfThemeHost.Invoke(() =>
        {
            var (window, viewModel) = OpenRulesTab(ExistingRule(out var configuration) with { WindowTitlePattern = "Discord" }, configuration);
            var processText = Assert.IsType<TextBox>(window.FindName("AppRuleProcessPathText"));

            processText.Text = string.Empty;

            var rule = Assert.Single(viewModel.AppRules.Rules);
            Assert.Equal(string.Empty, rule.ProcessPath);
            Assert.Equal("Discord", rule.WindowTitlePattern);
            Assert.True(rule.HasCriteria);
            Assert.Empty(viewModel.AppRules.CriteriaStatus);
            window.Close();
        });
    }

    [Fact]
    public void Clearing_the_last_criterion_is_allowed_but_flagged()
    {
        WpfThemeHost.Invoke(() =>
        {
            var (window, viewModel) = OpenRulesTab(ExistingRule(out var configuration), configuration);
            var processText = Assert.IsType<TextBox>(window.FindName("AppRuleProcessPathText"));
            var hint = Assert.IsType<TextBlock>(window.FindName("AppRuleCriteriaStatusText"));

            processText.Text = string.Empty;

            Assert.Equal(string.Empty, Assert.Single(viewModel.AppRules.Rules).ProcessPath);
            Assert.Contains("greift noch nicht", hint.Text, StringComparison.Ordinal);
            window.Close();
        });
    }

    private static readonly RoutedEvent ButtonBase_ClickEvent = System.Windows.Controls.Primitives.ButtonBase.ClickEvent;

    private static AppRule ExistingRule(out SnapConfiguration configuration)
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

    private static (MainWindow Window, MainViewModel ViewModel) OpenRulesTab(AppRule rule, SnapConfiguration configuration)
    {
        var window = new MainWindow();
        var viewModel = new MainViewModel(configuration with { AppRules = [rule] }, []);
        window.AttachViewModel(viewModel);
        var tabs = Assert.IsType<TabControl>(Assert.IsType<Grid>(window.Content).Children[1]);
        tabs.SelectedItem = tabs.Items.OfType<TabItem>().Single(item => Equals(item.Header, "Regeln"));
        window.Show();
        return (window, viewModel);
    }
}
