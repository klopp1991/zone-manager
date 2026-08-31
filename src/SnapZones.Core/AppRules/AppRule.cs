using System.IO;

namespace SnapZones.Core.AppRules;

public enum AppRuleEvent
{
    WindowCreated,
    WindowFocused,
    LayoutActivated
}

public sealed record AppRule(
    Guid Id,
    string ProcessPath,
    string? WindowTitlePattern,
    string? WindowClass,
    AppRuleEvent Event,
    int DelayMilliseconds,
    int RetryCount,
    int Priority,
    bool IsEnabled,
    Guid TargetLayoutId,
    Guid TargetZoneId)
{
    /// <summary>
    /// Kurzer, lesbarer Name für Listen. Bevorzugt das Titelmuster, weil es dem entspricht, was in der
    /// Titelzeile des Fensters steht; sonst der Dateiname des Programms, sonst die Fensterklasse. Der
    /// vollständige Pfad ist als Überschrift ungeeignet: er ist lang und enthält bei vielen Programmen
    /// eine Versionsnummer.
    /// </summary>
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
            return string.IsNullOrEmpty(windowClass) ? "Neue Regel" : windowClass;
        }
    }

    /// <summary>
    /// Ob die Regel überhaupt ein Merkmal nennt, an dem ein Fenster erkannt werden kann. Programm,
    /// Titelmuster und Fensterklasse sind gleichrangig: jedes einzelne genügt, damit die Regel greift.
    /// Eine Regel ohne jedes Merkmal würde auf jedes Fenster passen und bleibt deshalb wirkungslos.
    /// </summary>
    public bool HasCriteria =>
        !string.IsNullOrWhiteSpace(ProcessPath) ||
        !string.IsNullOrWhiteSpace(WindowTitlePattern) ||
        !string.IsNullOrWhiteSpace(WindowClass);

    /// <summary>Der Dateiname des Programms ohne Verzeichnis, etwa <c>claude.exe</c>.</summary>
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

public sealed record AppWindowIdentity(
    int ProcessId,
    string ProcessPath,
    string WindowTitle,
    string WindowClass);
