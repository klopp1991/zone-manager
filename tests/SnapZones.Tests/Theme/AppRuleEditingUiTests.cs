using System.Windows;
using System.Windows.Controls;
using SnapZones.App.ViewModels;
using SnapZones.Core.AppRules;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.Theme;

/// <summary>
/// Prüft den Zuordnungseditor über die echte Oberfläche, weil beide gemeldeten Fehler erst im
/// Zusammenspiel von Liste, Textfeld und Speicherung sichtbar wurden.
/// </summary>
public sealed class AppRuleEditingUiTests
{
    [Fact]
    public void Adding_a_rule_creates_a_visible_second_row_and_opens_its_detail()
    {
        WpfThemeHost.Invoke(() =>
        {
            var (window, viewModel) = AppRulesPresentationTests.Open(AppRulesPresentationTests.ExistingRule(out var configuration), configuration);
            try
            {
                var list = Assert.IsType<ItemsControl>(window.FindName("AppRuleList"));

                viewModel.AppRules.AddRule();
                window.UpdateLayout();

                Assert.Equal(2, viewModel.AppRules.Rules.Count);
                Assert.Equal(2, list.Items.Count);
                Assert.True(viewModel.AppRules.RuleItems[1].IsExpanded);
                Assert.False(viewModel.AppRules.RuleItems[0].IsExpanded);
                var processText = UiTree.Find<TextBox>(list, "AppRuleProcessPathText");
                Assert.Equal(string.Empty, processText.Text);
                Assert.Contains("mindestens eines", viewModel.AppRules.CriteriaStatus, StringComparison.Ordinal);

                processText.Text = "notepad.exe";

                Assert.Equal(["Discord.exe", "notepad.exe"], viewModel.AppRules.Rules.Select(rule => rule.ProcessPath));
                Assert.Empty(viewModel.AppRules.CriteriaStatus);
                // Das Detail bleibt dasselbe Element: die Liste wurde nachgefuehrt, nicht neu aufgebaut.
                Assert.Same(processText, UiTree.Find<TextBox>(list, "AppRuleProcessPathText"));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void The_process_path_can_be_cleared_in_favour_of_the_title_pattern()
    {
        WpfThemeHost.Invoke(() =>
        {
            var (window, viewModel) = AppRulesPresentationTests.Open(
                AppRulesPresentationTests.ExistingRule(out var configuration) with { WindowTitlePattern = "Discord" }, configuration);
            try
            {
                viewModel.AppRules.ToggleExpanded(viewModel.AppRules.RuleItems[0]);
                window.UpdateLayout();
                var list = Assert.IsType<ItemsControl>(window.FindName("AppRuleList"));
                var processText = UiTree.Find<TextBox>(list, "AppRuleProcessPathText");

                processText.Text = string.Empty;

                var rule = Assert.Single(viewModel.AppRules.Rules);
                Assert.Equal(string.Empty, rule.ProcessPath);
                Assert.Equal("Discord", rule.WindowTitlePattern);
                Assert.True(rule.HasCriteria);
                Assert.Empty(viewModel.AppRules.CriteriaStatus);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Clearing_the_last_criterion_is_allowed_but_flagged()
    {
        WpfThemeHost.Invoke(() =>
        {
            var (window, viewModel) = AppRulesPresentationTests.Open(AppRulesPresentationTests.ExistingRule(out var configuration), configuration);
            try
            {
                viewModel.AppRules.ToggleExpanded(viewModel.AppRules.RuleItems[0]);
                window.UpdateLayout();
                var list = Assert.IsType<ItemsControl>(window.FindName("AppRuleList"));
                var processText = UiTree.Find<TextBox>(list, "AppRuleProcessPathText");
                foreach (var expander in UiTree.VisualDescendants<Expander>(list))
                {
                    expander.IsExpanded = true;
                }

                window.UpdateLayout();
                var hint = UiTree.Find<TextBlock>(list, "AppRuleCriteriaStatusText");

                processText.Text = string.Empty;

                Assert.Equal(string.Empty, Assert.Single(viewModel.AppRules.Rules).ProcessPath);
                Assert.Contains("greift noch nicht", hint.Text, StringComparison.Ordinal);
                Assert.Equal("Kein Merkmal – Zuordnung wirkungslos", Assert.Single(viewModel.AppRules.RuleItems).Warning);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Removing_a_rule_is_immediate_and_can_be_undone()
    {
        var configuration = ConfigurationSamples.TwoLayouts();
        var first = AppRulesPresentationTests.ExistingRule(out _);
        var second = first with { Id = Guid.NewGuid(), ProcessPath = "notepad.exe" };
        var viewModel = new MainViewModel(configuration with { AppRules = [first, second] }, []);
        var saves = 0;
        viewModel.SaveRequested += _ => saves++;

        var index = viewModel.AppRules.RemoveRule(first);

        Assert.Equal(0, index);
        Assert.Equal(["notepad.exe"], viewModel.Configuration.AppRules.Select(rule => rule.ProcessPath));
        Assert.Equal(1, saves);

        viewModel.AppRules.RestoreRule(first, index);

        Assert.Equal(["Discord.exe", "notepad.exe"], viewModel.Configuration.AppRules.Select(rule => rule.ProcessPath));
        Assert.Equal(2, saves);
    }

    [Fact]
    public void The_dialog_result_becomes_a_complete_rule_that_applies_immediately()
    {
        var configuration = ConfigurationSamples.TwoLayouts();
        var viewModel = new MainViewModel(configuration, []);

        var rule = viewModel.AppRules.AddRule("Explorer.exe", AppRuleEvent.WindowFocused, configuration.Layouts[1].Id, configuration.Layouts[1].Zones[1].Id);

        Assert.Equal("Explorer.exe", rule.ProcessPath);
        Assert.Equal(AppRuleEvent.WindowFocused, rule.Event);
        Assert.Equal(configuration.Layouts[1].Zones[1].Id, rule.TargetZoneId);
        Assert.True(rule.HasCriteria);
        Assert.Single(viewModel.Configuration.AppRules);
        Assert.Equal("Web", viewModel.AppRules.DescribeTarget(rule));
        Assert.Equal("Beim Fokus · Monitor 1 › Abend", viewModel.AppRules.DescribeSubtitle(rule));
    }
}
