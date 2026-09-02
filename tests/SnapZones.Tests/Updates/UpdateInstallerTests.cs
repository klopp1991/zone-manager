using System.Text.Json;
using SnapZones.Core.Updates;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.Updates;

/// <summary>
/// Der Austausch der laufenden Programmdatei darf nie einen Zustand hinterlassen, in dem gar kein
/// lauffähiges Programm mehr am Platz liegt.
/// </summary>
public sealed class UpdateInstallerTests
{
    private static readonly DateTimeOffset Moment = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_running_file_is_moved_aside_and_the_new_one_takes_its_place()
    {
        using var directory = new TemporaryDirectory();
        var executable = Path.Combine(directory.Path, "ZoneManager.exe");
        var download = executable + ".download";
        File.WriteAllText(executable, "alt");
        File.WriteAllText(download, "neu");

        var result = UpdateInstaller.Replace(executable, download, Moment);

        Assert.Equal(UpdateInstallStatus.Applied, result.Status);
        Assert.Equal("neu", File.ReadAllText(executable));
        Assert.False(File.Exists(download));
        Assert.Equal("alt", File.ReadAllText(UpdateInstaller.BuildSupersededPath(executable, Moment)));
    }

    [Fact]
    public void A_first_installation_without_a_previous_file_simply_works()
    {
        using var directory = new TemporaryDirectory();
        var executable = Path.Combine(directory.Path, "ZoneManager.exe");
        var download = executable + ".download";
        File.WriteAllText(download, "neu");

        var result = UpdateInstaller.Replace(executable, download, Moment);

        Assert.Equal(UpdateInstallStatus.Applied, result.Status);
        Assert.Equal("neu", File.ReadAllText(executable));
        Assert.False(File.Exists(UpdateInstaller.BuildSupersededPath(executable, Moment)));
    }

    [Fact]
    public void A_missing_download_changes_nothing()
    {
        using var directory = new TemporaryDirectory();
        var executable = Path.Combine(directory.Path, "ZoneManager.exe");
        File.WriteAllText(executable, "alt");

        var result = UpdateInstaller.Replace(executable, executable + ".download", Moment);

        Assert.Equal(UpdateInstallStatus.DownloadFailed, result.Status);
        Assert.Equal("alt", File.ReadAllText(executable));
    }

