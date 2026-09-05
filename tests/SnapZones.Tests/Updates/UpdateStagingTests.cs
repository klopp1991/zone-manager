using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using SnapZones.App.Services;
using SnapZones.Core.Updates;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.Updates;

/// <summary>
/// Ein Update wird zuerst bereitgestellt und erst nach dem Ende der alten Anwendung übernommen. Die
/// laufende Programmdatei darf vorher nie angefasst werden: eine Single-File-Anwendung lädt Bausteine
/// über den Pfad ihrer Programmdatei nach, und eine weggeschobene Datei liess jedes Nachladen scheitern.
/// </summary>
public sealed class UpdateStagingTests
{
    private static readonly DateTimeOffset Moment = new(2026, 9, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Staging_downloads_both_files_and_leaves_the_running_program_untouched()
    {
        using var directory = new TemporaryDirectory();
        var executable = Path.Combine(directory.Path, "ZoneManager.exe");
        File.WriteAllText(executable, "laufend");
        var staging = Path.Combine(directory.Path, "updates");
        var release = Release("neu", "neuer helfer");
        var installer = new UpdateInstaller(() => new HttpClient(new StubHandler(release.Responses)));

        var result = await installer.StageAsync(staging, release.Description, CancellationToken.None);

        Assert.Equal(UpdateInstallStatus.Staged, result.Status);
        Assert.Equal("laufend", File.ReadAllText(executable));
        Assert.Equal("neu", File.ReadAllText(UpdateInstaller.BuildStagedExecutablePath(staging)));
        Assert.Equal("neuer helfer", File.ReadAllText(UpdateInstaller.BuildStagedHelperPath(staging)));
    }

    [Fact]
    public async Task A_wrong_checksum_discards_the_download()
    {
        using var directory = new TemporaryDirectory();
        var staging = Path.Combine(directory.Path, "updates");
        var release = Release("neu", helper: null, checksumOverride: new string('0', 64));
        var installer = new UpdateInstaller(() => new HttpClient(new StubHandler(release.Responses)));

        var result = await installer.StageAsync(staging, release.Description, CancellationToken.None);

        Assert.Equal(UpdateInstallStatus.DownloadFailed, result.Status);
        Assert.False(File.Exists(UpdateInstaller.BuildStagedExecutablePath(staging)));
    }

    [Fact]
    public async Task Leftovers_of_an_earlier_staging_are_cleared_first()
    {
        using var directory = new TemporaryDirectory();
        var staging = Path.Combine(directory.Path, "updates");
        Directory.CreateDirectory(staging);
        File.WriteAllText(UpdateInstaller.BuildStagedHelperPath(staging), "alter helfer");
        var release = Release("neu", helper: null);
        var installer = new UpdateInstaller(() => new HttpClient(new StubHandler(release.Responses)));

        var result = await installer.StageAsync(staging, release.Description, CancellationToken.None);

        Assert.Equal(UpdateInstallStatus.Staged, result.Status);
        Assert.False(File.Exists(UpdateInstaller.BuildStagedHelperPath(staging)));
    }

    [Fact]
    public void Applying_copies_the_staged_files_into_place_and_keeps_the_staged_copy()
    {
        using var directory = new TemporaryDirectory();
        var target = Path.Combine(directory.Path, "app", "ZoneManager.exe");
        var helper = Path.Combine(directory.Path, "app", "ZoneManager.Helper.exe");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target, "alt");
        File.WriteAllText(helper, "alter helfer");
        var staging = Path.Combine(directory.Path, "updates");
        Directory.CreateDirectory(staging);
        File.WriteAllText(UpdateInstaller.BuildStagedExecutablePath(staging), "neu");
        File.WriteAllText(UpdateInstaller.BuildStagedHelperPath(staging), "neuer helfer");

        var result = UpdateInstaller.Apply(staging, target, Moment);

        Assert.Equal(UpdateInstallStatus.Applied, result.Status);
        Assert.Equal("neu", File.ReadAllText(target));
        Assert.Equal("neuer helfer", File.ReadAllText(helper));
        Assert.Equal("alt", File.ReadAllText(UpdateInstaller.BuildSupersededPath(target, Moment)));
        // Die bereitgestellte Datei ist die, aus der der Uebernahmeprozess laeuft; sie bleibt liegen.
        Assert.True(File.Exists(UpdateInstaller.BuildStagedExecutablePath(staging)));
    }

