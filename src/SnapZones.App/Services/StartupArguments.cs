using System.Globalization;

namespace SnapZones.App.Services;

/// <summary>
/// Die Kommandozeilenschalter des Programms an einer Stelle. Alle Vergleiche sind unabhängig von
/// Gross- und Kleinschreibung.
/// </summary>
public static class StartupArguments
{
    /// <summary>Start aus dem Autostart: das Fenster bleibt im Infobereich.</summary>
    public const string Autostart = "--autostart";

    /// <summary>Schreibt auch die DEBUG-Zeilen ins Protokoll.</summary>
    public const string Verbose = "--verbose";

    /// <summary>Bittet die laufende Instanz, sich zu beenden, und endet selbst sofort.</summary>
    public const string Exit = "--exit";

    /// <summary>
    /// Wartet vor allem anderen, bis der genannte Prozess beendet ist. Ein Neustart — nach einem Update,
    /// nach der Installation, mit Administratorrechten — übergibt so die Einzelinstanz sauber: ohne
    /// diese Wartezeit fand der neue Prozess den alten noch vor, aktivierte ihn und beendete sich.
    /// </summary>
    public const string WaitForPid = "--wait-for-pid";

    /// <summary>Übernimmt eine bereitgestellte Version an die Stelle der genannten Programmdatei.</summary>
    public const string ApplyUpdate = "--apply-update";

    /// <summary>Richtet das Signaturzertifikat ein; läuft ohne Oberfläche und endet danach.</summary>
    public const string InstallCertificate = "--install-certificate";

    /// <summary>Entfernt das Signaturzertifikat; läuft ohne Oberfläche und endet danach.</summary>
    public const string RemoveCertificate = "--remove-certificate";

    /// <summary>
    /// Nach einer Installation nicht das installierte Programm starten. Nötig, wenn die Installation
    /// in einem erhöhten Hilfsprozess läuft: ein von dort gestartetes Programm liefe ebenfalls erhöht.
    /// </summary>
    public const string NoLaunch = "--no-launch";

    /// <summary>
    /// Marker eines bereits versuchten erhöhten Neustarts; verhindert eine Endlosschleife, wenn
    /// Windows den Prozess trotz Bestätigung ohne Administratorrechte startet.
    /// </summary>
    public const string ElevationAttempted = "--elevation-attempted";

    public static bool Contains(IEnumerable<string> arguments, string expected)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        return arguments.Contains(expected, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Liest den Wert hinter einem Schalter, etwa den Pfad hinter <c>--apply-update</c>.</summary>
    public static string? ReadValue(IReadOnlyList<string> arguments, string expected)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        for (var index = 0; index < arguments.Count - 1; index++)
        {
            if (string.Equals(arguments[index], expected, StringComparison.OrdinalIgnoreCase))
            {
                return arguments[index + 1];
            }
        }

        return null;
    }

    public static bool TryReadWaitForPid(IReadOnlyList<string> arguments, out int processId)
    {
        processId = 0;
        return ReadValue(arguments, WaitForPid) is { } value &&
            int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out processId) &&
            processId > 0;
    }

    /// <summary>
    /// Die Argumente, mit denen ein Nachfolgeprozess gestartet wird: dieselben Schalter wie der eigene
    /// Start ohne Einmalmarker, dazu die Anweisung, auf das Ende dieses Prozesses zu warten, und auf
    /// Wunsch der Start im Infobereich.
    /// </summary>
    public static IReadOnlyList<string> ForSuccessor(IReadOnlyList<string> ownArguments, int ownProcessId, bool hidden)
    {
        ArgumentNullException.ThrowIfNull(ownArguments);
        var result = new List<string>(ownArguments.Count + 3);
        for (var index = 0; index < ownArguments.Count; index++)
        {
            var argument = ownArguments[index];
            if (string.Equals(argument, WaitForPid, StringComparison.OrdinalIgnoreCase))
            {
                // Schalter samt Wert des vorherigen Vorgaengers ueberspringen.
                index++;
                continue;
            }

            if (string.Equals(argument, Autostart, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(argument, ElevationAttempted, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            result.Add(argument);
        }

        if (hidden)
        {
            result.Add(Autostart);
        }

        result.Add(WaitForPid);
        result.Add(ownProcessId.ToString(CultureInfo.InvariantCulture));
        return result;
    }
}
