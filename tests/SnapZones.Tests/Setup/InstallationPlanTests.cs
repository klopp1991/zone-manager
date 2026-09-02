using SnapZones.App.Services;
using SnapZones.Core.Setup;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.Setup;

/// <summary>
/// Die Installation ist ein Modus derselben Programmdatei, kein eigenes Setup-Programm. Ein getrenntes
/// Setup müsste die 66 MB grosse Programmdatei ein zweites Mal enthalten.
/// </summary>
public sealed class InstallationPlanTests
{
    private const string ProgramFiles = @"C:\Program Files";
    private const string StartMenu = @"C:\ProgramData\Microsoft\Windows\Start Menu\Programs";

    [Fact]
    public void An_uninstalled_program_is_planned_into_the_program_files_directory()
    {
        // Ein eigenes leeres Verzeichnis statt des echten «Programme»: sonst haengt das Ergebnis daran,
        // ob das Programm auf dem pruefenden Rechner gerade installiert ist.
        using var directory = new TemporaryDirectory();
        var programFiles = directory.Path;

        var plan = InstallationPlan.Create(@"C:\Users\Beispiel\Downloads\ZoneManager.exe", programFiles, StartMenu);

        Assert.Equal(InstallationState.NotInstalled, plan.State);
        Assert.Equal(Path.Combine(programFiles, InstallationPlan.DirectoryName), plan.TargetDirectory);
        Assert.Equal(
            Path.Combine(programFiles, InstallationPlan.DirectoryName, InstallationPlan.ExecutableName),
            plan.TargetPath);
        Assert.Equal(@"C:\ProgramData\Microsoft\Windows\Start Menu\Programs\Zone Manager.lnk", plan.ShortcutPath);
        Assert.True(plan.RequiresCopy);
    }

    [Fact]
    public void Running_from_the_installation_directory_needs_no_copy()
    {
        var plan = InstallationPlan.Create(
            @"C:\Program Files\ZoneManager\ZoneManager.exe",
            ProgramFiles,
            StartMenu);

        Assert.Equal(InstallationState.AlreadyInstalled, plan.State);
        Assert.False(plan.RequiresCopy);
    }

    [Fact]
    public void The_state_ignores_upper_and_lower_case_and_redundant_path_parts()
    {
        var plan = InstallationPlan.Create(
            @"C:\program files\zonemanager\.\ZONEMANAGER.EXE",
            ProgramFiles,
            StartMenu);

        Assert.Equal(InstallationState.AlreadyInstalled, plan.State);
    }

    [Fact]
    public void An_existing_installation_elsewhere_is_recognised_as_an_upgrade()
    {
        using var directory = new TemporaryDirectory();
        var programFiles = directory.Path;
        Directory.CreateDirectory(Path.Combine(programFiles, InstallationPlan.DirectoryName));
        File.WriteAllText(
            Path.Combine(programFiles, InstallationPlan.DirectoryName, InstallationPlan.ExecutableName),
            "alt");

        var plan = InstallationPlan.Create(@"C:\Users\Beispiel\Downloads\ZoneManager.exe", programFiles, StartMenu);

        Assert.Equal(InstallationState.UpgradeInPlace, plan.State);
        Assert.True(plan.RequiresCopy);
    }

    [Fact]
    public void The_entry_in_apps_and_features_can_uninstall_itself_and_offers_nothing_it_cannot_do()
    {
        var plan = InstallationPlan.Create(@"C:\Downloads\ZoneManager.exe", ProgramFiles, StartMenu);

        var entry = plan.BuildUninstallEntry("2026.0901.01");

        Assert.Equal("Zone Manager", entry["DisplayName"]);
        Assert.Equal("2026.0901.01", entry["DisplayVersion"]);
        Assert.Equal(@"""C:\Program Files\ZoneManager\ZoneManager.exe"" --uninstall", entry["UninstallString"]);
        Assert.Equal(
            @"""C:\Program Files\ZoneManager\ZoneManager.exe"" --uninstall --silent",
            entry["QuietUninstallString"]);

        // Ohne NoModify und NoRepair boete Windows Schaltflaechen an, die ins Leere fuehren.
        Assert.Equal("1", entry["NoModify"]);
        Assert.Equal("1", entry["NoRepair"]);
    }

    [Theory]
    [InlineData(new string[0], SetupRunner.Mode.None)]
    [InlineData(new[] { "--install" }, SetupRunner.Mode.Install)]
    [InlineData(new[] { "--INSTALL" }, SetupRunner.Mode.Install)]
    [InlineData(new[] { "--uninstall" }, SetupRunner.Mode.Uninstall)]
    [InlineData(new[] { "--uninstall", "--silent" }, SetupRunner.Mode.Uninstall)]
    [InlineData(new[] { "--autostart" }, SetupRunner.Mode.None)]
    public void The_command_line_selects_the_setup_mode(string[] arguments, SetupRunner.Mode expected) =>
        Assert.Equal(expected, SetupRunner.Decide(arguments));

    [Fact]
    public void Uninstalling_wins_over_installing_when_both_are_given()
    {
        // «Apps und Features» meint Entfernen; die zerstoerungsfreie Auslegung waere hier die falsche.
        Assert.Equal(SetupRunner.Mode.Uninstall, SetupRunner.Decide(["--install", "--uninstall"]));
    }

    [Fact]
    public void The_silent_switch_is_recognised_on_its_own()
    {
        Assert.True(SetupRunner.IsSilent(["--uninstall", "--silent"]));
        Assert.False(SetupRunner.IsSilent(["--uninstall"]));
    }
}
