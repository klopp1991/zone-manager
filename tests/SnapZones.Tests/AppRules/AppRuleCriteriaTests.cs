using SnapZones.Core.AppRules;
using Xunit;

namespace SnapZones.Tests.AppRules;

/// <summary>
/// Programm, Titelmuster und Fensterklasse sind gleichrangige Merkmale. Wer eine Regel vom Pfad auf
/// das Titelmuster umstellt, muss den Pfad löschen können, ohne dass die Regel unbrauchbar wird.
/// </summary>
public sealed class AppRuleCriteriaTests
{
    private static AppRule Rule(string processPath, string? title = null, string? windowClass = null) => new(
        Guid.NewGuid(),
        processPath,
        title,
        windowClass,
        AppRuleEvent.WindowCreated,
        0,
        0,
        50,
        true,
        Guid.NewGuid(),
        Guid.NewGuid());

    private static AppWindowIdentity Window(string path = @"C:\Apps\editor.exe", string title = "Posteingang – Buchhaltung", string windowClass = "CabinetWClass") =>
        new(42, path, title, windowClass);

    [Fact]
    public void A_rule_without_a_process_matches_by_title_alone()
    {
        var rule = Rule(string.Empty, "Posteingang");

        Assert.True(AppRuleMatcher.Matches(rule, Window()));
        Assert.True(AppRuleMatcher.Matches(rule, Window(@"C:\Other\outlook.exe")));
        Assert.False(AppRuleMatcher.Matches(rule, Window(title: "Kalender")));
    }

    [Fact]
    public void A_rule_without_a_process_matches_by_window_class_alone()
    {
        var rule = Rule("   ", null, "CabinetWClass");

        Assert.True(AppRuleMatcher.Matches(rule, Window()));
        Assert.False(AppRuleMatcher.Matches(rule, Window(windowClass: "Chrome_WidgetWin_1")));
    }

    [Fact]
    public void A_rule_without_any_criterion_matches_nothing()
    {
        var rule = Rule(string.Empty);

        Assert.False(rule.HasCriteria);
        Assert.False(AppRuleMatcher.Matches(rule, Window()));
        Assert.Null(AppRuleMatcher.Resolve([rule], AppRuleEvent.WindowCreated, Window()));
    }

    [Fact]
    public void A_configured_process_still_has_to_match()
    {
        Assert.False(AppRuleMatcher.Matches(Rule("teams.exe"), Window()));
        Assert.True(AppRuleMatcher.Matches(Rule("editor.exe"), Window()));
    }

    [Fact]
    public void The_more_narrowly_defined_rule_wins_at_equal_priority()
    {
        var broad = Rule(string.Empty, "Posteingang");
        var narrow = Rule("editor.exe", "Posteingang");

        var resolved = AppRuleMatcher.Resolve([broad, narrow], AppRuleEvent.WindowCreated, Window());

        Assert.Equal(narrow.Id, resolved!.Id);
    }

    [Fact]
    public void A_rule_without_a_process_is_still_named_in_the_list()
    {
        Assert.Equal("Posteingang", Rule(string.Empty, "Posteingang").DisplayName);
        Assert.Equal("CabinetWClass", Rule(string.Empty, null, "CabinetWClass").DisplayName);
        Assert.Equal("Neue Regel", Rule(string.Empty).DisplayName);
    }
}
