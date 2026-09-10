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

/// <summary>
/// Eine Veröffentlichung, wie sie die Release-Seite beschreibt. An einer Veröffentlichung hängt genau
/// eine Datei: das Installationspaket. Programmdatei, Fensterhelfer und ihre Prüfsummendateien lagen
/// bis zum 10.09.2026 einzeln daneben; sie sind im Paket enthalten und werden nicht mehr veröffentlicht.
/// </summary>
/// <param name="DownloadUrl">Die Adresse des Installationspakets <c>ZoneManager-Setup-&lt;Version&gt;.msi</c>.</param>
/// <param name="Checksum">
/// Die SHA-256-Prüfsumme des Pakets, wie sie im Text der Veröffentlichung steht. Ohne sie wird nichts
/// geladen: die Grösse allein ist kein Echtheitsmerkmal.
/// </param>
public sealed record ReleaseDescription(
    string TagName,
    string DownloadUrl,
    long SizeInBytes,
    string? Notes,
    string? Checksum = null);

public sealed record UpdateCheckResult(
    UpdateAvailability Availability,
    ProductVersion? LatestVersion,
    ReleaseDescription? Release,
    string Message);

public static class UpdateCheck
{
    /// <summary>
    /// Das grösste Installationspaket, das als Update angenommen wird. Das Paket misst rund 70 MB;
    /// alles jenseits dieser Grenze ist nicht mehr plausibel und wird nicht heruntergeladen.
    /// </summary>
    public const long MaximumDownloadBytes = 200L * 1024 * 1024;

    /// <summary>Die Dateiendung des einen Anhangs, den eine Veröffentlichung trägt.</summary>
    public const string PackageExtension = ".msi";

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
    /// Prüft Herkunft, Art, Grösse und Prüfsumme des Downloads. Die Datei kommt ausschliesslich über
    /// HTTPS von der Release-Ablage des Projekts; ein Verweis auf einen anderen Rechner wird abgelehnt,
    /// damit eine manipulierte Antwort kein fremdes Paket unterschieben kann.
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

        // Die Pruefsumme steht im Text der Veroeffentlichung. Ohne sie waere das Paket die eine Datei,
        // die niemand nachrechnet -- und es laeuft mit Administratorrechten.
        if (!TryParseChecksum(release.Checksum, out _))
        {
            rejection = "Die Veröffentlichung nennt keine SHA-256-Prüfsumme und wird nicht geladen.";
            return false;
        }

        rejection = string.Empty;
        return true;
    }

    /// <summary>
    /// Liest eine SHA-256-Prüfsumme aus einem Text: bevorzugt das Feld hinter dem Wort «sha256», sonst
    /// das erste Feld mit 64 Hexadezimalzeichen. Damit taugt sowohl der Inhalt einer
    /// <c>.sha256</c>-Datei als auch der Text einer Veröffentlichung als Quelle.
    /// </summary>
    public static bool TryParseChecksum(string? content, out string checksum)
    {
        checksum = string.Empty;
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        var tokens = content.Split(
            (char[])[' ', '\t', '\r', '\n', '*', ':', '`', '|'],
            StringSplitOptions.RemoveEmptyEntries);

        // Erst die ausdrueckliche Nennung: in einem Release-Text stehen auch andere Zeichenketten.
        for (var index = 0; index < tokens.Length - 1; index++)
        {
            // «SHA-256», «SHA256», «sha256» – der Bindestrich darf den Treffer nicht verhindern.
            if (tokens[index].Replace("-", string.Empty, StringComparison.Ordinal)
                    .Contains("sha256", StringComparison.OrdinalIgnoreCase) &&
                IsSha256(tokens[index + 1]))
            {
                checksum = tokens[index + 1].ToLowerInvariant();
                return true;
            }
        }

        foreach (var token in tokens)
        {
            if (IsSha256(token))
            {
                checksum = token.ToLowerInvariant();
                return true;
            }
        }

        return false;
    }

    private static bool IsSha256(string token) => token.Length == 64 && token.All(Uri.IsHexDigit);

    private static bool IsTrustedHost(string host) =>
        string.Equals(host, "github.com", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(host, "objects.githubusercontent.com", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".github.com", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase);
}
