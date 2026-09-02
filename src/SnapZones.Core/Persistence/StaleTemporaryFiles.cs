namespace SnapZones.Core.Persistence;

/// <summary>
/// Raeumt Temp-Dateien eines abgebrochenen Schreibvorgangs auf. Das atomare Schreiben legt zuerst eine
/// Temp-Datei an und tauscht sie danach ein; endet der Prozess dazwischen, bleibt die Temp-Datei liegen
/// und sammelte sich frueher ueber Monate an. Beim naechsten Laden verschwindet sie hier.
/// </summary>
public static class StaleTemporaryFiles
{
    private static readonly TimeSpan MinimumAge = TimeSpan.FromHours(1);

    /// <summary>Loescht Dateien nach Muster, die aelter als eine Stunde sind. Fehler werden verschluckt.</summary>
    /// <returns>Anzahl der entfernten Dateien.</returns>
    public static int Remove(string directoryPath, string searchPattern, TimeProvider? timeProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(searchPattern);
        if (!Directory.Exists(directoryPath))
        {
            return 0;
        }

        var now = (timeProvider ?? TimeProvider.System).GetUtcNow();
        var removed = 0;
        foreach (var path in Directory.EnumerateFiles(directoryPath, searchPattern))
        {
            try
            {
                if (now - File.GetLastWriteTimeUtc(path) < MinimumAge)
                {
                    continue;
                }

                File.Delete(path);
                removed++;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Eine gerade noch benutzte Temp-Datei bleibt liegen und kommt beim naechsten Mal dran.
            }
        }

        return removed;
    }
}
