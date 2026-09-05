namespace SnapZones.Core.Drag;

/// <summary>
/// Entscheidet, ob ein Hook nach einem Sicherheitsstopp von selbst wieder anlaufen darf.
///
/// <para>
/// Die Ereignisgrenze schützt vor einer Rückkopplung, in der das Programm mit jeder eigenen
/// Platzierung neue Ereignisse erzeugt. Sie greift aber auch bei harmloser Last: ein zügig gezogenes
/// Fenster meldet Dutzende Lageänderungen je Sekunde, dazu kommen Animationen anderer Programme. Bis
/// zum 05.09.2026 blieb das Einrasten danach still, bis der Anwender es von Hand wieder einschaltete —
/// meist ohne zu wissen, warum es aufgehört hatte.
/// </para>
///
/// <para>
/// Ein Stopp wegen der Ereignisgrenze wird deshalb nach einer kurzen Ruhezeit von selbst aufgehoben.
/// Häufen sich die Stopps, ist es keine Last mehr, sondern eine Rückkopplung; dann bleibt der Stopp
/// stehen, bis jemand nachsieht. Ein Stopp nach einem Fehler wird nie von selbst aufgehoben.
/// </para>
/// </summary>
public sealed class HookRecoveryPolicy
{
    private readonly int maximumAutomaticResumes;
    private readonly TimeSpan countingWindow;
    private readonly TimeSpan resumeDelay;
    private readonly Queue<DateTimeOffset> stops = new();

    public HookRecoveryPolicy(int maximumAutomaticResumes, TimeSpan countingWindow, TimeSpan resumeDelay)
    {
        if (maximumAutomaticResumes < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumAutomaticResumes));
        }

        if (countingWindow <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(countingWindow));
        }

        if (resumeDelay <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(resumeDelay));
        }

        this.maximumAutomaticResumes = maximumAutomaticResumes;
        this.countingWindow = countingWindow;
        this.resumeDelay = resumeDelay;
    }

    /// <summary>Die Voreinstellung: dreimal je fünf Minuten, jeweils nach zehn Sekunden Ruhe.</summary>
    public static HookRecoveryPolicy Default { get; } =
        new(3, TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(10));

    /// <summary>
    /// Liefert die Wartezeit bis zum Wiederanlauf oder <c>null</c>, wenn der Stopp stehen bleiben muss.
    /// </summary>
    public TimeSpan? Decide(string? reason, DateTimeOffset now)
    {
        if (!HookCircuitBreaker.IsRateLimit(reason))
        {
            return null;
        }

        while (stops.Count > 0 && now - stops.Peek() > countingWindow)
        {
            stops.Dequeue();
        }

        stops.Enqueue(now);
        return stops.Count <= maximumAutomaticResumes ? resumeDelay : null;
    }

    /// <summary>Vergisst die gezählten Stopps, etwa nach einem Wiedereinschalten von Hand.</summary>
    public void Reset() => stops.Clear();
}
