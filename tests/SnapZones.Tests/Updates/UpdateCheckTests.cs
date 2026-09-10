using SnapZones.Core.Updates;
using Xunit;

namespace SnapZones.Tests.Updates;

/// <summary>
/// Der Update-Vergleich ist bewusst zurückhaltend: nur eine eindeutig höhere Version aus der bekannten
/// Release-Ablage wird überhaupt angeboten. Alles Zweifelhafte endet als «unbekannt», nie als Angebot.
/// </summary>
public sealed class UpdateCheckTests
{
    [Theory]
    [InlineData("2026.0901.01", 2026, 901, 1)]
    [InlineData("v2026.0901.01", 2026, 901, 1)]
    [InlineData("2026.831.1", 2026, 831, 1)]
    [InlineData("2026.0901.01+abc", 2026, 901, 1)]
    public void Versions_are_read_regardless_of_tag_prefix_leading_zeros_and_metadata(
        string text,
        int year,
        int monthDay,
        int sequence)
    {
        Assert.True(ProductVersion.TryParse(text, out var version));
        Assert.Equal(new ProductVersion(year, monthDay, sequence), version);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("2026.0901")]
    [InlineData("2026.0901.01.02")]
    [InlineData("zwei.null.zwei")]
    [InlineData("2026.-1.01")]
    public void Anything_outside_the_scheme_is_refused(string text) =>
        Assert.False(ProductVersion.TryParse(text, out _));

    [Fact]
    public void The_sequence_is_compared_as_a_number_not_as_text()
    {
        // «10» steht alphabetisch vor «09» und waere als Text die aeltere Version.
        Assert.True(ProductVersion.TryParse("2026.0901.10", out var tenth));
        Assert.True(ProductVersion.TryParse("2026.0901.09", out var ninth));

        Assert.True(tenth > ninth);
    }

    [Fact]
    public void The_display_form_keeps_its_leading_zeros()
    {
        Assert.True(ProductVersion.TryParse("2026.831.1", out var version));

        Assert.Equal("2026.0831.01", version.ToString());
    }

