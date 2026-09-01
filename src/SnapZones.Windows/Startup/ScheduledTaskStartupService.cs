using System.Diagnostics;
using System.IO;
using System.Security;
using System.Text;

namespace SnapZones.Windows.Startup;

/// <summary>
/// Autostart über eine Aufgabe der Windows-Aufgabenplanung statt über den Registry-Schlüssel «Run».
///
/// Der Grund ist die UAC-Abfrage. Ein Eintrag unter «Run» startet das Programm mit normalen Rechten;
/// weil es sich anschliessend selbst erhöht, erscheint bei jeder Anmeldung eine Abfrage, die bestätigt
/// werden muss, bevor der Autostart überhaupt wirkt. Eine Anmeldeaufgabe mit
/// <c>RunLevel=HighestAvailable</c> startet dagegen unmittelbar erhöht und ohne Abfrage.
///
/// Angesprochen wird die Aufgabenplanung über <c>schtasks.exe</c> mit einer Aufgabendefinition in XML.
/// Das kommt ohne COM-Interop aus und ist an einer Stelle nachlesbar. Das Anlegen der Aufgabe verlangt
/// Administratorrechte; die besitzt das Programm im Normalbetrieb bereits.
/// </summary>
public sealed class ScheduledTaskStartupService : IStartupService
{
    /// <summary>Name der Anmeldeaufgabe. Wird nicht geändert, sonst entsteht bei einem Update eine zweite.</summary>
    public const string TaskName = "ZoneManager Autostart";

    private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(20);
    private readonly string executablePath;
    private readonly string userId;
    private readonly Func<bool> elevated;
    private readonly Func<IReadOnlyList<string>, string?, ProcessResult> runScheduleTasks;

    public ScheduledTaskStartupService(string executablePath, Func<bool>? elevated = null)
        : this(executablePath, BuildUserId(), RunScheduleTasks, elevated)
    {
    }

    /// <summary>Für Tests: der Aufruf von <c>schtasks.exe</c> ist austauschbar.</summary>
    public ScheduledTaskStartupService(
        string executablePath,
        string userId,
        Func<IReadOnlyList<string>, string?, ProcessResult> runScheduleTasks,
        Func<bool>? elevated = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(runScheduleTasks);
        this.executablePath = executablePath;
        this.userId = userId;
        this.runScheduleTasks = runScheduleTasks;
        this.elevated = elevated ?? (() => false);
    }

    public sealed record ProcessResult(int ExitCode, string Output);

    public bool IsEnabled
    {
        get
        {
            var result = runScheduleTasks(["/Query", "/TN", TaskName, "/XML", "ONE"], null);
            // Eine Aufgabe gleichen Namens, die auf ein anderes Programm zeigt, gilt als nicht
            // eingerichtet. Sonst meldete eine Altlast Autostart, ohne dass dieser Stand startet.
            return result.ExitCode == 0 && DescribesThisExecutable(result.Output);
        }
    }

