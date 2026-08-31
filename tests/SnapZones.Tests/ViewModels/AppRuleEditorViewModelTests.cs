using SnapZones.App.ViewModels;
using SnapZones.Core.AppRules;
using SnapZones.Core.Geometry;
using SnapZones.Core.Monitors;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.ViewModels;

public sealed class AppRuleEditorViewModelTests
{
    [Fact]
    public void New_rule_is_persisted_after_process_and_target_are_valid()
    {
        var configuration = ConfigurationSamples.TwoLayouts();
        var viewModel = new AppRuleEditorViewModel([], configuration.Layouts);
        IReadOnlyList<AppRule>? saved = null;
        viewModel.RulesChanged += rules => saved = rules;

        viewModel.AddRule();
        viewModel.ProcessPath = @"C:\Tools\editor.exe";

        var rule = Assert.Single(saved!);
        Assert.Equal(@"C:\Tools\editor.exe", rule.ProcessPath);
        Assert.Equal(AppRuleEvent.WindowCreated, rule.Event);
        Assert.Equal(configuration.Layouts[0].Id, rule.TargetLayoutId);
        Assert.Equal(configuration.Layouts[0].Zones[0].Id, rule.TargetZoneId);
    }

    [Fact]
    public void Existing_rule_updates_event_delay_and_target_automatically()
    {
        var configuration = ConfigurationSamples.TwoLayouts();
        var original = Rule(configuration.Layouts[0].Id, configuration.Layouts[0].Zones[0].Id);
        var viewModel = new AppRuleEditorViewModel([original], configuration.Layouts);
        viewModel.SelectedRule = viewModel.Rules[0];
        IReadOnlyList<AppRule>? saved = null;
        viewModel.RulesChanged += rules => saved = rules;

        viewModel.SelectedEvent = AppRuleEvent.WindowFocused;
        viewModel.DelayMilliseconds = 1200;
        viewModel.SelectedTargetLayout = configuration.Layouts[1];
        viewModel.SelectedTargetZone = configuration.Layouts[1].Zones[1];

        var updated = Assert.Single(saved!);
        Assert.Equal(original.Id, updated.Id);
        Assert.Equal(AppRuleEvent.WindowFocused, updated.Event);
        Assert.Equal(1200, updated.DelayMilliseconds);
        Assert.Equal(configuration.Layouts[1].Id, updated.TargetLayoutId);
        Assert.Equal(configuration.Layouts[1].Zones[1].Id, updated.TargetZoneId);
    }

    [Fact]
    public void Missing_target_keeps_the_rule_and_reports_that_it_is_paused()
    {
        var configuration = ConfigurationSamples.TwoLayouts();
        var rule = Rule(Guid.NewGuid(), Guid.NewGuid());
        var viewModel = new AppRuleEditorViewModel([rule], configuration.Layouts);

        viewModel.SelectedRule = viewModel.Rules[0];

        Assert.Single(viewModel.Rules);
        Assert.Contains("pausiert", viewModel.TargetStatus, StringComparison.OrdinalIgnoreCase);
        Assert.Null(viewModel.SelectedTargetLayout);
        Assert.Null(viewModel.SelectedTargetZone);
    }

    [Fact]
    public void Delete_removes_only_the_selected_rule()
    {
        var configuration = ConfigurationSamples.TwoLayouts();
        var first = Rule(configuration.Layouts[0].Id, configuration.Layouts[0].Zones[0].Id);
        var second = first with { Id = Guid.NewGuid(), ProcessPath = "other.exe" };
        var viewModel = new AppRuleEditorViewModel([first, second], configuration.Layouts)
        {
            SelectedRule = first
        };
        IReadOnlyList<AppRule>? saved = null;
        viewModel.RulesChanged += rules => saved = rules;

        viewModel.DeleteSelectedRule();

        Assert.Equal(second.Id, Assert.Single(saved!).Id);
    }

    [Fact]
    public void Main_view_model_persists_a_valid_rule_automatically()
    {
        var configuration = ConfigurationSamples.TwoLayouts();
        var viewModel = new MainViewModel(configuration, []);
        SnapZones.Core.Models.SnapConfiguration? saved = null;
        viewModel.SaveRequested += current => saved = current;

        viewModel.AppRules.ProcessPath = "editor.exe";

        Assert.Equal("editor.exe", Assert.Single(saved!.AppRules).ProcessPath);
    }

    [Fact]
    public void Main_view_model_uses_the_custom_monitor_name_for_rule_targets()
    {
        var configuration = ConfigurationSamples.TwoLayouts() with
        {
            MonitorNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["stable:DISPLAY-A"] = "Arbeitsmonitor"
            }
        };
        var monitor = new LiveMonitor(
            configuration.Layouts[0].Monitor,
            new MonitorWorkArea(0, 0, 3440, 1440),
            96,
            96,
            true);

        var viewModel = new MainViewModel(configuration, [monitor]);

        Assert.NotEmpty(viewModel.AppRules.TargetLayouts);
        Assert.All(
            viewModel.AppRules.TargetLayouts,
            layout => Assert.Equal("Arbeitsmonitor", layout.UserFacingMonitorName));
    }

    private static AppRule Rule(Guid layoutId, Guid zoneId) => new(
        Guid.NewGuid(),
        "editor.exe",
        null,
        "EditorMain",
        AppRuleEvent.WindowCreated,
        0,
        0,
        50,
        true,
        layoutId,
        zoneId);
}
