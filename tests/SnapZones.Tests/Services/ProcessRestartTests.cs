using SnapZones.App.Services;
using Xunit;

namespace SnapZones.Tests.Services;

public sealed class ProcessRestartTests
{
    /// <summary>
    /// Der Neustart darf nicht über die Shell laufen. Die Shell prüft die Zone der Programmdatei und legt
    /// für eine Datei auf einem Netzlaufwerk oder mit Herkunftsmarke den Dialog «Datei öffnen -
    /// Sicherheitswarnung» vor; der Start bliebe an ihm hängen, und nach einem Update wartet niemand
    /// darauf. Am 09.09.2026 hat genau dieser Dialog den Prueflauf zweimal stillstehen lassen.
    /// </summary>
    [Fact]
    public void The_successor_starts_without_the_shell()
    {
        var startInfo = ProcessRestart.BuildStartInfo(@"\\server\freigabe\ZoneManager\ZoneManager.exe", []);

        Assert.False(startInfo.UseShellExecute);
        Assert.Empty(startInfo.Verb);
    }

    [Fact]
    public void The_successor_runs_in_the_folder_of_its_own_executable_and_keeps_its_arguments()
    {
        var startInfo = ProcessRestart.BuildStartInfo(
            @"C:\Programme\ZoneManager\ZoneManager.exe",
            ["--autostart", "--wait-for-pid", "4711"]);

        Assert.Equal(@"C:\Programme\ZoneManager\ZoneManager.exe", startInfo.FileName);
        Assert.Equal(@"C:\Programme\ZoneManager", startInfo.WorkingDirectory);
        Assert.Equal(["--autostart", "--wait-for-pid", "4711"], startInfo.ArgumentList);
    }

    /// <summary>
    /// <see cref="ProcessRestart.Warmup"/> übersetzt vorab, was der Neustart braucht — nach dem Austausch
    /// der Programmdatei lässt sich nichts mehr nachladen. Seit <see cref="ProcessRestart.BuildStartInfo"/>
    /// eine eigene Methode ist, gehört sie dazu; der Aufruf muss beide finden und darf nicht werfen.
    /// </summary>
    [Fact]
    public void Warming_up_prepares_the_restart_without_touching_anything()
    {
        ProcessRestart.Warmup();

        var startInfo = ProcessRestart.BuildStartInfo(@"C:\Programme\ZoneManager\ZoneManager.exe", []);
        Assert.False(startInfo.UseShellExecute);
    }
}