    [Fact]
    public void A_failed_second_step_puts_the_running_file_back()
    {
        // Die geladene Datei ist gesperrt, waehrend die alte bereits beiseitegeschoben wurde. Danach
        // muss die alte Programmdatei wieder an ihrem Platz liegen, sonst bliebe gar keine uebrig.
        using var directory = new TemporaryDirectory();
        var executable = Path.Combine(directory.Path, "ZoneManager.exe");
        var download = executable + ".download";
        File.WriteAllText(executable, "alt");
        File.WriteAllText(download, "neu");

        UpdateInstallResult result;
        using (var _ = new FileStream(download, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            result = UpdateInstaller.Replace(executable, download, Moment);
        }

        Assert.Equal(UpdateInstallStatus.ReplaceFailed, result.Status);
        Assert.Equal("alt", File.ReadAllText(executable));
        Assert.False(File.Exists(UpdateInstaller.BuildSupersededPath(executable, Moment)));
    }

    [Fact]
    public void A_blocked_target_leaves_everything_untouched()
    {
        using var directory = new TemporaryDirectory();
        var blocked = Path.Combine(directory.Path, "Blockiert.exe");
        Directory.CreateDirectory(blocked);
        var download = blocked + ".download";
        File.WriteAllText(download, "neu");

        var result = UpdateInstaller.Replace(blocked, download, Moment);

        Assert.Equal(UpdateInstallStatus.ReplaceFailed, result.Status);
        Assert.True(Directory.Exists(blocked));
    }

    [Fact]
    public void Superseded_files_are_removed_on_the_next_start_and_the_program_itself_is_kept()
    {
        using var directory = new TemporaryDirectory();
        var executable = Path.Combine(directory.Path, "ZoneManager.exe");
        File.WriteAllText(executable, "aktuell");
        File.WriteAllText(UpdateInstaller.BuildSupersededPath(executable, Moment), "alt");
        File.WriteAllText(UpdateInstaller.BuildSupersededPath(executable, Moment.AddDays(-1)), "aelter");
        var unrelated = Path.Combine(directory.Path, "settings.json");
        File.WriteAllText(unrelated, "{}");

        var removed = UpdateInstaller.RemoveSupersededFiles(executable);

        Assert.Equal(2, removed);
        Assert.True(File.Exists(executable));
        Assert.True(File.Exists(unrelated));
    }

    [Fact]
    public void The_helper_is_replaced_together_with_the_application()
    {
        using var directory = new TemporaryDirectory();
        var executable = Path.Combine(directory.Path, "ZoneManager.exe");
        var helper = Path.Combine(directory.Path, "ZoneManager.Helper.exe");
        File.WriteAllText(executable, "alt");
        File.WriteAllText(helper, "alter helfer");
        File.WriteAllText(executable + ".download", "neu");
        File.WriteAllText(helper + ".download", "neuer helfer");

        var result = UpdateInstaller.ReplaceAll(
            executable,
            executable + ".download",
            helper,
            helper + ".download",
            Moment);

        Assert.Equal(UpdateInstallStatus.Applied, result.Status);
        Assert.Equal("neu", File.ReadAllText(executable));
        Assert.Equal("neuer helfer", File.ReadAllText(helper));
        Assert.Equal("alt", File.ReadAllText(UpdateInstaller.BuildSupersededPath(executable, Moment)));
        Assert.Equal("alter helfer", File.ReadAllText(UpdateInstaller.BuildSupersededPath(helper, Moment)));
    }

    [Fact]
    public void A_failed_application_swap_takes_the_helper_back()
    {
        // Sonst bliebe die schlechteste aller Paarungen zurueck: alte Anwendung, neuer Helfer.
        using var directory = new TemporaryDirectory();
        var executable = Path.Combine(directory.Path, "ZoneManager.exe");
        var helper = Path.Combine(directory.Path, "ZoneManager.Helper.exe");
        File.WriteAllText(executable, "alt");
        File.WriteAllText(helper, "alter helfer");
        File.WriteAllText(executable + ".download", "neu");
        File.WriteAllText(helper + ".download", "neuer helfer");

        // Eine geoeffnete Datei laesst sich nicht beiseiteschieben; das ist der Fehlerfall im Feld.
        using (new FileStream(executable, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var result = UpdateInstaller.ReplaceAll(
                executable,
                executable + ".download",
                helper,
                helper + ".download",
                Moment);

            Assert.Equal(UpdateInstallStatus.ReplaceFailed, result.Status);
        }

        Assert.Equal("alt", File.ReadAllText(executable));
        Assert.Equal("alter helfer", File.ReadAllText(helper));
        Assert.False(File.Exists(UpdateInstaller.BuildSupersededPath(helper, Moment)));
    }

    [Fact]
    public void Without_a_helper_only_the_application_is_replaced()
    {
        using var directory = new TemporaryDirectory();
        var executable = Path.Combine(directory.Path, "ZoneManager.exe");
        var helper = Path.Combine(directory.Path, "ZoneManager.Helper.exe");
        File.WriteAllText(executable, "alt");
        File.WriteAllText(helper, "unveraendert");
        File.WriteAllText(executable + ".download", "neu");

        var result = UpdateInstaller.ReplaceAll(executable, executable + ".download", null, null, Moment);

        Assert.Equal(UpdateInstallStatus.Applied, result.Status);
        Assert.Equal("neu", File.ReadAllText(executable));
        Assert.Equal("unveraendert", File.ReadAllText(helper));
    }

    [Fact]
    public void The_helper_is_looked_for_next_to_the_application()
    {
        var executable = Path.Combine("C:", "Programme", "ZoneManager", "ZoneManager.exe");

        Assert.Equal(
            Path.Combine("C:", "Programme", "ZoneManager", "ZoneManager.Helper.exe"),
            UpdateInstaller.BuildHelperPath(executable));
    }

    [Fact]
    public void A_release_is_read_from_the_published_description()
    {
        using var document = JsonDocument.Parse("""
        {
          "tag_name": "v2026.0901.01",
          "draft": false,
          "body": "Fehlerbehebungen",
          "assets": [
            { "name": "quelle.zip", "browser_download_url": "https://github.com/x/quelle.zip", "size": 10 },
            { "name": "ZoneManager.exe", "browser_download_url": "https://github.com/x/ZoneManager.exe", "size": 66149043 }
          ]
        }
        """);

        var release = GitHubReleaseFeed.Parse(document.RootElement);

        Assert.NotNull(release);
        Assert.Equal("v2026.0901.01", release.TagName);
        Assert.Equal(66_149_043, release.SizeInBytes);
        Assert.Equal("Fehlerbehebungen", release.Notes);
    }

    [Theory]
    [InlineData("""{ "tag_name": "v1", "draft": true, "assets": [ { "name": "ZoneManager.exe", "browser_download_url": "https://github.com/x/y.exe", "size": 5 } ] }""")]
    [InlineData("""{ "tag_name": "v1", "assets": [ { "name": "andere.exe", "browser_download_url": "https://github.com/x/y.exe", "size": 5 } ] }""")]
    [InlineData("""{ "tag_name": "v1", "assets": [] }""")]
    [InlineData("""{ "assets": [] }""")]
    [InlineData("""[]""")]
    public void An_incomplete_or_draft_release_yields_nothing(string json)
    {
        using var document = JsonDocument.Parse(json);

        Assert.Null(GitHubReleaseFeed.Parse(document.RootElement));
    }
}
