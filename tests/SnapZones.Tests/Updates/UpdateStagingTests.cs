using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using SnapZones.Core.Updates;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.Updates;

/// <summary>
/// Ein Update wird zuerst geladen und geprüft und erst beim Beenden übernommen. An einer
/// Veröffentlichung hängt seit dem 10.09.2026 genau eine Datei, das Installationspaket; Windows
/// Installer ersetzt Programmdatei und Fensterhelfer gemeinsam. Die laufende Programmdatei wird von
/// hier aus nie angefasst: eine Single-File-Anwendung lädt Bausteine über den Pfad ihrer Programmdatei
/// nach, und eine weggeschobene Datei liess jedes Nachladen scheitern.
/// </summary>
public sealed class UpdateStagingTests
{
    [Fact]
    public async Task Staging_downloads_the_package_and_leaves_the_running_program_untouched()
    {
        using var directory = new TemporaryDirectory();
        var executable = Path.Combine(directory.Path, "ZoneManager.exe");
        File.WriteAllText(executable, "laufend");
        var staging = Path.Combine(directory.Path, "updates");
        var release = Release("paket");
        var installer = new UpdateInstaller(() => new HttpClient(new StubHandler(release.Responses)));

        var result = await installer.StageAsync(release.Description, staging, CancellationToken.None);

        Assert.Equal(UpdateInstallStatus.Staged, result.Status);
        Assert.Equal("laufend", File.ReadAllText(executable));
        Assert.NotNull(result.PackagePath);
        Assert.EndsWith(".msi", result.PackagePath, StringComparison.Ordinal);
        Assert.Equal("paket", File.ReadAllText(result.PackagePath));
    }

    [Fact]
    public async Task A_wrong_checksum_discards_the_download()
    {
        using var directory = new TemporaryDirectory();
        var staging = Path.Combine(directory.Path, "updates");
        var release = Release("paket", checksumOverride: new string('0', 64));
        var installer = new UpdateInstaller(() => new HttpClient(new StubHandler(release.Responses)));

        var result = await installer.StageAsync(release.Description, staging, CancellationToken.None);

        Assert.Equal(UpdateInstallStatus.Refused, result.Status);
        Assert.Contains("Prüfsumme", result.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFiles(staging));
    }

    [Fact]
    public async Task A_size_that_differs_from_the_announcement_discards_the_download()
    {
        using var directory = new TemporaryDirectory();
        var staging = Path.Combine(directory.Path, "updates");
        var release = Release("paket");
        var described = release.Description with { SizeInBytes = release.Description.SizeInBytes + 1 };
        var installer = new UpdateInstaller(() => new HttpClient(new StubHandler(release.Responses)));

        var result = await installer.StageAsync(described, staging, CancellationToken.None);

        Assert.Equal(UpdateInstallStatus.Refused, result.Status);
        Assert.Contains("Grösse", result.Message, StringComparison.Ordinal);
        Assert.Empty(Directory.EnumerateFiles(staging));
    }

    [Fact]
    public async Task A_release_without_a_checksum_is_refused_before_anything_is_loaded()
    {
        using var directory = new TemporaryDirectory();
        var staging = Path.Combine(directory.Path, "updates");
        var release = Release("paket");
        var installer = new UpdateInstaller(() => new HttpClient(new StubHandler(release.Responses)));

        var result = await installer.StageAsync(
            release.Description with { Checksum = null, Notes = null },
            staging,
            CancellationToken.None);

        Assert.Equal(UpdateInstallStatus.Refused, result.Status);
        Assert.False(Directory.Exists(staging));
    }

    [Fact]
    public async Task An_unreachable_address_reports_a_failed_download()
    {
        using var directory = new TemporaryDirectory();
        var staging = Path.Combine(directory.Path, "updates");
        var release = Release("paket");
        var installer = new UpdateInstaller(() => new HttpClient(new StubHandler([])));

        var result = await installer.StageAsync(release.Description, staging, CancellationToken.None);

        Assert.Equal(UpdateInstallStatus.DownloadFailed, result.Status);
        Assert.Empty(Directory.EnumerateFiles(staging));
    }

    [Fact]
    public void The_staging_directory_is_emptied_and_a_missing_one_is_no_error()
    {
        using var directory = new TemporaryDirectory();
        var staging = Path.Combine(directory.Path, "updates");
        Directory.CreateDirectory(staging);
        File.WriteAllText(Path.Combine(staging, "ZoneManager-Setup-alt.msi"), "alt");

        UpdateInstaller.CleanStagingDirectory(staging);
        Assert.Empty(Directory.EnumerateFiles(staging));

        UpdateInstaller.CleanStagingDirectory(Path.Combine(directory.Path, "gibtesnicht"));
        UpdateInstaller.CleanStagingDirectory(string.Empty);
    }

    [Fact]
    public void Files_left_behind_by_the_former_swap_are_removed_once()
    {
        // Bis zum 10.09.2026 wurde die laufende Programmdatei beiseitegeschoben. Auf Rechnern, die
        // damals ein Update bekommen haben, liegen die Reste noch.
        using var directory = new TemporaryDirectory();
        var executable = Path.Combine(directory.Path, "ZoneManager.exe");
        File.WriteAllText(executable, "laufend");
        File.WriteAllText($"{executable}.previous.1", "alt");
        File.WriteAllText($"{executable}.previous.2", "aelter");

        Assert.Equal(2, UpdateInstaller.RemoveSupersededFiles(executable));
        Assert.Equal(0, UpdateInstaller.RemoveSupersededFiles(executable));
        Assert.Equal(0, UpdateInstaller.RemoveSupersededFiles(null));
        Assert.True(File.Exists(executable));
    }

    private static (ReleaseDescription Description, Dictionary<string, byte[]> Responses) Release(
        string packageContent,
        string? checksumOverride = null)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(packageContent);
        var url = "https://github.com/x/ZoneManager-Setup-2026.0905.01.msi";
        var responses = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase) { [url] = bytes };
        var checksum = checksumOverride ?? Checksum(bytes);
        var description = new ReleaseDescription(
            "v2026.0905.01",
            url,
            bytes.Length,
            $"Fehlerbehebungen\n\nSHA-256 `{checksum}`",
            checksum);
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
