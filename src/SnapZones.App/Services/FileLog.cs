using System.IO;

namespace SnapZones.App.Services;

/// <summary>
/// Einfaches Textprotokoll mit Mindeststufe, vollstaendiger Ausnahmebeschreibung und fuenf
/// Generationen. Frueher stand nur Typ und Meldung einer Ausnahme im Protokoll und jede DEBUG-Zeile
/// verdraengte die interessanten Eintraege nach zwei Megabyte; eine Absturzschleife war damit weder
/// zu erkennen noch zu erklaeren.
/// </summary>
public sealed class FileLog
{
    private const long MaximumBytes = 1_048_576;
    private const int Generations = 5;
    private static readonly string[] Levels = ["DEBUG", "INFO", "WARN", "ERROR", "FATAL"];
    private readonly string filePath;
    private readonly object gate = new();
    private readonly int minimumRank;

    public FileLog(string directoryPath, string minimumLevel = "INFO")
    {
        Directory.CreateDirectory(directoryPath);
        filePath = Path.Combine(directoryPath, "snapzones.log");
        minimumRank = Rank(minimumLevel);
    }

    /// <summary>Pfad der aktuellen Protokolldatei, fuer Hinweise an den Anwender.</summary>
    public string FilePath => filePath;

    /// <summary>Ob Eintraege dieser Stufe ueberhaupt geschrieben werden.</summary>
    public bool IsEnabled(string level) => Rank(level) >= minimumRank;

    public void Write(string level, string message, Exception? exception = null)
    {
        if (!IsEnabled(level))
        {
            return;
        }

        var line = $"{DateTimeOffset.Now:O} [{level}] {message}{Describe(exception)}{Environment.NewLine}";
        lock (gate)
        {
            try
            {
                RotateIfNeeded();
                File.AppendAllText(filePath, line);
            }
            catch (Exception writeException) when (writeException is IOException or UnauthorizedAccessException)
            {
                // Ein nicht schreibbares Protokoll darf nie selbst zum Fehler werden.
            }
        }
    }

    /// <summary>
    /// Beschreibt eine Ausnahme mit Typ, Meldung, innerer Ausnahme und Aufrufstapel. Die Folgezeilen
    /// sind eingerueckt, damit ein Eintrag im Protokoll weiterhin an seinem Zeitstempel erkennbar ist.
    /// </summary>
    internal static string Describe(Exception? exception)
    {
        if (exception is null)
        {
            return string.Empty;
        }

        var header = $" | {exception.GetType().Name}: {exception.Message}";
        var detail = exception.ToString()
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => "    " + line.TrimEnd());
        return header + Environment.NewLine + string.Join(Environment.NewLine, detail);
    }

    private static int Rank(string level)
    {
        var index = Array.FindIndex(Levels, candidate => string.Equals(candidate, level, StringComparison.OrdinalIgnoreCase));
        return index < 0 ? 1 : index;
    }

    private void RotateIfNeeded()
    {
        if (!File.Exists(filePath) || new FileInfo(filePath).Length < MaximumBytes)
        {
            return;
        }

        var oldest = $"{filePath}.{Generations}";
        if (File.Exists(oldest))
        {
            File.Delete(oldest);
        }

        for (var generation = Generations - 1; generation >= 1; generation--)
        {
            var source = $"{filePath}.{generation}";
            if (File.Exists(source))
            {
                File.Move(source, $"{filePath}.{generation + 1}", overwrite: true);
            }
        }

        File.Move(filePath, $"{filePath}.1", overwrite: true);
    }
}
