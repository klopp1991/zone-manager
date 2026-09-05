using System.IO;
using SnapZones.Windows.Files;

namespace SnapZones.App.Services;

/// <summary>Was mit der eigenen Programmdatei geschehen ist.</summary>
public enum ExecutableChange
{
    /// <summary>Die Datei liegt unverändert am Platz.</summary>
    Unchanged,

    /// <summary>Am Pfad liegt eine andere Datei — ein Update oder ein frischer Build.</summary>
    Replaced,

    /// <summary>Am Pfad liegt keine Datei mehr.</summary>
    Missing,

    /// <summary>Die Datei liess sich gerade nicht lesen; kein Urteil möglich.</summary>
    Unreadable
}

/// <summary>
/// Woran ein Austausch der Programmdatei erkennbar ist: an der Dateikennung des Datenträgers, wo es sie
/// gibt, sonst an Grösse und Zeitstempeln. Die Kennung ist verlässlicher — NTFS gibt einer neuen Datei
/// gleichen Namens kurz nach dem Wegschieben der alten deren Erstellzeit, und ein Kopieren übernimmt
/// die Änderungszeit, sodass eine kopierte Datei an den Zeitstempeln nicht von der alten zu
/// unterscheiden ist.
/// </summary>
public readonly record struct ExecutableIdentity(
    long Length,
    DateTime LastWriteTimeUtc,
    DateTime CreationTimeUtc,
    FileIdentity? File = null)
{
    public static ExecutableIdentity? TryCapture(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return info.Exists
                ? new ExecutableIdentity(info.Length, info.LastWriteTimeUtc, info.CreationTimeUtc, FileIdentity.TryRead(path))
                : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Vergleicht den gemerkten Stand mit dem aktuellen Zustand am Pfad.</summary>
    public ExecutableChange Compare(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                return ExecutableChange.Missing;
            }

            if (File is { } known && FileIdentity.TryRead(path) is { } current)
            {
                // Dieselbe Datei kann als laufende Programmdatei nicht veraendert worden sein; eine
                // andere Kennung ist ein Austausch, was immer die Zeitstempel sagen.
                return known == current ? ExecutableChange.Unchanged : ExecutableChange.Replaced;
            }

            return info.Length == Length &&
                info.LastWriteTimeUtc == LastWriteTimeUtc &&
                info.CreationTimeUtc == CreationTimeUtc
                ? ExecutableChange.Unchanged
                : ExecutableChange.Replaced;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ExecutableChange.Unreadable;
        }
    }
}

/// <summary>
/// Entscheidet aus einer Folge von Beobachtungen, ob die Programmdatei tatsächlich ersetzt oder
/// entfernt wurde. Eine einzelne Beobachtung genügt nicht: auf einem Netzlaufwerk fehlt eine Datei
/// auch einmal für einen Augenblick, und ein Austausch besteht aus zwei Schritten, zwischen denen der
/// Pfad kurz leer ist. Gemeldet wird erst, wenn zwei Beobachtungen nacheinander dasselbe sagen.
/// </summary>
public sealed class ExecutableChangeDetector
{
    private ExecutableChange pending = ExecutableChange.Unchanged;

    /// <summary>Liefert die bestätigte Änderung oder <c>Unchanged</c>, solange noch nichts feststeht.</summary>
    public ExecutableChange Observe(ExecutableChange observation)
    {
        switch (observation)
        {
            case ExecutableChange.Unchanged:
                pending = ExecutableChange.Unchanged;
                return ExecutableChange.Unchanged;
            case ExecutableChange.Unreadable:
                // Kein Urteil: der bisherige Verdacht bleibt stehen, wird aber nicht bestaetigt.
                return ExecutableChange.Unchanged;
            default:
                if (pending == observation)
                {
                    return observation;
                }

                pending = observation;
                return ExecutableChange.Unchanged;
        }
    }
}

/// <summary>
/// Wacht darüber, dass die eigene Programmdatei am Platz bleibt.
///
/// <para>
/// Eine Single-File-Anwendung lädt viele ihrer Bausteine erst bei Bedarf aus der eigenen Programmdatei
/// nach, und zwar über deren Pfad. Wird die Datei unter dem laufenden Prozess ausgetauscht — durch ein
/// Update, einen frischen Build, ein Kopieren von Hand —, scheitert jedes spätere Nachladen mit einer
/// <c>FileNotFoundException</c>: beim Beenden, beim ersten Fehlerdialog, bei der nächsten Updatesuche.
/// Am 03. und 04.09.2026 endete das Programm dreimal so, jeweils Minuten nach einem Build.
/// </para>
///
/// <para>
/// Der Wächter meldet einen bestätigten Austausch, damit die Anwendung geordnet speichern und in die
/// neue Datei hinüberstarten kann, solange alles Nötige noch geladen ist.
/// </para>
/// </summary>
public sealed class ExecutableIntegrityWatch : IDisposable
{
    private readonly string executablePath;
    private readonly ExecutableIdentity? identity;
    private readonly ExecutableChangeDetector detector = new();
    private readonly Action<ExecutableChange> onChanged;
    private readonly System.Threading.Timer timer;
    private int reported;
    private int checking;

    public ExecutableIntegrityWatch(string executablePath, TimeSpan interval, Action<ExecutableChange> onChanged)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        this.executablePath = executablePath;
        this.onChanged = onChanged ?? throw new ArgumentNullException(nameof(onChanged));
        identity = ExecutableIdentity.TryCapture(executablePath);
        var period = interval > TimeSpan.Zero ? interval : TimeSpan.FromSeconds(3);
        timer = new System.Threading.Timer(_ => Check(), null, period, period);
    }

    /// <summary>Ob die Programmdatei beim Start lesbar war; sonst kann nichts gewacht werden.</summary>
    public bool IsArmed => identity is not null;

    private void Check()
    {
        if (identity is not { } known || Volatile.Read(ref reported) != 0)
        {
            return;
        }

        if (Interlocked.Exchange(ref checking, 1) != 0)
        {
            return;
        }

        try
        {
            var change = detector.Observe(known.Compare(executablePath));
            if (change != ExecutableChange.Unchanged && Interlocked.Exchange(ref reported, 1) == 0)
            {
                onChanged(change);
            }
        }
        finally
        {
            Volatile.Write(ref checking, 0);
        }
    }

    public void Dispose() => timer.Dispose();
}