    public void SetEnabled(bool enabled)
    {
        if (!enabled)
        {
            _ = runScheduleTasks(["/Delete", "/TN", TaskName, "/F"], null);
            return;
        }

        var result = runScheduleTasks(
            ["/Create", "/TN", TaskName, "/XML", DefinitionPlaceholder, "/F"],
            BuildDefinitionXml(executablePath, userId, elevated()));
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "Die Autostart-Aufgabe konnte nicht angelegt werden. " +
                (string.IsNullOrWhiteSpace(result.Output) ? "Die Aufgabenplanung meldete keinen Grund." : result.Output.Trim()));
        }
    }

    /// <summary>
    /// Platzhalter im Argumentbaustein, den der Aufrufer durch den Pfad der geschriebenen XML-Datei
    /// ersetzt. So bleibt die Argumentliste eine reine Funktion und ist prüfbar.
    /// </summary>
    public const string DefinitionPlaceholder = "<definition>";

    /// <summary>
    /// Die Aufgabendefinition. <c>LogonTrigger</c> auf den angemeldeten Benutzer,
    /// <c>InteractiveToken</c> damit die Oberfläche sichtbar ist, und <c>HighestAvailable</c> für den
    /// Start ohne UAC-Abfrage. Die Standardgrenzen der Aufgabenplanung sind bewusst abgeschaltet: eine
    /// Aufgabe, die im Akkubetrieb nicht startet oder nach drei Tagen beendet wird, wäre als Autostart
    /// eines dauerhaft laufenden Programms unbrauchbar.
    /// </summary>
    public static string BuildDefinitionXml(string executablePath, string userId, bool elevated = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var builder = new StringBuilder();
        builder.Append("""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo>
                <Description>Startet Sascha's Zone Manager bei der Anmeldung.</Description>
              </RegistrationInfo>
              <Triggers>
                <LogonTrigger>
                  <Enabled>true</Enabled>
                  <UserId>
            """);
        builder.Append(SecurityElement.Escape(userId));
        builder.Append("""
            </UserId>
                </LogonTrigger>
              </Triggers>
              <Principals>
                <Principal id="Author">
                  <UserId>
            """);
        builder.Append(SecurityElement.Escape(userId));
        builder.Append("""
            </UserId>
                  <LogonType>InteractiveToken</LogonType>
                  <RunLevel>PLATZHALTER_RUNLEVEL</RunLevel>
                </Principal>
              </Principals>
              <Settings>
                <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <AllowHardTerminate>false</AllowHardTerminate>
                <StartWhenAvailable>false</StartWhenAvailable>
                <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
                <IdleSettings>
                  <StopOnIdleEnd>false</StopOnIdleEnd>
                  <RestartOnIdle>false</RestartOnIdle>
                </IdleSettings>
                <AllowStartOnDemand>true</AllowStartOnDemand>
                <Enabled>true</Enabled>
                <Hidden>false</Hidden>
                <RunOnlyIfIdle>false</RunOnlyIfIdle>
                <DisallowStartOnRemoteAppSession>false</DisallowStartOnRemoteAppSession>
                <UseUnifiedSchedulingEngine>true</UseUnifiedSchedulingEngine>
                <WakeToRun>false</WakeToRun>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                <Priority>7</Priority>
              </Settings>
              <Actions Context="Author">
                <Exec>
                  <Command>
            """);
        builder.Append(SecurityElement.Escape(executablePath));
        builder.Append("""
            </Command>
                  <Arguments>--autostart</Arguments>
                </Exec>
              </Actions>
            </Task>
            """);
        // Die Anmeldeaufgabe startet nur dann erhoeht, wenn das Programm auch sonst erhoeht laufen soll.
        // Sonst waere der Autostart maechtiger als jeder Start von Hand.
        return builder.ToString().Replace(
            "PLATZHALTER_RUNLEVEL",
            elevated ? "HighestAvailable" : "LeastPrivilege",
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Entfernt Nullbytes und die Stuecklistenmarke. Beides taucht auf, wenn eine UTF-16-Ausgabe in
    /// einer anderen Zeichentabelle gelesen wird, und beides wuerde jeden Textvergleich stoeren.
    /// </summary>
    private static string Normalize(string output) => output
        .Replace("\0", string.Empty, StringComparison.Ordinal)
        .Replace("\ufeff", string.Empty, StringComparison.Ordinal);

    private bool DescribesThisExecutable(string definitionXml) =>
        definitionXml.Contains(
            "<Command>" + SecurityElement.Escape(executablePath) + "</Command>",
            StringComparison.OrdinalIgnoreCase);

    private static string BuildUserId() =>
        string.IsNullOrWhiteSpace(Environment.UserDomainName)
            ? Environment.UserName
            : $"{Environment.UserDomainName}\\{Environment.UserName}";

    private static ProcessResult RunScheduleTasks(IReadOnlyList<string> arguments, string? definitionXml)
    {
        string? definitionPath = null;
        try
        {
            var effective = arguments;
            if (definitionXml is not null)
            {
                // Die Aufgabenplanung liest die Datei als UTF-16; die XML-Deklaration sagt dasselbe.
                definitionPath = Path.Combine(Path.GetTempPath(), $"ZoneManager-{Guid.NewGuid():N}.xml");
                File.WriteAllText(definitionPath, definitionXml, Encoding.Unicode);
                effective = arguments
                    .Select(argument => argument == DefinitionPlaceholder ? definitionPath : argument)
                    .ToArray();
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            // schtasks gibt eine Aufgabendefinition als UTF-16 aus, alle uebrigen Meldungen in der
            // Zeichentabelle der Konsole. Ohne die passende Angabe stuende zwischen jedem Zeichen ein
            // Nullbyte und der Vergleich des Programmpfads schluege immer fehl.
            if (effective.Contains("/XML"))
            {
                startInfo.StandardOutputEncoding = Encoding.Unicode;
            }
            foreach (var argument in effective)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return new ProcessResult(-1, "schtasks.exe konnte nicht gestartet werden.");
            }

            var output = Normalize(process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd());
            if (!process.WaitForExit(CommandTimeout))
            {
                return new ProcessResult(-1, "schtasks.exe hat nicht innerhalb der Zeitgrenze geantwortet.");
            }

            return new ProcessResult(process.ExitCode, output);
        }
        catch (Exception exception)
        {
            return new ProcessResult(-1, exception.Message);
        }
        finally
        {
            if (definitionPath is not null && File.Exists(definitionPath))
            {
                File.Delete(definitionPath);
            }
        }
    }
}
