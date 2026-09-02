using SnapZones.Windows.Startup;
using Xunit;

namespace SnapZones.Tests.Profiles;

/// <summary>
/// Der Autostart läuft über eine Anmeldeaufgabe, weil nur sie das Programm erhöht startet, ohne bei
/// jeder Anmeldung eine UAC-Abfrage zu zeigen. Lässt sich die Aufgabe nicht anlegen, bleibt der
/// Registry-Eintrag als Rückfall — dann erscheint die Abfrage wieder, aber der Autostart geht nicht
/// verloren.
/// </summary>
public sealed class ScheduledTaskStartupServiceTests
{
    private const string Executable = @"C:\Tools\ZoneManager.exe";
    private const string User = @"RECHNER\Benutzer";

    [Fact]
    public void The_definition_starts_elevated_at_logon_without_a_prompt()
    {
        var xml = ScheduledTaskStartupService.BuildDefinitionXml(Executable, User);

        Assert.Contains("<RunLevel>HighestAvailable</RunLevel>", xml, StringComparison.Ordinal);
        Assert.Contains("<LogonType>InteractiveToken</LogonType>", xml, StringComparison.Ordinal);
        Assert.Contains("<LogonTrigger>", xml, StringComparison.Ordinal);
        Assert.Contains($"<UserId>{User}</UserId>", xml, StringComparison.Ordinal);
        Assert.Contains($"<Command>{Executable}</Command>", xml, StringComparison.Ordinal);
        Assert.Contains("<Arguments>--autostart</Arguments>", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void The_definition_lifts_the_limits_that_would_break_a_permanent_program()
    {
        var xml = ScheduledTaskStartupService.BuildDefinitionXml(Executable, User);

        // Ohne diese drei startete die Aufgabe im Akkubetrieb nicht, endete beim Wechsel auf Akku und
        // wuerde nach drei Tagen Laufzeit beendet.
        Assert.Contains("<DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>", xml, StringComparison.Ordinal);
        Assert.Contains("<StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>", xml, StringComparison.Ordinal);
        Assert.Contains("<ExecutionTimeLimit>PT0S</ExecutionTimeLimit>", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void Characters_with_a_meaning_in_xml_are_escaped()
    {
        var xml = ScheduledTaskStartupService.BuildDefinitionXml(@"C:\A & B\ZoneManager.exe", @"DOM\Bär<>");

        Assert.Contains(@"<Command>C:\A &amp; B\ZoneManager.exe</Command>", xml, StringComparison.Ordinal);
        Assert.Contains(@"<UserId>DOM\Bär&lt;&gt;</UserId>", xml, StringComparison.Ordinal);
    }

    [Fact]
    public void Enabling_creates_the_task_from_a_definition_and_overwrites_an_older_one()
    {
        var runner = new FakeScheduleTasks();
        var service = new ScheduledTaskStartupService(Executable, User, runner.Run);

        service.SetEnabled(true);

        var call = Assert.Single(runner.Calls);
        Assert.Equal(["/Create", "/TN", ScheduledTaskStartupService.TaskName, "/XML", ScheduledTaskStartupService.DefinitionPlaceholder, "/F"], call.Arguments);
        Assert.NotNull(call.DefinitionXml);
        Assert.Contains($"<Command>{Executable}</Command>", call.DefinitionXml, StringComparison.Ordinal);
    }

    [Fact]
    public void Disabling_removes_the_task_and_passes_no_definition()
    {
        var runner = new FakeScheduleTasks();
        var service = new ScheduledTaskStartupService(Executable, User, runner.Run);

        service.SetEnabled(false);

        var call = Assert.Single(runner.Calls);
        Assert.Equal(["/Delete", "/TN", ScheduledTaskStartupService.TaskName, "/F"], call.Arguments);
        Assert.Null(call.DefinitionXml);
    }

    [Fact]
    public void A_failed_creation_reports_the_reason_given_by_the_task_scheduler()
    {
        var runner = new FakeScheduleTasks { ExitCode = 1, Output = "Zugriff verweigert." };
        var service = new ScheduledTaskStartupService(Executable, User, runner.Run);

        var exception = Assert.Throws<InvalidOperationException>(() => service.SetEnabled(true));

        Assert.Contains("Zugriff verweigert.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_task_pointing_at_a_different_program_does_not_count_as_configured()
    {
        // Nach einem Verschieben der EXE zeigt eine alte Aufgabe ins Leere. Sie darf nicht als
        // eingerichteter Autostart gelten, sonst startet das Programm nie und meldet trotzdem Erfolg.
        var stale = new FakeScheduleTasks
        {
            Output = ScheduledTaskStartupService.BuildDefinitionXml(@"D:\Alt\ZoneManager.exe", User)
        };
        var matching = new FakeScheduleTasks
        {
            Output = ScheduledTaskStartupService.BuildDefinitionXml(Executable, User)
        };

        Assert.False(new ScheduledTaskStartupService(Executable, User, stale.Run).IsEnabled);
        Assert.True(new ScheduledTaskStartupService(Executable, User, matching.Run).IsEnabled);
    }

    [Fact]
    public void A_missing_task_means_no_autostart()
    {
        var runner = new FakeScheduleTasks { ExitCode = 1, Output = "FEHLER: Der angegebene Task ist nicht vorhanden." };

        Assert.False(new ScheduledTaskStartupService(Executable, User, runner.Run).IsEnabled);
    }

    [Fact]
    public void Enabling_prefers_the_task_and_clears_the_registry_entry()
    {
        // Beide Wege gleichzeitig wuerden das Programm bei der Anmeldung zweimal starten.
        var task = new FakeStartup();
        var registry = new FakeStartup { IsEnabled = true };
        var registration = new StartupRegistration(task, registry);

        registration.SetEnabled(true);

        Assert.True(task.IsEnabled);
        Assert.False(registry.IsEnabled);
        Assert.Equal(StartupMechanism.ScheduledTask, registration.Mechanism);
    }

    [Fact]
    public void The_registry_entry_takes_over_when_the_task_cannot_be_created()
    {
        var task = new FakeStartup { ThrowOnEnable = true };
        var registry = new FakeStartup();
        var reported = new List<string>();
        var registration = new StartupRegistration(task, registry, reported.Add);

        registration.SetEnabled(true);

        Assert.True(registry.IsEnabled);
        Assert.Equal(StartupMechanism.RegistryRun, registration.Mechanism);
        Assert.Contains("UAC", Assert.Single(reported), StringComparison.Ordinal);
    }

    [Fact]
    public void Disabling_removes_both_ways()
    {
        var task = new FakeStartup { IsEnabled = true };
        var registry = new FakeStartup { IsEnabled = true };
        var registration = new StartupRegistration(task, registry);

        registration.SetEnabled(false);

        Assert.False(registration.IsEnabled);
        Assert.Equal(StartupMechanism.None, registration.Mechanism);
    }

    private sealed class FakeScheduleTasks
    {
        public int ExitCode { get; init; }
        public string Output { get; init; } = string.Empty;
        public List<(IReadOnlyList<string> Arguments, string? DefinitionXml)> Calls { get; } = [];

        public ScheduledTaskStartupService.ProcessResult Run(
            IReadOnlyList<string> arguments,
            string? definitionXml)
        {
            Calls.Add((arguments, definitionXml));
            return new ScheduledTaskStartupService.ProcessResult(ExitCode, Output);
        }
    }

    private sealed class FakeStartup : IStartupService
    {
        public bool IsEnabled { get; set; }
        public bool ThrowOnEnable { get; init; }

        public void SetEnabled(bool enabled)
        {
            if (enabled && ThrowOnEnable)
            {
                throw new InvalidOperationException("Zugriff verweigert.");
            }

            IsEnabled = enabled;
        }
    }
}
