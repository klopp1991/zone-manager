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
        "Fehlerbehebungen");
}
