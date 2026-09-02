using System.Diagnostics;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using SnapZones.Core.Elevation;

namespace SnapZones.Windows.Elevation;

public enum CertificateState
{
    /// <summary>Es ist kein eigenes Zertifikat eingerichtet. Das ist der Auslieferungszustand.</summary>
    NotInstalled,

    /// <summary>Das Zertifikat liegt in allen nötigen Speichern und ist gültig.</summary>
    Trusted,

    /// <summary>Das Zertifikat besteht, ist aber abgelaufen oder noch nicht gültig.</summary>
    Expired,

    /// <summary>Es liegt nur in einem Teil der Speicher — eine halbe Einrichtung.</summary>
    Incomplete
}

public sealed record CertificateStatus(
    CertificateState State,
    string Message,
    DateTimeOffset? ValidUntil = null,
    string? Thumbprint = null);

public sealed record CertificateActionResult(bool Successful, string Message);

/// <summary>
/// Richtet das selbst ausgestellte Zertifikat ein, mit dem das Hilfsprogramm signiert wird, und entfernt
/// es wieder.
///
/// Gearbeitet wird über die Windows-eigene PowerShell. <c>New-SelfSignedCertificate</c> und
/// <c>Set-AuthenticodeSignature</c> gehören zum Lieferumfang von Windows; ein Zertifikat von Hand zu
/// erzeugen oder eine Authenticode-Signatur selbst in eine Programmdatei zu schreiben käme nicht in
/// Frage — beides ist umfangreich, fehleranfällig und hier ohne Not.
///
/// Alle drei Vorgänge verlangen Administratorrechte, weil sie in den Zertifikatspeicher der lokalen
/// Maschine schreiben.
/// </summary>
public sealed class SigningCertificateService
{
    private static readonly TimeSpan CommandTimeout = TimeSpan.FromMinutes(2);
    private readonly Func<string, (int ExitCode, string Output)> runPowerShell;

    public SigningCertificateService(Func<string, (int ExitCode, string Output)>? runPowerShell = null)
    {
        this.runPowerShell = runPowerShell ?? RunPowerShell;
    }

    /// <summary>Liest den aktuellen Stand aus den Zertifikatspeichern der lokalen Maschine.</summary>
    public CertificateStatus Read()
    {
        try
        {
            var found = new Dictionary<string, X509Certificate2>(StringComparer.Ordinal);
            foreach (var storeName in SigningCertificateProfile.Stores)
            {
                if (FindIn(storeName) is { } certificate)
                {
                    found[storeName] = certificate;
                }
            }

            if (found.Count == 0)
            {
                return new CertificateStatus(
                    CertificateState.NotInstalled,
                    "Es ist kein eigenes Zertifikat eingerichtet.");
            }

            var reference = found.Values.First();
            var validUntil = new DateTimeOffset(reference.NotAfter.ToUniversalTime(), TimeSpan.Zero);
            var now = DateTimeOffset.UtcNow;

            if (found.Count != SigningCertificateProfile.Stores.Count)
            {
                var missing = SigningCertificateProfile.Stores.Where(store => !found.ContainsKey(store));
                return new CertificateStatus(
                    CertificateState.Incomplete,
                    $"Das Zertifikat fehlt in: {string.Join(", ", missing)}. Richte es erneut ein.",
                    validUntil,
                    reference.Thumbprint);
            }

            if (now > validUntil || now < new DateTimeOffset(reference.NotBefore.ToUniversalTime(), TimeSpan.Zero))
            {
                return new CertificateStatus(
                    CertificateState.Expired,
                    $"Das Zertifikat war bis {validUntil:dd.MM.yyyy} gültig und muss erneuert werden.",
                    validUntil,
                    reference.Thumbprint);
            }

            return new CertificateStatus(
                CertificateState.Trusted,
                $"Eingerichtet und gültig bis {validUntil:dd.MM.yyyy}.",
                validUntil,
                reference.Thumbprint);
        }
        catch (Exception exception)
        {
            return new CertificateStatus(
                CertificateState.NotInstalled,
                $"Der Zertifikatspeicher liess sich nicht lesen: {exception.Message}");
        }
    }

