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
    [InlineData("http://github.com/klopp1991/zone-manager/releases/download/v1/ZoneManager.exe")]
    [InlineData("https://beispiel.invalid/ZoneManager.exe")]
    [InlineData("https://github.com.angreifer.invalid/ZoneManager.exe")]
    [InlineData("nicht einmal eine Adresse")]
    public void A_download_from_anywhere_but_the_release_store_is_refused(string url)
    {
        // Eine manipulierte Antwort darf keine fremde Programmdatei unterschieben.
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
            DownloadUrl = "https://objects.githubusercontent.com/irgendwo/ZoneManager.exe"
        };
        Assert.True(UpdateCheck.IsAcceptableDownload(redirected, out _));
    }

    private static ReleaseDescription Release(string tag) => new(
        tag,
        $"https://github.com/klopp1991/zone-manager/releases/download/{tag}/ZoneManager.exe",
        66_149_043,
        "Fehlerbehebungen",
        $"https://github.com/klopp1991/zone-manager/releases/download/{tag}/ZoneManager.exe.sha256");

    [Fact]
    public void A_release_without_a_checksum_file_is_never_downloaded()
    {
        // Die Groesse allein ist kein Echtheitsmerkmal; ohne ZoneManager.exe.sha256 bleibt die Datei liegen.
        var release = Release("v2026.0901.01") with { ChecksumUrl = null };

        Assert.False(UpdateCheck.IsAcceptableDownload(release, out var rejection));
        Assert.Contains("Prüfsumme", rejection, StringComparison.Ordinal);
        Assert.Equal(UpdateAvailability.Unknown, UpdateCheck.Evaluate("2026.0831.01", release).Availability);
    }

    [Fact]
    public void The_checksum_file_is_parsed_in_sha256sum_and_get_filehash_notation()
    {
        var hash = new string('a', 64);
        Assert.True(UpdateCheck.TryParseChecksum($"{hash} *ZoneManager.exe" + Environment.NewLine, out var first));
        Assert.Equal(hash, first);
        Assert.True(UpdateCheck.TryParseChecksum($"SHA256  {hash.ToUpperInvariant()}", out var second));
        Assert.Equal(hash, second);
        Assert.False(UpdateCheck.TryParseChecksum("kaputt", out _));
        Assert.False(UpdateCheck.TryParseChecksum(null, out _));
    }

    [Fact]
    public void A_helper_without_a_checksum_file_stops_the_whole_update()
    {
        // Der Helfer laeuft mit uiAccess. Lieber gar kein Update als eines, dessen zweite Datei
        // niemand nachrechnet.
        var release = Release("v2026.0901.01") with
        {
            HelperUrl = "https://github.com/klopp1991/zone-manager/releases/download/v1/ZoneManager.Helper.exe",
            HelperSizeInBytes = 10_578_996,
            HelperChecksumUrl = null,
        };

        Assert.False(UpdateCheck.IsAcceptableDownload(release, out var rejection));
        Assert.Contains("ZoneManager.Helper.exe.sha256", rejection, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://beispiel.invalid/ZoneManager.Helper.exe")]
    [InlineData("http://github.com/klopp1991/zone-manager/releases/download/v1/ZoneManager.Helper.exe")]
    public void A_helper_from_anywhere_but_the_release_store_is_refused(string url)
    {
        var release = Release("v2026.0901.01") with
        {
            HelperUrl = url,
            HelperSizeInBytes = 10_578_996,
            HelperChecksumUrl = "https://github.com/klopp1991/zone-manager/releases/download/v1/ZoneManager.Helper.exe.sha256",
        };

        Assert.False(UpdateCheck.IsAcceptableDownload(release, out var rejection));
        Assert.False(string.IsNullOrWhiteSpace(rejection));
    }

    [Fact]
    public void An_older_release_without_a_helper_stays_acceptable()
    {
        // Veroeffentlichungen bis 2026.0902.01 tragen keinen Helfer; sie duerfen nicht daran scheitern.
        var release = Release("v2026.0901.01");

        Assert.False(UpdateCheck.HasHelper(release));
        Assert.True(UpdateCheck.IsAcceptableDownload(release, out var rejection));
        Assert.Equal(string.Empty, rejection);
    }

    [Fact]
    public void The_release_feed_reads_the_helper_and_its_checksum()
    {
        var json = System.Text.Json.JsonDocument.Parse("""
            {
              "tag_name": "v2026.0902.02",
              "assets": [
                { "name": "ZoneManager.exe", "browser_download_url": "https://github.com/x/releases/download/v1/ZoneManager.exe", "size": 123 },
                { "name": "ZoneManager.exe.sha256", "browser_download_url": "https://github.com/x/releases/download/v1/ZoneManager.exe.sha256", "size": 80 },
                { "name": "ZoneManager.Helper.exe", "browser_download_url": "https://github.com/x/releases/download/v1/ZoneManager.Helper.exe", "size": 456 },
                { "name": "ZoneManager.Helper.exe.sha256", "browser_download_url": "https://github.com/x/releases/download/v1/ZoneManager.Helper.exe.sha256", "size": 87 }
              ]
            }
            """);

        var release = GitHubReleaseFeed.Parse(json.RootElement);

        Assert.NotNull(release);
        // Die Namen unterscheiden sich nur um ein Wort; die Programmdatei darf nicht den Helfer erwischen.
        Assert.Equal("https://github.com/x/releases/download/v1/ZoneManager.exe", release.DownloadUrl);
        Assert.Equal(123, release.SizeInBytes);
        Assert.Equal("https://github.com/x/releases/download/v1/ZoneManager.Helper.exe", release.HelperUrl);
        Assert.Equal(456, release.HelperSizeInBytes);
        Assert.Equal("https://github.com/x/releases/download/v1/ZoneManager.Helper.exe.sha256", release.HelperChecksumUrl);
        Assert.True(UpdateCheck.HasHelper(release));
    }

    [Fact]
    public void The_release_feed_reads_the_checksum_asset_next_to_the_executable()
    {
        var json = System.Text.Json.JsonDocument.Parse("""
            {
              "tag_name": "v2026.0901.01",
              "assets": [
                { "name": "ZoneManager.exe", "browser_download_url": "https://github.com/x/releases/download/v1/ZoneManager.exe", "size": 123 },
                { "name": "ZoneManager.exe.sha256", "browser_download_url": "https://github.com/x/releases/download/v1/ZoneManager.exe.sha256", "size": 80 }
              ]
            }
            """);

        var release = GitHubReleaseFeed.Parse(json.RootElement);

        Assert.NotNull(release);
        Assert.Equal("https://github.com/x/releases/download/v1/ZoneManager.exe.sha256", release.ChecksumUrl);
        Assert.Equal(123, release.SizeInBytes);
    }
}
