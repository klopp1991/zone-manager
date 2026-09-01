namespace SnapZones.Core.Updates;

public enum UpdateAvailability
{
    /// <summary>Die laufende Version ist die neueste.</summary>
    UpToDate,

    /// <summary>Eine neuere Version steht bereit.</summary>
    UpdateAvailable,

    /// <summary>Die Veröffentlichung liess sich nicht auswerten — kein Grund zur Beunruhigung, aber auch kein Angebot.</summary>
    Unknown
}

/// <summary>Eine Veröffentlichung, wie sie die Release-Seite beschreibt.</summary>
public sealed record ReleaseDescription(
    string TagName,
    string DownloadUrl,
    long SizeInBytes,
    string? Notes);

public sealed record UpdateCheckResult(
    UpdateAvailability Availability,
    ProductVersion? LatestVersion,
    ReleaseDescription? Release,
    string Message);

public static class UpdateCheck
{
    /// <summary>
    /// Die grösste Programmdatei, die als Update angenommen wird. Der Self-contained-Publish misst rund
    /// 66 MB; alles jenseits dieser Grenze ist nicht mehr plausibel und wird nicht heruntergeladen.
    /// </summary>
    public const long MaximumDownloadBytes = 200L * 1024 * 1024;

    /// <summary>
    /// Vergleicht die laufende mit der veröffentlichten Version. Bewusst zurückhaltend: nur eine
    /// eindeutig höhere Version gilt als Update. Lässt sich eine der beiden nicht lesen, wird nichts
    /// angeboten, statt auf Verdacht eine fremde Datei vorzuschlagen.
    /// </summary>
    public static UpdateCheckResult Evaluate(string currentVersion, ReleaseDescription? release)
    {
        if (release is null)
        {
            return new UpdateCheckResult(
                UpdateAvailability.Unknown,
                null,
                null,
                "Es wurde keine Veröffentlichung gefunden.");
        }

        if (!ProductVersion.TryParse(currentVersion, out var current))
        {
            return new UpdateCheckResult(
                UpdateAvailability.Unknown,
                null,
                null,
                $"Die laufende Version «{currentVersion}» folgt nicht dem Schema JJJJ.MMTT.NN.");
        }

        if (!ProductVersion.TryParse(release.TagName, out var latest))
        {
            return new UpdateCheckResult(
                UpdateAvailability.Unknown,
                null,
                null,
                $"Die veröffentlichte Version «{release.TagName}» folgt nicht dem Schema JJJJ.MMTT.NN.");
        }

        if (latest <= current)
        {
            return new UpdateCheckResult(
                UpdateAvailability.UpToDate,
                latest,
                release,
                $"Version {current} ist die neueste.");
        }

        if (!IsAcceptableDownload(release, out var rejection))
        {
            return new UpdateCheckResult(UpdateAvailability.Unknown, latest, null, rejection);
        }

        return new UpdateCheckResult(
            UpdateAvailability.UpdateAvailable,
            latest,
            release,
            $"Version {latest} steht bereit. Installiert ist {current}.");
    }

    /// <summary>
    /// Prüft die Herkunft und die Grösse des Downloads. Die Datei kommt ausschliesslich über HTTPS von
    /// der Release-Ablage des Projekts; ein Verweis auf einen anderen Rechner wird abgelehnt, damit eine
    /// manipulierte Antwort keine fremde Programmdatei unterschieben kann.
    /// </summary>
    public static bool IsAcceptableDownload(ReleaseDescription release, out string rejection)
    {
        ArgumentNullException.ThrowIfNull(release);

        if (!Uri.TryCreate(release.DownloadUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !IsTrustedHost(uri.Host))
        {
            rejection = "Die Veröffentlichung verweist auf eine unerwartete Adresse und wird nicht geladen.";
            return false;
        }

        if (release.SizeInBytes <= 0 || release.SizeInBytes > MaximumDownloadBytes)
        {
            rejection = "Die angebotene Datei hat eine unplausible Grösse und wird nicht geladen.";
            return false;
        }

        rejection = string.Empty;
        return true;
    }

    private static bool IsTrustedHost(string host) =>
        string.Equals(host, "github.com", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(host, "objects.githubusercontent.com", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".github.com", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase);
}
