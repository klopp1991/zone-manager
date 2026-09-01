namespace SnapZones.Windows.Startup;

/// <summary>
/// Richtet den Autostart ein und bevorzugt dabei die Anmeldeaufgabe der Windows-Aufgabenplanung, weil
/// nur sie das Programm ohne UAC-Abfrage erhöht startet. Schlägt das fehl — etwa weil das Programm ohne
/// Administratorrechte läuft oder die Aufgabenplanung gesperrt ist —, greift der bisherige
/// Registry-Schlüssel «Run» als Rückfall. Dann erscheint bei der Anmeldung wieder die UAC-Abfrage,
/// aber der Autostart geht nicht verloren.
///
/// Es ist immer höchstens einer der beiden Wege eingetragen: wird die Aufgabe angelegt, verschwindet
/// der Registry-Eintrag, sonst startete das Programm zweimal.
/// </summary>
public sealed class StartupRegistration : IStartupService
{
    private readonly IStartupService scheduledTask;
    private readonly IStartupService registryRun;
    private readonly Action<string>? reportFallback;

    public StartupRegistration(
        string executablePath,
        Action<string>? reportFallback = null,
        Func<bool>? elevated = null)
        : this(
            new ScheduledTaskStartupService(executablePath, elevated),
            new WindowsStartupService(executablePath),
            reportFallback)
    {
    }

    public StartupRegistration(
        IStartupService scheduledTask,
        IStartupService registryRun,
        Action<string>? reportFallback = null)
    {
        this.scheduledTask = scheduledTask ?? throw new ArgumentNullException(nameof(scheduledTask));
        this.registryRun = registryRun ?? throw new ArgumentNullException(nameof(registryRun));
        this.reportFallback = reportFallback;
    }

    /// <summary>Welcher der beiden Wege gerade eingetragen ist.</summary>
    public StartupMechanism Mechanism => scheduledTask.IsEnabled
        ? StartupMechanism.ScheduledTask
        : registryRun.IsEnabled
            ? StartupMechanism.RegistryRun
            : StartupMechanism.None;

    public bool IsEnabled => Mechanism != StartupMechanism.None;

    public void SetEnabled(bool enabled)
    {
        if (!enabled)
        {
            scheduledTask.SetEnabled(false);
            registryRun.SetEnabled(false);
            return;
        }

        try
        {
            scheduledTask.SetEnabled(true);
            registryRun.SetEnabled(false);
        }
        catch (Exception exception)
        {
            reportFallback?.Invoke(
                "Der Autostart läuft über den Registry-Eintrag, weil die Anmeldeaufgabe nicht angelegt " +
                $"werden konnte. Bei der Anmeldung erscheint deshalb die Windows-UAC-Abfrage. {exception.Message}");
            registryRun.SetEnabled(true);
        }
    }
}

public enum StartupMechanism
{
    None,
    ScheduledTask,
    RegistryRun
}