    [Fact]
    public void Applying_without_a_staged_file_changes_nothing()
    {
        using var directory = new TemporaryDirectory();
        var target = Path.Combine(directory.Path, "ZoneManager.exe");
        File.WriteAllText(target, "alt");

        var result = UpdateInstaller.Apply(Path.Combine(directory.Path, "leer"), target, Moment);

        Assert.Equal(UpdateInstallStatus.DownloadFailed, result.Status);
        Assert.Equal("alt", File.ReadAllText(target));
    }

    [Fact]
    public void A_blocked_target_puts_the_previous_file_back()
    {
        using var directory = new TemporaryDirectory();
        var target = Path.Combine(directory.Path, "ZoneManager.exe");
        File.WriteAllText(target, "alt");
        var staging = Path.Combine(directory.Path, "updates");
        Directory.CreateDirectory(staging);
        File.WriteAllText(UpdateInstaller.BuildStagedExecutablePath(staging), "neu");

        UpdateInstallResult result;
        using (new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            result = UpdateInstaller.Apply(staging, target, Moment);
        }

        Assert.Equal(UpdateInstallStatus.ReplaceFailed, result.Status);
        Assert.Equal("alt", File.ReadAllText(target));
        Assert.False(File.Exists(target + ".download"));
    }

    [Fact]
    public void Cleaning_removes_the_staging_directory_and_tolerates_a_file_in_use()
    {
        using var directory = new TemporaryDirectory();
        var staging = Path.Combine(directory.Path, "updates");
        Directory.CreateDirectory(staging);
        var staged = UpdateInstaller.BuildStagedExecutablePath(staging);
        File.WriteAllText(staged, "neu");

        using (new FileStream(staged, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.False(UpdateInstaller.CleanStagingDirectory(staging));
            Assert.True(File.Exists(staged));
        }

        Assert.True(UpdateInstaller.CleanStagingDirectory(staging));
        Assert.False(Directory.Exists(staging));
        Assert.True(UpdateInstaller.CleanStagingDirectory(staging));
    }

    [Fact]
    public void Write_access_is_probed_with_a_real_file()
    {
        using var directory = new TemporaryDirectory();

        Assert.True(UpdateApplyRunner.CanWriteTo(Path.Combine(directory.Path, "neu")));
        Assert.Empty(Directory.EnumerateFiles(Path.Combine(directory.Path, "neu")));
        Assert.False(UpdateApplyRunner.CanWriteTo(string.Empty));
    }

    private static (ReleaseDescription Description, Dictionary<string, byte[]> Responses) Release(
        string executableContent,
        string? helper,
        string? checksumOverride = null)
    {
        var responses = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        var executableBytes = System.Text.Encoding.UTF8.GetBytes(executableContent);
        responses["https://github.com/x/ZoneManager.exe"] = executableBytes;
        responses["https://github.com/x/ZoneManager.exe.sha256"] =
            System.Text.Encoding.UTF8.GetBytes((checksumOverride ?? Checksum(executableBytes)) + "  ZoneManager.exe\n");

        byte[]? helperBytes = helper is null ? null : System.Text.Encoding.UTF8.GetBytes(helper);
        if (helperBytes is not null)
        {
            responses["https://github.com/x/ZoneManager.Helper.exe"] = helperBytes;
            responses["https://github.com/x/ZoneManager.Helper.exe.sha256"] =
                System.Text.Encoding.UTF8.GetBytes(Checksum(helperBytes) + "  ZoneManager.Helper.exe\n");
        }

        var description = new ReleaseDescription(
            "v2026.0905.01",
            "https://github.com/x/ZoneManager.exe",
            executableBytes.Length,
            null,
            "https://github.com/x/ZoneManager.exe.sha256",
            helperBytes is null ? null : "https://github.com/x/ZoneManager.Helper.exe",
            helperBytes?.Length ?? 0,
            helperBytes is null ? null : "https://github.com/x/ZoneManager.Helper.exe.sha256");
        return (description, responses);
    }

    private static string Checksum(byte[] content) => Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    private sealed class StubHandler(Dictionary<string, byte[]> responses) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri?.ToString() ?? string.Empty;
            if (!responses.TryGetValue(url, out var content))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(content)
            });
        }
    }
}
