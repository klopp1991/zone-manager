using ZoneManager.Core.AppRules;
using Xunit;

namespace ZoneManager.Tests.AppRules;

public sealed class AppRuleMatcherTests
{
    private static readonly Guid LayoutId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ZoneId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public void Resolve_matches_a_file_name_against_the_full_process_path()
    {
        var rule = Rule("chrome.exe");
        var window = new AppWindowIdentity(
            42,
            @"C:\Program Files\Google\Chrome\Application\chrome.exe",
            "YouTube - Google Chrome",
            "Chrome_WidgetWin_1");

        var result = AppRuleMatcher.Resolve([rule], AppRuleEvent.WindowCreated, window);

        Assert.Equal(rule.Id, result?.Id);
    }

    [Fact]
    public void Resolve_requires_title_and_class_constraints_to_match()
    {
        var rule = Rule(@"C:\Apps\player.exe") with
        {
            WindowTitlePattern = "*YouTube*",
            WindowClass = "PlayerMainWindow"
        };
        var wrongTitle = new AppWindowIdentity(7, @"C:\Apps\player.exe", "Musik", "PlayerMainWindow");
        var wrongClass = wrongTitle with { WindowTitle = "YouTube", WindowClass = "Dialog" };
        var matching = wrongTitle with { WindowTitle = "Mein YouTube Video" };

        Assert.Null(AppRuleMatcher.Resolve([rule], AppRuleEvent.WindowCreated, wrongTitle));
        Assert.Null(AppRuleMatcher.Resolve([rule], AppRuleEvent.WindowCreated, wrongClass));
        Assert.Equal(rule.Id, AppRuleMatcher.Resolve([rule], AppRuleEvent.WindowCreated, matching)?.Id);
    }

    [Fact]
    public void Resolve_prefers_priority_then_the_more_specific_rule()
    {
        var general = Rule("code.exe") with { Priority = 20 };
        var highPriority = Rule("code.exe") with { Priority = 30 };
        var equallyPrioritisedSpecific = highPriority with
        {
            Id = Guid.NewGuid(),
            WindowClass = "Chrome_WidgetWin_1"
        };
        var window = new AppWindowIdentity(12, @"C:\Tools\code.exe", "Projekt", "Chrome_WidgetWin_1");

        var result = AppRuleMatcher.Resolve(
            [general, highPriority, equallyPrioritisedSpecific],
            AppRuleEvent.WindowCreated,
            window);

        Assert.Equal(equallyPrioritisedSpecific.Id, result?.Id);
    }

    [Fact]
    public void Resolve_ignores_disabled_rules_and_other_events()
    {
        var disabled = Rule("notepad.exe") with { IsEnabled = false };
        var otherEvent = Rule("notepad.exe") with { Event = AppRuleEvent.LayoutActivated };
        var window = new AppWindowIdentity(9, @"C:\Windows\notepad.exe", "Notiz", "Notepad");

        var result = AppRuleMatcher.Resolve([disabled, otherEvent], AppRuleEvent.WindowCreated, window);

        Assert.Null(result);
    }

    private static AppRule Rule(string processPath) => new(
        Guid.NewGuid(),
        processPath,
        WindowTitlePattern: null,
        WindowClass: null,
        AppRuleEvent.WindowCreated,
        DelayMilliseconds: 250,
        RetryCount: 2,
        Priority: 50,
        IsEnabled: true,
        LayoutId,
        ZoneId);
}
