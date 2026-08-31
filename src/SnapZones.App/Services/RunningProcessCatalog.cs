using System.Diagnostics;

namespace SnapZones.App.Services;

/// <summary>
/// Ein laufendes Programm, das als Ziel einer App-Regel in Frage kommt.
/// </summary>
/// <param name="DisplayName">Programmname ohne Pfad, zum Beispiel <c>Teams.exe</c>.</param>
/// <param name="ProcessPath">Vollständiger Pfad zur Programmdatei, sofern lesbar; sonst der Programmname.</param>
/// <param name="WindowTitle">Titel des Hauptfensters; leer, wenn keiner gelesen werden konnte.</param>
public sealed record RunningProcessEntry(string DisplayName, string ProcessPath, string WindowTitle)
{
    public bool HasFullPath => ProcessPath.Contains('\\', StringComparison.Ordinal);

    /// <summary>
    /// Der Wert, der in die Regel übernommen wird: nur der Dateiname, nicht der vollständige Pfad.
    /// Viele Programme installieren sich in ein Verzeichnis mit Versionsnummer – etwa
    /// <c>…\AnthropicClaude\app-1.2.3\claude.exe</c>. Eine Regel auf diesen Pfad hört beim nächsten
    /// Update auf zu greifen, während <c>claude.exe</c> unabhängig vom Installationsort trifft.
    /// </summary>
    public string RuleIdentity => DisplayName;
}

/// <summary>
/// Sammelt die laufenden Programme mit sichtbarem Fenster für die Prozessauswahl der App-Regeln.
/// Die Aufbereitung ist von der Systemabfrage getrennt, damit sie ohne echte Prozesse prüfbar bleibt.
/// </summary>
public static class RunningProcessCatalog
{
    /// <summary>
    /// Entfernt Doppelnennungen desselben Programms, bevorzugt Einträge mit vollständigem Pfad und
    /// sortiert alphabetisch nach dem angezeigten Namen.
    /// </summary>
    public static IReadOnlyList<RunningProcessEntry> Normalize(IEnumerable<RunningProcessEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return entries
            .Where(entry => !string.IsNullOrWhiteSpace(entry.DisplayName))
            .GroupBy(entry => entry.ProcessPath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(entry => entry.WindowTitle.Length).First())
            .GroupBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.OrderByDescending(entry => entry.HasFullPath)
                .ThenByDescending(entry => entry.WindowTitle.Length)
                .First())
            .OrderBy(entry => entry.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Filtert nach einem freien Suchbegriff über Programmname, Pfad und Fenstertitel.
    /// Ein leerer Begriff liefert die vollständige Liste.
    /// </summary>
    public static IReadOnlyList<RunningProcessEntry> Filter(
        IReadOnlyList<RunningProcessEntry> entries,
        string? query)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var normalized = query?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            return entries;
        }

        return entries
            .Where(entry =>
                entry.DisplayName.Contains(normalized, StringComparison.CurrentCultureIgnoreCase) ||
                entry.ProcessPath.Contains(normalized, StringComparison.CurrentCultureIgnoreCase) ||
                entry.WindowTitle.Contains(normalized, StringComparison.CurrentCultureIgnoreCase))
            .ToArray();
    }

    /// <summary>
    /// Liest die laufenden Programme mit sichtbarem Hauptfenster aus dem System.
    /// Prozesse, deren Pfad ohne Administratorrechte nicht lesbar ist, bleiben mit ihrem Namen enthalten.
    /// </summary>
    public static IReadOnlyList<RunningProcessEntry> FromSystem()
    {
        var entries = new List<RunningProcessEntry>();
        foreach (var process in Process.GetProcesses())
        {
            try
            {
                if (process.MainWindowHandle == nint.Zero)
                {
                    continue;
                }

                var title = process.MainWindowTitle ?? string.Empty;
                var path = TryReadPath(process);
                var name = path.Length > 0
                    ? System.IO.Path.GetFileName(path)
                    : $"{process.ProcessName}.exe";
                entries.Add(new RunningProcessEntry(name, path.Length > 0 ? path : name, title));
            }
            catch (Exception)
            {
                // Prozesse, die zwischenzeitlich beendet wurden oder nicht gelesen werden dürfen, werden übersprungen.
            }
            finally
            {
                process.Dispose();
            }
        }

        return Normalize(entries);
    }

    private static string TryReadPath(Process process)
    {
        try
        {
            return process.MainModule?.FileName ?? string.Empty;
        }
        catch (Exception)
        {
            // Erhöhte oder geschützte Prozesse liefern hier eine Ausnahme; der Name genügt dann als Regelziel.
            return string.Empty;
        }
    }
}
