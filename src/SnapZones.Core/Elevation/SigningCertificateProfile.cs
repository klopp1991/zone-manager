using System.Globalization;

namespace SnapZones.Core.Elevation;

/// <summary>
/// Beschreibt das selbst ausgestellte Zertifikat, mit dem das Hilfsprogramm signiert wird, und erzeugt
/// die PowerShell-Befehle dafür.
///
/// Warum überhaupt ein eigenes Zertifikat: Windows startet ein Programm mit <c>uiAccess</c> nur, wenn es
/// eine gültige Authenticode-Signatur trägt. Ein öffentlich anerkanntes Zertifikat kostet Geld und
/// verlangt einen Hardware-Token. Für den Eigengebrauch genügt ein selbst ausgestelltes, dem der eigene
/// Rechner vertraut.
///
/// <para><b>Was das bedeutet.</b> Der Rechner vertraut anschliessend allem, was mit diesem Schlüssel
/// signiert wurde. Der private Schlüssel liegt dabei auf der Maschine. Wer ihn in die Hände bekommt, kann
/// Schadsoftware so signieren, dass Windows sie für vertrauenswürdig hält. Zwei Dinge halten den Schaden
/// klein: das Zertifikat ist <b>keine</b> Zertifizierungsstelle und kann deshalb keine weiteren
/// Zertifikate ausstellen, und sein Schlüssel ist nicht exportierbar.</para>
/// </summary>
public static class SigningCertificateProfile
{
    /// <summary>Name des Zertifikats. Eindeutig genug, um es im Zertifikatspeicher wiederzufinden.</summary>
    public const string Subject = "CN=Zone Manager Fensterhelfer";

    public const string FriendlyName = "Zone Manager – Fensterhelfer";

    /// <summary>
    /// Namen, unter denen frueher dasselbe Zertifikat ausgestellt wurde. Ein bereits eingerichtetes
    /// Zertifikat wird dadurch weiterhin erkannt und laesst sich entfernen, ohne im Zertifikatspeicher
    /// von Hand aufzuraeumen.
    /// </summary>
    public static IReadOnlyList<string> LegacySubjects { get; } =
        ["CN=Sascha's Zone Manager Fensterhelfer", "CN=Sascha’s Zone Manager Fensterhelfer"];

    /// <summary>Ob ein Antragsteller zum eigenen Zertifikat gehoert, dem aktuellen oder einem frueheren.</summary>
    public static bool Matches(string? subject) =>
        subject is not null &&
        (string.Equals(subject, Subject, StringComparison.Ordinal) ||
         LegacySubjects.Any(legacy => string.Equals(subject, legacy, StringComparison.Ordinal)));

    /// <summary>Gültigkeit in Jahren. Lang genug, um nicht zu stören, kurz genug, um nicht ewig zu gelten.</summary>
    public const int ValidityYears = 5;

    /// <summary>
    /// Die drei Speicher, in denen das Zertifikat landet, und wofür jeder gebraucht wird.
    /// <list type="bullet">
    ///   <item><c>My</c> — trägt den privaten Schlüssel; ohne ihn liesse sich nichts signieren.</item>
    ///   <item><c>Root</c> — macht die Signatur für Windows überhaupt gültig.</item>
    ///   <item><c>TrustedPublisher</c> — erspart die Rückfrage beim Ausführen.</item>
    /// </list>
    /// </summary>
    public static IReadOnlyList<string> Stores { get; } = ["My", "Root", "TrustedPublisher"];

    /// <summary>
    /// Der Befehl, der das Zertifikat erzeugt und in die Vertrauensspeicher legt. Er verlangt
    /// Administratorrechte, weil er in den Speicher der lokalen Maschine schreibt.
    /// </summary>
    public static string BuildCreateScript(DateTimeOffset now) =>
        $$"""
        $ErrorActionPreference = 'Stop'
        # Ein reines Signaturzertifikat, ausdruecklich KEINE Zertifizierungsstelle: es kann keine
        # weiteren Zertifikate ausstellen. Der private Schluessel ist nicht exportierbar.
        $certificate = New-SelfSignedCertificate `
            -Type CodeSigningCert `
            -Subject '{{Escape(Subject)}}' `
            -FriendlyName '{{Escape(FriendlyName)}}' `
            -CertStoreLocation 'Cert:\LocalMachine\My' `
            -KeyUsage DigitalSignature `
            -KeyExportPolicy NonExportable `
            -KeyLength 3072 `
            -NotAfter (Get-Date '{{now.UtcDateTime.ToString("o", CultureInfo.InvariantCulture)}}').AddYears({{ValidityYears}})
        foreach ($store in 'Root','TrustedPublisher') {
            $target = New-Object System.Security.Cryptography.X509Certificates.X509Store($store, 'LocalMachine')
            $target.Open('ReadWrite')
            $target.Add($certificate)
            $target.Close()
        }
        Write-Output $certificate.Thumbprint
        """;

    /// <summary>Der Befehl, der das Hilfsprogramm mit diesem Zertifikat signiert.</summary>
    public static string BuildSignScript(string filePath, string thumbprint) =>
        $$"""
        $ErrorActionPreference = 'Stop'
        $certificate = Get-ChildItem 'Cert:\LocalMachine\My\{{Escape(thumbprint)}}'
        $result = Set-AuthenticodeSignature -FilePath '{{Escape(filePath)}}' -Certificate $certificate
        if ($result.Status -ne 'Valid') { throw "Die Signatur ist $($result.Status)." }
        Write-Output $result.Status
        """;

    /// <summary>
    /// Der Befehl, der das Zertifikat aus allen drei Speichern entfernt. Danach startet das
    /// Hilfsprogramm nicht mehr, und das Programm geht wieder seinen bisherigen Weg.
    /// </summary>
    public static string BuildRemoveScript() =>
        $$"""
        $ErrorActionPreference = 'SilentlyContinue'
        $entfernt = 0
        foreach ($store in 'My','Root','TrustedPublisher') {
            $target = New-Object System.Security.Cryptography.X509Certificates.X509Store($store, 'LocalMachine')
            $target.Open('ReadWrite')
            foreach ($candidate in @($target.Certificates)) {
                if ({{RemoveCondition()}}) {
                    $target.Remove($candidate)
                    $entfernt++
                }
            }
            $target.Close()
        }
        Write-Output $entfernt
        """;

    /// <summary>
    /// Die Bedingung, die den aktuellen und jeden frueheren Antragsteller trifft. Ohne die frueheren
    /// Namen bliebe ein unter dem alten Namen ausgestelltes Zertifikat beim Entfernen liegen.
    /// </summary>
    private static string RemoveCondition() =>
        string.Join(
            " -or ",
            new[] { Subject }
                .Concat(LegacySubjects)
                .Select(subject => $"$candidate.Subject -eq '{Escape(subject)}'"));

    /// <summary>
    /// Schützt vor einem Hochkomma, das den umgebenden PowerShell-Text sprengen würde. Alle Werte hier
    /// stammen aus dem Programm selbst, aber ein Dateipfad kommt aus der Umgebung.
    /// </summary>
    public static string Escape(string value) =>
        (value ?? string.Empty).Replace("'", "''", StringComparison.Ordinal);
}
