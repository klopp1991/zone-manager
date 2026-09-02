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
/// <param name="ChecksumUrl">
/// Die Adresse der Prüfsummendatei <c>ZoneManager.exe.sha256</c>. Ohne sie wird nichts geladen: die
/// Grösse allein ist kein Echtheitsmerkmal.
/// </param>
/// <param name="HelperUrl">
/// Die Adresse des Fensterhelfers <c>ZoneManager.Helper.exe</c>, sofern die Veröffentlichung ihn
/// mitbringt. Ältere Veröffentlichungen tragen ihn nicht; dann bleibt der vorhandene Helfer liegen.
/// </param>
/// <param name="HelperChecksumUrl">
/// Die Adresse von <c>ZoneManager.Helper.exe.sha256</c>. Liegt ein Helfer bei, ist sie Pflicht — sonst
/// wäre er die eine Datei der Veröffentlichung, die niemand nachrechnet.
/// </param>
public sealed record ReleaseDescription(
    string TagName,
    string DownloadUrl,
    long SizeInBytes,
    string? Notes,
    string? ChecksumUrl = null,
    string? HelperUrl = null,
    long HelperSizeInBytes = 0,
    string? HelperChecksumUrl = null);

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

        if (!IsAcceptableFile(
                release.DownloadUrl,
                release.SizeInBytes,
                release.ChecksumUrl,
                "ZoneManager.exe.sha256",
                out rejection))
        {
            return false;
        }

        // Bringt die Veroeffentlichung einen Fensterhelfer mit, gelten fuer ihn dieselben Regeln. Ein
        // Helfer ohne Pruefsumme waere die eine Datei, die niemand nachrechnet -- und er laeuft mit
        // uiAccess. Lieber gar kein Update als eines mit einer ungeprueften zweiten Datei.
        if (HasHelper(release) &&
            !IsAcceptableFile(
                release.HelperUrl,
                release.HelperSizeInBytes,
                release.HelperChecksumUrl,
                "ZoneManager.Helper.exe.sha256",
                out rejection))
        {
            return false;
        }

        rejection = string.Empty;
        return true;
    }

    /// <summary>
    /// Ob die Veröffentlichung einen Fensterhelfer mitbringt. Ältere Veröffentlichungen tragen ihn nicht;
    /// dann bleibt der vorhandene Helfer unangetastet.
    /// </summary>
    public static bool HasHelper(ReleaseDescription release)
    {
        ArgumentNullException.ThrowIfNull(release);
        return !string.IsNullOrWhiteSpace(release.HelperUrl);
    }

    private static bool IsAcceptableFile(
        string? url,
        long sizeInBytes,
        string? checksumUrl,
        string checksumFileName,
        out string rejection)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps ||
            !IsTrustedHost(uri.Host))
        {
            rejection = "Die Veröffentlichung verweist auf eine unerwartete Adresse und wird nicht geladen.";
            return false;
        }

        if (sizeInBytes <= 0 || sizeInBytes > MaximumDownloadBytes)
        {
            rejection = "Die angebotene Datei hat eine unplausible Grösse und wird nicht geladen.";
            return false;
        }

        if (!Uri.TryCreate(checksumUrl, UriKind.Absolute, out var checksumUri) ||
            checksumUri.Scheme != Uri.UriSchemeHttps ||
            !IsTrustedHost(checksumUri.Host))
        {
            rejection = $"Die Veröffentlichung trägt keine Prüfsumme ({checksumFileName}) und wird nicht geladen.";
            return false;
        }

        rejection = string.Empty;
        return true;
    }

    /// <summary>
    /// Liest die SHA-256-Prüfsumme aus dem Inhalt einer <c>.sha256</c>-Datei: das erste Feld mit 64
    /// Hexadezimalzeichen, wie es <c>sha256sum</c> und <c>Get-FileHash</c> schreiben.
    /// </summary>
    public static bool TryParseChecksum(string? content, out string checksum)
    {
        checksum = string.Empty;
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        foreach (var token in content.Split((char[])[' ', '\t', '\r', '\n', '*'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (token.Length == 64 && token.All(Uri.IsHexDigit))
            {
                checksum = token.ToLowerInvariant();
                return true;
            }
        }

        return false;
    }

    private static bool IsTrustedHost(string host) =>
        string.Equals(host, "github.com", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(host, "objects.githubusercontent.com", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".github.com", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase);
}
