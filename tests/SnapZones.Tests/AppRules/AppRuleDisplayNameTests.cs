using SnapZones.App.Services;
using SnapZones.Core.AppRules;
using Xunit;

namespace SnapZones.Tests.AppRules;

public sealed class AppRuleDisplayNameTests
{
    private const string VersionedPath = @"C:\Users\Beispiel\AppData\Local\Discord\app-1.0.9016\Discord.exe";

    private static AppRule Rule(string processPath, string? titlePattern = null) => new(
        Guid.NewGuid(),
        processPath,
        titlePattern,
        null,
        AppRuleEvent.WindowCreated,
        0,
        0,
        50,
        true,
        Guid.NewGuid(),
        Guid.NewGuid());

    [Fact]
    public void The_list_shows_the_window_title_when_one_is_configured()
    {
        Assert.Equal("Posteingang", Rule(@"C:\Program Files\Outlook\outlook.exe", "Posteingang").DisplayName);
    }

    [Fact]
    public void Without_a_title_the_list_shows_the_file_name_instead_of_the_full_path()
    {
        var rule = Rule(VersionedPath);

        Assert.Equal("Discord.exe", rule.DisplayName);
        Assert.Equal("Discord.exe", rule.ProcessFileName);
        Assert.DoesNotContain("app-1.2.3", rule.DisplayName, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Discord.exe", "Discord.exe")]
    [InlineData("\"C:\\Apps\\Teams.exe\"", "Teams.exe")]
    [InlineData("  ", "Kein Programm gewählt")]
    public void The_file_name_is_derived_robustly(string processPath, string expected)
    {
        Assert.Equal(expected, Rule(processPath).ProcessFileName);
    }

    [Fact]
    public void A_whitespace_only_title_falls_back_to_the_file_name()
    {
        Assert.Equal("Discord.exe", Rule(VersionedPath, "   ").DisplayName);
    }

    [Fact]
    public void The_running_process_picker_hands_over_the_update_proof_file_name()
    {
        var entry = new RunningProcessEntry("Discord.exe", VersionedPath, "Discord");

        // Ein Regelziel auf den versionierten Pfad wuerde beim naechsten Update nicht mehr greifen.
        Assert.Equal("Discord.exe", entry.RuleIdentity);
        Assert.NotEqual(entry.ProcessPath, entry.RuleIdentity);
    }

    [Fact]
    public void A_bare_file_name_matches_the_program_regardless_of_its_install_directory()
    {
        var rule = Rule("Discord.exe");
        var beforeUpdate = new AppWindowIdentity(1, VersionedPath, "Discord", "Chrome_WidgetWin_1");
        var afterUpdate = new AppWindowIdentity(
            2,
            @"C:\Users\Beispiel\AppData\Local\Discord\app-1.0.9017\Discord.exe",
            "Discord",
            "Chrome_WidgetWin_1");

        Assert.True(AppRuleMatcher.Matches(rule, beforeUpdate));
        Assert.True(AppRuleMatcher.Matches(rule, afterUpdate));

        // Zum Vergleich: der volle Pfad ueberlebt das Update nicht.
        Assert.False(AppRuleMatcher.Matches(Rule(VersionedPath), afterUpdate));
    }
}