    [Fact]
    public void A_higher_published_version_is_offered()
    {
        var result = UpdateCheck.Evaluate("2026.0831.01", Release("v2026.0901.01"));

        Assert.Equal(UpdateAvailability.UpdateAvailable, result.Availability);
        Assert.Equal(new ProductVersion(2026, 901, 1), result.LatestVersion);
        Assert.NotNull(result.Release);
        Assert.Contains("2026.0901.01", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_same_or_an_older_published_version_is_not_offered()
    {
        Assert.Equal(
            UpdateAvailability.UpToDate,
            UpdateCheck.Evaluate("2026.0901.01", Release("v2026.0901.01")).Availability);
        Assert.Equal(
            UpdateAvailability.UpToDate,
            UpdateCheck.Evaluate("2026.0901.02", Release("v2026.0901.01")).Availability);
    }

    [Fact]
    public void An_unreadable_version_on_either_side_offers_nothing()
    {
        Assert.Equal(
            UpdateAvailability.Unknown,
            UpdateCheck.Evaluate("Entwicklerbau", Release("v2026.0901.01")).Availability);
        Assert.Equal(
            UpdateAvailability.Unknown,
            UpdateCheck.Evaluate("2026.0831.01", Release("neueste")).Availability);
        Assert.Equal(
            UpdateAvailability.Unknown,
            UpdateCheck.Evaluate("2026.0831.01", null).Availability);
    }

    [Theory]
    [InlineData("http://github.com/klopp1991/zone-manager/releases/download/v1/ZoneManager-Setup.msi")]
    [InlineData("https://beispiel.invalid/ZoneManager-Setup.msi")]
    [InlineData("https://github.com.angreifer.invalid/ZoneManager-Setup.msi")]
    [InlineData("nicht einmal eine Adresse")]
    public void A_download_from_anywhere_but_the_release_store_is_refused(string url)
    {
        // Eine manipulierte Antwort darf kein fremdes Installationspaket unterschieben.
        var release = Release("v2026.0901.01") with { DownloadUrl = url };

        Assert.False(UpdateCheck.IsAcceptableDownload(release, out var rejection));
        Assert.False(string.IsNullOrWhiteSpace(rejection));
        Assert.Equal(UpdateAvailability.Unknown, UpdateCheck.Evaluate("2026.0831.01", release).Availability);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(UpdateCheck.MaximumDownloadBytes + 1)]
    public void A_file_of_implausible_size_is_refused(long size)
    {
        var release = Release("v2026.0901.01") with { SizeInBytes = size };

        Assert.False(UpdateCheck.IsAcceptableDownload(release, out _));
        Assert.Equal(UpdateAvailability.Unknown, UpdateCheck.Evaluate("2026.0831.01", release).Availability);
    }

    [Fact]
    public void The_release_store_itself_is_accepted()
    {
        Assert.True(UpdateCheck.IsAcceptableDownload(Release("v2026.0901.01"), out var rejection));
        Assert.Equal(string.Empty, rejection);

        var redirected = Release("v2026.0901.01") with
        {
            DownloadUrl = "https://objects.githubusercontent.com/irgendwo/ZoneManager-Setup.msi"
        };
        Assert.True(UpdateCheck.IsAcceptableDownload(redirected, out _));
    }

    private static ReleaseDescription Release(string tag) => new(
        tag,
        $"https://github.com/klopp1991/zone-manager/releases/download/{tag}/ZoneManager-Setup-2026.0901.01.msi",
        70_149_043,
        $"Fehlerbehebungen\n\nSHA-256 `{Hash}`",
        Hash);

    private const string Hash = "6d1f0a2b3c4d5e6f708192a3b4c5d6e7f8091a2b3c4d5e6f708192a3b4c5d6e7";

    [Fact]
    public void A_release_without_a_checksum_is_never_downloaded()
    {
        // Die Groesse allein ist kein Echtheitsmerkmal; ohne SHA-256 im Text bleibt die Datei liegen.
        var release = Release("v2026.0901.01") with { Checksum = null, Notes = "Fehlerbehebungen" };

        Assert.False(UpdateCheck.IsAcceptableDownload(release, out var rejection));
        Assert.Contains("Prüfsumme", rejection, StringComparison.Ordinal);
        Assert.Equal(UpdateAvailability.Unknown, UpdateCheck.Evaluate("2026.0831.01", release).Availability);
    }

    [Fact]
    public void The_checksum_is_read_from_a_checksum_file_and_from_a_release_text()
    {
        var hash = new string('a', 64);
        Assert.True(UpdateCheck.TryParseChecksum($"{hash} *ZoneManager-Setup.msi" + Environment.NewLine, out var first));
        Assert.Equal(hash, first);
        Assert.True(UpdateCheck.TryParseChecksum($"SHA256  {hash.ToUpperInvariant()}", out var second));
        Assert.Equal(hash, second);
        Assert.False(UpdateCheck.TryParseChecksum("kaputt", out _));
        Assert.False(UpdateCheck.TryParseChecksum(null, out _));
    }

    [Fact]
    public void The_named_checksum_wins_over_any_other_hex_field_in_the_text()
    {
        // Ein Release-Text traegt auch anderes; die ausdrueckliche Nennung entscheidet.
        var other = new string('b', 64);
        var wanted = new string('c', 64);

        Assert.True(UpdateCheck.TryParseChecksum($"Anhang {other}\nSHA-256: `{wanted}`", out var checksum));
        Assert.Equal(wanted, checksum);
    }

    [Fact]
    public void The_release_feed_reads_the_installer_package_and_its_checksum()
    {
        var hash = new string('d', 64);
        var json = System.Text.Json.JsonDocument.Parse($$"""
            {
              "tag_name": "v2026.0902.02",
              "body": "Fehlerbehebungen\n\nSHA-256 `{{hash}}`",
              "assets": [
                { "name": "ZoneManager-Setup-2026.0902.02.msi", "browser_download_url": "https://github.com/x/releases/download/v1/ZoneManager-Setup-2026.0902.02.msi", "size": 70123456 }
              ]
            }
            """);

        var release = GitHubReleaseFeed.Parse(json.RootElement);

        Assert.NotNull(release);
        Assert.EndsWith(".msi", release.DownloadUrl, StringComparison.Ordinal);
        Assert.Equal(70123456, release.SizeInBytes);
        Assert.Equal(hash, release.Checksum);
        Assert.True(UpdateCheck.IsAcceptableDownload(release, out _));
    }

    [Fact]
    public void A_release_without_an_installer_package_offers_nothing()
    {
        // Veroeffentlichungen bis 2026.0909.03 tragen Programmdatei und Helfer einzeln; seit dem
        // 10.09.2026 haengt nur noch das Installationspaket daran. Ein alter Stand wird nicht angeboten.
        var json = System.Text.Json.JsonDocument.Parse("""
            {
              "tag_name": "v2026.0901.01",
              "assets": [
                { "name": "ZoneManager.exe", "browser_download_url": "https://github.com/x/releases/download/v1/ZoneManager.exe", "size": 123 },
                { "name": "ZoneManager.exe.sha256", "browser_download_url": "https://github.com/x/releases/download/v1/ZoneManager.exe.sha256", "size": 80 }
              ]
            }
            """);

        Assert.Null(GitHubReleaseFeed.Parse(json.RootElement));
    }

    [Fact]
    public void The_api_address_of_an_asset_wins_over_the_browser_address()
    {
        // Nur die API-Adresse nimmt einen Zugangsschluessel an; ohne sie ist eine private Ablage zu.
        var json = System.Text.Json.JsonDocument.Parse($$"""
            {
              "tag_name": "v2026.0902.02",
              "body": "SHA-256 `{{new string('e', 64)}}`",
              "assets": [
                {
                  "name": "ZoneManager-Setup-2026.0902.02.msi",
                  "url": "https://api.github.com/repos/x/y/releases/assets/1",
                  "browser_download_url": "https://github.com/x/releases/download/v1/ZoneManager-Setup-2026.0902.02.msi",
                  "size": 70123456
                }
              ]
            }
            """);

        var release = GitHubReleaseFeed.Parse(json.RootElement);

        Assert.NotNull(release);
        Assert.Equal("https://api.github.com/repos/x/y/releases/assets/1", release.DownloadUrl);
    }
}
