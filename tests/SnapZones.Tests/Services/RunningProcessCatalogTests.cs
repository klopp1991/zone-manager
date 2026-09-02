using SnapZones.App.Services;
using Xunit;

namespace SnapZones.Tests.Services;

public sealed class RunningProcessCatalogTests
{
    [Fact]
    public void Normalize_keeps_one_entry_per_program_and_sorts_alphabetically()
    {
        var entries = new[]
        {
            new RunningProcessEntry("Teams.exe", @"C:\Apps\Teams.exe", "Chat"),
            new RunningProcessEntry("Teams.exe", @"C:\Apps\Teams.exe", "Besprechung läuft"),
            new RunningProcessEntry("Explorer.exe", @"C:\Windows\Explorer.exe", "Dokumente")
        };

        var normalized = RunningProcessCatalog.Normalize(entries);

        Assert.Equal(["Explorer.exe", "Teams.exe"], normalized.Select(entry => entry.DisplayName));
        Assert.Equal("Besprechung läuft", normalized.Single(entry => entry.DisplayName == "Teams.exe").WindowTitle);
    }

    [Fact]
    public void Normalize_prefers_the_entry_that_carries_a_full_path()
    {
        var entries = new[]
        {
            new RunningProcessEntry("Notepad.exe", "Notepad.exe", "Unbenannt"),
            new RunningProcessEntry("Notepad.exe", @"C:\Windows\System32\Notepad.exe", string.Empty)
        };

        var normalized = RunningProcessCatalog.Normalize(entries);

        var single = Assert.Single(normalized);
        Assert.Equal(@"C:\Windows\System32\Notepad.exe", single.ProcessPath);
        Assert.True(single.HasFullPath);
    }

    [Fact]
    public void Normalize_drops_entries_without_a_usable_name()
    {
        var entries = new[]
        {
            new RunningProcessEntry(" ", @"C:\Apps\Leer.exe", string.Empty),
            new RunningProcessEntry("Gut.exe", @"C:\Apps\Gut.exe", string.Empty)
        };

        var single = Assert.Single(RunningProcessCatalog.Normalize(entries));

        Assert.Equal("Gut.exe", single.DisplayName);
    }

    [Theory]
    [InlineData("teams", "Teams.exe")]
    [InlineData("WINDOWS", "Explorer.exe")]
    [InlineData("Dokumente", "Explorer.exe")]
    public void Filter_matches_name_path_and_window_title_case_insensitively(string query, string expected)
    {
        var entries = RunningProcessCatalog.Normalize(
        [
            new RunningProcessEntry("Teams.exe", @"C:\Apps\Teams.exe", "Chat"),
            new RunningProcessEntry("Explorer.exe", @"C:\Windows\Explorer.exe", "Dokumente")
        ]);

        var match = Assert.Single(RunningProcessCatalog.Filter(entries, query));

        Assert.Equal(expected, match.DisplayName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Filter_without_a_query_returns_every_entry(string? query)
    {
        var entries = RunningProcessCatalog.Normalize(
        [
            new RunningProcessEntry("Teams.exe", @"C:\Apps\Teams.exe", string.Empty),
            new RunningProcessEntry("Explorer.exe", @"C:\Windows\Explorer.exe", string.Empty)
        ]);

        Assert.Equal(2, RunningProcessCatalog.Filter(entries, query).Count);
    }
}
