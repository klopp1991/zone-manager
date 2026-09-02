using SnapZones.App.ViewModels;
using SnapZones.Core.AppRules;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.ViewModels;

/// <summary>
/// Eine Regel, die nichts bewirkt, muss das in der Liste sagen. Bis zum 02.09.2026 stand eine Regel mit
/// geloeschtem Ziellayout unauffaellig in der Liste; nur ein kleiner Text unter dem Zielfeld verriet es.
/// </summary>
public sealed class AppRuleListItemTests
{
    [Fact]
    public void A_rule_whose_target_layout_is_gone_is_flagged_as_paused()
    {
        var configuration = ConfigurationSamples.TwoLayouts();
        var rule = Rule(configuration.Layouts[0].Id, configuration.Layouts[0].Zones[0].Id) with
        {
            TargetLayoutId = Guid.NewGuid()
        };
        var viewModel = new AppRuleEditorViewModel([rule], configuration.Layouts);

        var item = Assert.Single(viewModel.RuleItems);
        Assert.Equal("Ziellayout fehlt – Regel pausiert", item.Warning);
        Assert.True(item.HasWarning);
        Assert.Same(item, viewModel.SelectedRuleItem);
    }

    [Fact]
    public void A_rule_whose_target_zone_is_gone_is_flagged_as_paused()
    {
        var configuration = ConfigurationSamples.TwoLayouts();
        var rule = Rule(configuration.Layouts[0].Id, Guid.NewGuid());
        var viewModel = new AppRuleEditorViewModel([rule], configuration.Layouts);

        Assert.Equal("Zielzone fehlt – Regel pausiert", Assert.Single(viewModel.RuleItems).Warning);
    }

    [Fact]
    public void A_healthy_rule_carries_no_warning_and_a_disabled_one_says_so()
    {
        var configuration = ConfigurationSamples.TwoLayouts();
        var healthy = Rule(configuration.Layouts[0].Id, configuration.Layouts[0].Zones[0].Id);
        var disabled = healthy with { Id = Guid.NewGuid(), IsEnabled = false };
        var viewModel = new AppRuleEditorViewModel([healthy, disabled], configuration.Layouts);

        Assert.Null(viewModel.RuleItems[0].Warning);
        Assert.False(viewModel.RuleItems[0].HasWarning);
        Assert.Equal("Abgeschaltet", viewModel.RuleItems[1].Warning);
    }

    [Fact]
    public void Deleting_the_target_layout_later_updates_the_warning()
    {
        var configuration = ConfigurationSamples.TwoLayouts();
        var rule = Rule(configuration.Layouts[1].Id, configuration.Layouts[1].Zones[0].Id);
        var viewModel = new AppRuleEditorViewModel([rule], configuration.Layouts);
        Assert.Null(Assert.Single(viewModel.RuleItems).Warning);

        viewModel.RefreshTargets([configuration.Layouts[0]]);

        Assert.Equal("Ziellayout fehlt – Regel pausiert", Assert.Single(viewModel.RuleItems).Warning);
    }

    [Fact]
    public void Selecting_a_list_item_selects_the_rule_behind_it()
    {
        var configuration = ConfigurationSamples.TwoLayouts();
        var first = Rule(configuration.Layouts[0].Id, configuration.Layouts[0].Zones[0].Id);
        var second = first with { Id = Guid.NewGuid(), ProcessPath = "notepad.exe" };
        var viewModel = new AppRuleEditorViewModel([first, second], configuration.Layouts);

        viewModel.SelectedRuleItem = viewModel.RuleItems[1];

        Assert.Equal(second.Id, viewModel.SelectedRule?.Id);
        Assert.Equal("notepad.exe", viewModel.ProcessPath);

        // Ein Neuaufbau der Liste meldet kurz null; die Auswahl bleibt dann stehen.
        viewModel.SelectedRuleItem = null;
        Assert.Equal(second.Id, viewModel.SelectedRule?.Id);
    }

    private static AppRule Rule(Guid layoutId, Guid zoneId) => new(
        Guid.NewGuid(),
        "Discord.exe",
        null,
        null,
        AppRuleEvent.WindowCreated,
        0,
        0,
        50,
        true,
        layoutId,
        zoneId);
}
