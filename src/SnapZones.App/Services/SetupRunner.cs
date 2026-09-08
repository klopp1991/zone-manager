using SnapZones.Core.Setup;
using SnapZones.Windows.Displays;
using SnapZones.Windows.Setup;

namespace SnapZones.App.Services;

/// <summary>
/// Die Kommandozeilenmodi <c>--install</c> und <c>--uninstall</c>.
///
/// Beide laufen ohne Hauptfenster und ohne Hook und beenden das Programm danach. Ist <c>--silent</c>
/// gesetzt, erscheint keine Rückmeldung — so ruft «Apps und Features» die Deinstallation auf.
/// </summary>
public static class SetupRunner
{
    public const string InstallArgument = "--install";
    public const string UninstallArgument = "--uninstall";
    public const string SilentArgument = "--silent";

    public enum Mode
    {
        None,
        Install,
        Uninstall
    }

    public static Mode Decide(IEnumerable<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var list = arguments.ToArray();

        // Deinstallieren schlaegt Installieren: stuenden beide da, waere die Absicht unklar, und die
        // zerstoerungsfreie Auslegung waere hier die falsche — «Apps und Features» meint Entfernen.
        if (Contains(list, UninstallArgument))
        {
            return Mode.Uninstall;
        }

        return Contains(list, InstallArgument) ? Mode.Install : Mode.None;
    }

    public static bool IsSilent(IEnumerable<string> arguments) =>
        Contains(arguments.ToArray(), SilentArgument);

    /// <summary>
    /// Führt den gewählten Modus aus und liefert Beendigungscode und Meldung. Der Aufrufer entscheidet,
    /// ob er die Meldung anzeigt.
    /// </summary>
    public static (int ExitCode, string Message, string? StartPath) Run(
        Mode mode,
        string sourcePath,
        string version,
        InstallationService service)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentNullException.ThrowIfNull(service);

        if (mode == Mode.Uninstall)
        {
            // Der Anzeigetreiber fuer Vollbildzonen geht mit dem Programm; sonst bliebe ein
            // Geistermonitor in den Anzeigeeinstellungen zurueck.
            var driverRemoval = DisplayDriverSetup.Remove();
            var removal = service.Uninstall();
            var message = driverRemoval.Outcome == DriverActionOutcome.AlreadyDone
                ? removal.Message
                : removal.Message + " " + driverRemoval.Message;
            return (removal.Outcome == RemovalOutcome.Failed ? 1 : 0, message, null);
        }

        var plan = InstallationService.CreatePlan(sourcePath);
        var result = service.Install(plan, version);
        if (result.Outcome == InstallationOutcome.Failed)
        {
            return (1, result.Message, null);
        }

        // Die Treiberinstallation ist der zweite Schritt desselben erhoehten Prozesses; eine zweite
        // UAC-Abfrage gibt es dafuer nicht. Schlaegt sie fehl, bleibt die Programminstallation gueltig.
        var driver = DisplayDriverSetup.Install();
        var combined = driver.Outcome == DriverActionOutcome.AlreadyDone
            ? result.Message
            : result.Message + " " + driver.Message;
        return result.Outcome switch
        {
            InstallationOutcome.AlreadyCurrent => (0, combined, null),
            _ => (0, combined + " Das Programm wird von dort gestartet.", result.InstalledPath)
        };
    }

    private static bool Contains(IReadOnlyList<string> arguments, string expected) =>
        arguments.Contains(expected, StringComparer.OrdinalIgnoreCase);
}
