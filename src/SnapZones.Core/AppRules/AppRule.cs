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
    /// Titelzeile des Fensters steht; sonst der Dateiname des Programms. Der vollständige Pfad ist als
    /// Überschrift ungeeignet: er ist lang und enthält bei vielen Programmen eine Versionsnummer.
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

            return ProcessFileName;
        }
    }

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