    /// <summary>
    /// Erzeugt das Zertifikat, legt es in die Vertrauensspeicher und signiert damit das Hilfsprogramm.
    /// Die drei Schritte gehören zusammen: ein Zertifikat ohne signiertes Hilfsprogramm nützt nichts,
    /// und ein signiertes Hilfsprogramm ohne Zertifikat startet nicht.
    /// </summary>
    public CertificateActionResult Install(string helperPath, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(helperPath);

        if (!File.Exists(helperPath))
        {
            return new CertificateActionResult(
                false,
                $"Der Fensterhelfer liegt nicht unter {helperPath}. Installiere das Programm zuerst.");
        }

        var existing = Read();
        var thumbprint = existing.State == CertificateState.Trusted ? existing.Thumbprint : null;
        if (thumbprint is null)
        {
            var creation = runPowerShell(SigningCertificateProfile.BuildCreateScript(now));
            if (creation.ExitCode != 0)
            {
                return new CertificateActionResult(
                    false,
                    $"Das Zertifikat liess sich nicht erzeugen: {Summarize(creation.Output)}");
            }

            thumbprint = ReadThumbprint(creation.Output);
            if (thumbprint is null)
            {
                return new CertificateActionResult(
                    false,
                    "Das Zertifikat wurde erzeugt, sein Fingerabdruck war aber nicht lesbar.");
            }
        }

        var signing = runPowerShell(SigningCertificateProfile.BuildSignScript(helperPath, thumbprint));
        return signing.ExitCode == 0
            ? new CertificateActionResult(
                true,
                "Zertifikat eingerichtet und Fensterhelfer signiert. Beim nächsten Bedarf wird er verwendet.")
            : new CertificateActionResult(
                false,
                $"Der Fensterhelfer liess sich nicht signieren: {Summarize(signing.Output)}");
    }

    /// <summary>Entfernt das Zertifikat aus allen drei Speichern.</summary>
    public CertificateActionResult Remove()
    {
        var result = runPowerShell(SigningCertificateProfile.BuildRemoveScript());
        return result.ExitCode == 0
            ? new CertificateActionResult(
                true,
                "Zertifikat entfernt. Der Fensterhelfer startet nicht mehr; das Programm fragt bei Bedarf "
                    + "wieder nach Administratorrechten.")
            : new CertificateActionResult(
                false,
                $"Das Zertifikat liess sich nicht entfernen: {Summarize(result.Output)}");
    }

    /// <summary>Ob die genannte Datei eine gültige Authenticode-Signatur trägt.</summary>
    public bool IsSigned(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return false;
        }

        try
        {
            using var certificate = new X509Certificate2(X509Certificate.CreateFromSignedFile(filePath));
            return SigningCertificateProfile.Matches(certificate.Subject);
        }
        catch (Exception)
        {
            // Eine unsignierte Datei loest hier eine Ausnahme aus; das ist der Normalfall, kein Fehler.
            return false;
        }
    }

    private static X509Certificate2? FindIn(string storeName)
    {
        using var store = new X509Store(storeName, StoreLocation.LocalMachine);
        store.Open(OpenFlags.ReadOnly);
        return store.Certificates
            .FirstOrDefault(candidate => SigningCertificateProfile.Matches(candidate.Subject));
    }

    private static string? ReadThumbprint(string output)
    {
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 40 && trimmed.All(Uri.IsHexDigit))
            {
                return trimmed;
            }
        }

        return null;
    }

    private static string Summarize(string output)
    {
        var cleaned = output.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return cleaned.Length switch
        {
            0 => "Die PowerShell meldete keinen Grund.",
            > 300 => cleaned[..300],
            _ => cleaned
        };
    }

    private static (int ExitCode, string Output) RunPowerShell(string script)
    {
        var scriptPath = Path.Combine(Path.GetTempPath(), $"ZoneManager-{Guid.NewGuid():N}.ps1");
        try
        {
            File.WriteAllText(scriptPath, script, new UTF8Encoding(true));
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                ArgumentList =
                {
                    "-NoProfile",
                    "-NonInteractive",
                    "-ExecutionPolicy", "Bypass",
                    "-File", scriptPath
                }
            };

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return (-1, "Die PowerShell liess sich nicht starten.");
            }

            var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            return process.WaitForExit(CommandTimeout)
                ? (process.ExitCode, output)
                : (-1, "Die PowerShell hat nicht innerhalb der Zeitgrenze geantwortet.");
        }
        catch (Exception exception)
        {
            return (-1, exception.Message);
        }
        finally
        {
            try
            {
                if (File.Exists(scriptPath))
                {
                    File.Delete(scriptPath);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}
