using System.IO;

namespace SnapZones.Core.AppRules;

/// <summary>
/// Ein Fenster, das die Anwendung vollständig in Ruhe lässt. Für ein ausgeschlossenes Fenster erscheint
/// beim Ziehen kein Overlay, es rastet nicht ein, keine App-Regel greift und seine Position wird weder
/// gemerkt noch wiederhergestellt. Es behält damit dauerhaft seine eigene Grösse und Position.
///
/// Ein Ausschluss beschreibt das Fenster nach denselben drei Merkmalen wie eine App-Regel; leer gelassene
/// Merkmale schränken nicht ein.
/// </summary>
public sealed record AppExclusion(
    Guid Id,
    string ProcessPath,
    string? WindowTitlePattern,
    string? WindowClass,
    bool IsEnabled)
{
    /// <summary>Kurzer, lesbarer Name für Listen; nie der vollständige Pfad, der ist zu lang.</summary>
    public string DisplayName
    {
        get
        {
            var title = WindowTitlePattern?.Trim();
            if (!string.IsNullOrEmpty(title))
            {
                return title;
            }

            if (!string.IsNullOrWhiteSpace(ProcessPath))
            {
                return ProcessFileName;
            }

            var windowClass = WindowClass?.Trim();
            return string.IsNullOrEmpty(windowClass) ? "Neuer Ausschluss" : windowClass;
        }
    }

    /// <summary>Ob der Ausschluss ein Merkmal nennt, an dem ein Fenster erkannt werden kann.</summary>
    public bool HasCriteria => AppCriteria.HasCriteria(ProcessPath, WindowTitlePattern, WindowClass);

    /// <summary>Der Dateiname des Programms ohne Verzeichnis, etwa <c>notepad.exe</c>.</summary>
    public string ProcessFileName
    {
        get
        {
            var path = ProcessPath?.Trim().Trim('"') ?? string.Empty;
            if (path.Length == 0)
            {
                return "Kein Programm gewählt";
            }

            var name = Path.GetFileName(path);
            return name.Length == 0 ? path : name;
        }
    }
}

public static class AppExclusionMatcher
{
    /// <summary>
    /// Ob das Fenster von mindestens einem aktiven Ausschluss erfasst wird. Ein Ausschluss ist bewusst
    /// keine Regel mit Ziel: er kennt keine Priorität und keinen Konflikt, denn mehrere zutreffende
    /// Ausschlüsse führen zum selben Ergebnis.
    /// </summary>
    public static bool IsExcluded(
        IEnumerable<AppExclusion>? exclusions,
        AppWindowIdentity? window)
    {
        if (exclusions is null || window is null)
        {
            return false;
        }

        return exclusions.Any(exclusion =>
            exclusion.IsEnabled &&
            AppCriteria.Matches(
                exclusion.ProcessPath,
                exclusion.WindowTitlePattern,
                exclusion.WindowClass,
                window));
    }
}
