using SnapZones.App.Services;
using Xunit;

namespace SnapZones.Tests.Services;

public sealed class ElevationNoticeTests
{
    private static ElevationCapability Restricted() => ElevationCapability.Inspect(
        isElevated: false,
        isAdministratorMember: false,
        isUserAccountControlEnabled: true,
        isInteractiveSession: true);

    private static ElevationCapability Elevatable() => ElevationCapability.Inspect(
        isElevated: false,
        isAdministratorMember: true,
        isUserAccountControlEnabled: true,
        isInteractiveSession: true);

    private static ElevationCapability Elevated() => ElevationCapability.Inspect(
        isElevated: true,
        isAdministratorMember: true,
        isUserAccountControlEnabled: true,
        isInteractiveSession: true);

    [Fact]
    public void An_elevated_process_shows_no_banner()
    {
        Assert.Null(ElevationNotice.BuildBanner(Elevated(), startupNotice: null));
        Assert.Null(ElevationNotice.DescribePlacementFailure(Elevated(), "Ein Fenster liess sich nicht einrasten"));
    }

    [Fact]
    public void The_banner_names_the_restriction_and_the_startup_reason()
    {
        var banner = ElevationNotice.BuildBanner(Elevatable(), "Die Abfrage der Benutzerkontensteuerung wurde abgebrochen.");

        Assert.NotNull(banner);
        Assert.Contains(ElevationNotice.RestrictionSummary, banner, StringComparison.Ordinal);
        Assert.Contains("abgebrochen", banner, StringComparison.Ordinal);
        Assert.Contains("erneuter Versuch", banner, StringComparison.Ordinal);
    }

    [Fact]
    public void The_banner_falls_back_to_the_capability_description()
    {
        var capability = Restricted();
        var banner = ElevationNotice.BuildBanner(capability, startupNotice: null);

        Assert.NotNull(banner);
        Assert.Contains(capability.Description, banner, StringComparison.Ordinal);
        Assert.Contains("nicht möglich", banner, StringComparison.Ordinal);
    }

    [Fact]
    public void A_failed_placement_is_explained_instead_of_reported_as_an_error()
    {
        var message = ElevationNotice.DescribePlacementFailure(Elevatable(), "Das Fenster liess sich nicht einrasten");

        Assert.NotNull(message);
        Assert.StartsWith("Das Fenster liess sich nicht einrasten:", message, StringComparison.Ordinal);
        Assert.Contains(ElevationNotice.RestrictionSummary, message, StringComparison.Ordinal);
    }
}
