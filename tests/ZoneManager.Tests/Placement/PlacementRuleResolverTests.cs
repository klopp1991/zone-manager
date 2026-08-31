using ZoneManager.Core.Placement;
using Xunit;

namespace ZoneManager.Tests.Placement;

public sealed class PlacementRuleResolverTests
{
    [Fact]
    public void Resolve_prefers_a_class_rule_over_an_application_only_exclusion()
    {
        var identity = new WindowIdentity("C:\\Apps\\excel.exe", "XLMAIN", WindowKind.MainWindow);
        var general = Rule(identity, WindowPlacementMode.Exclude);
        var specific = Rule(identity, WindowPlacementMode.FixedZone) with { WindowClass = "XLMAIN", ZoneId = Guid.NewGuid() };

        var result = PlacementRuleResolver.Resolve(identity, "Budget.xlsx - Excel", [general, specific]);

        Assert.False(result.HasConflict);
        Assert.Equal(specific.Id, result.Rule!.Id);
    }

    [Fact]
    public void Resolve_reports_a_conflict_for_two_equally_specific_rules()
    {
        var identity = new WindowIdentity("C:\\Apps\\excel.exe", "XLMAIN", WindowKind.MainWindow);
        var first = Rule(identity, WindowPlacementMode.Exclude) with { WindowClass = "XLMAIN" };
        var second = Rule(identity, WindowPlacementMode.RememberLast) with { WindowClass = "XLMAIN" };

        var result = PlacementRuleResolver.Resolve(identity, "Excel", [first, second]);

        Assert.True(result.HasConflict);
        Assert.Null(result.Rule);
    }

    [Fact]
    public void Resolve_matches_application_key_ordinal_ignore_case()
    {
        var identity = new WindowIdentity("C:\\Apps\\EXCEL.EXE", "XLMAIN", WindowKind.MainWindow);
        var rule = Rule(identity, WindowPlacementMode.Exclude) with { ApplicationKey = "c:\\apps\\excel.exe" };

        var result = PlacementRuleResolver.Resolve(identity, "Excel", [rule]);

        Assert.Same(rule, result.Rule);
    }

    [Fact]
    public void Resolve_requires_optional_class_and_kind_to_match()
    {
        var identity = new WindowIdentity("excel.exe", "XLMAIN", WindowKind.MainWindow);
        var classRule = Rule(identity, WindowPlacementMode.Exclude) with { WindowClass = "OTHER" };
        var kindRule = Rule(identity, WindowPlacementMode.FixedZone) with { WindowKind = WindowKind.Dialog };

        var result = PlacementRuleResolver.Resolve(identity, "Excel", [classRule, kindRule]);

        Assert.False(result.HasConflict);
        Assert.Null(result.Rule);
    }

    [Fact]
    public void Resolve_supports_escaped_wildcard_title_patterns_case_insensitively()
    {
        var identity = new WindowIdentity("excel.exe", "XLMAIN", WindowKind.MainWindow);
        var rule = Rule(identity, WindowPlacementMode.FixedZone) with { TitlePattern = "Budget-*.xlsx?" };

        var result = PlacementRuleResolver.Resolve(identity, "budget-Q1.xlsx1", [rule]);

        Assert.Same(rule, result.Rule);
    }

    [Fact]
    public void Resolve_ignores_disabled_rules()
    {
        var identity = new WindowIdentity("excel.exe", "XLMAIN", WindowKind.MainWindow);
        var rule = Rule(identity, WindowPlacementMode.Exclude) with { IsEnabled = false };

        var result = PlacementRuleResolver.Resolve(identity, "Excel", [rule]);

        Assert.False(result.HasConflict);
        Assert.Null(result.Rule);
    }

    [Fact]
    public void Resolve_only_returns_rules_for_the_requested_trigger()
    {
        var identity = new WindowIdentity("excel.exe", "XLMAIN", WindowKind.MainWindow);
        var focused = Rule(identity, WindowPlacementMode.FixedZone) with
        {
            Trigger = WindowPlacementTrigger.WindowFocused
        };

        var createdResult = PlacementRuleResolver.Resolve(identity, "Excel", [focused]);
        var focusedResult = PlacementRuleResolver.Resolve(
            identity,
            "Excel",
            WindowPlacementTrigger.WindowFocused,
            [focused]);

        Assert.Null(createdResult.Rule);
        Assert.Same(focused, focusedResult.Rule);
    }

    private static WindowPlacementRule Rule(WindowIdentity identity, WindowPlacementMode mode) => new(
        Guid.NewGuid(), true, identity.ApplicationKey,
        null, null, null, mode, null, null, null);
}

