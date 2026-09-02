using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using SnapZones.App.Services;
using SnapZones.App.ViewModels;
using SnapZones.App.Views;
using SnapZones.Core.Models;
using SnapZones.Tests.Support;
using SnapZones.Tests.Theme;
using SnapZones.Windows.Startup;
using Xunit;

namespace SnapZones.Tests.Services;

/// <summary>
/// Das Programm startet voreingestellt mit gewöhnlichen Rechten und fragt erst nach, wenn es tatsächlich
/// auf ein höher berechtigtes Fenster trifft — und dann höchstens einmal je Sitzung.
/// </summary>
public sealed class LazyElevationTests
{
    [Fact]
    public void The_stored_preference_decides_and_anything_unreadable_stays_on_the_safe_side()
    {
        Assert.Equal(
            ElevationMode.Always,
            ElevationPreference.Parse("""{ "Settings": { "ElevationMode": "Always" } }"""));
        Assert.Equal(
            ElevationMode.WhenNeeded,
            ElevationPreference.Parse("""{ "Settings": { "ElevationMode": "WhenNeeded" } }"""));

        // Aeltere Staende kennen das Feld nicht, beschaedigte Dateien lassen sich nicht lesen. Beides
        // darf nicht dazu fuehren, dass sich das Programm ungefragt erhoeht.
        foreach (var json in new[]
                 {
                     "",
                     "kein JSON",
                     "{}",
                     """{ "Settings": {} }""",
                     """{ "Settings": { "ElevationMode": "Unsinn" } }""",
                     """{ "Settings": { "ElevationMode": 3 } }""",
                     "[]"
                 })
        {
            Assert.Equal(ElevationMode.WhenNeeded, ElevationPreference.Parse(json));
        }
    }

    [Fact]
    public void A_missing_configuration_directory_stays_on_the_safe_side()
    {
        using var directory = new TemporaryDirectory();

        Assert.Equal(ElevationMode.WhenNeeded, ElevationPreference.Read(directory.Path));
        Assert.Equal(ElevationMode.WhenNeeded, ElevationPreference.Read(string.Empty));
    }

    [Fact]
    public void The_stored_preference_is_read_back_from_a_real_file()
    {
        using var directory = new TemporaryDirectory();
        File.WriteAllText(
            Path.Combine(directory.Path, ElevationPreference.FileName),
            """{ "SchemaVersion": 5, "Settings": { "ElevationMode": "Always" } }""");

        Assert.Equal(ElevationMode.Always, ElevationPreference.Read(directory.Path));
    }

    [Fact]
    public void The_question_is_asked_once_per_session_and_never_again()
    {
        var questions = 0;
        var escalation = new ElevationEscalation(_ => { questions++; return false; });

        var first = escalation.Offer(@"C:\ZoneManager.exe", []);
        var second = escalation.Offer(@"C:\ZoneManager.exe", []);

        Assert.Equal(ElevationEscalationStatus.Declined, first.Status);
        Assert.Equal(ElevationEscalationStatus.AlreadyOffered, second.Status);
        Assert.Equal(1, questions);
        Assert.True(escalation.HasOffered);
    }

    [Fact]
    public void Agreeing_restarts_elevated_and_keeps_every_argument()
    {
        ProcessStartInfo? captured = null;
        var escalation = new ElevationEscalation(
            _ => true,
            startInfo => { captured = startInfo; return true; });

        var result = escalation.Offer(@"C:\Program Files\ZoneManager\ZoneManager.exe", ["--autostart"]);

        Assert.Equal(ElevationEscalationStatus.Restarting, result.Status);
        Assert.NotNull(captured);
        Assert.Equal("runas", captured.Verb);
        Assert.True(captured.UseShellExecute);
        Assert.Equal(@"C:\Program Files\ZoneManager", captured.WorkingDirectory);
        Assert.Equal(["--autostart"], captured.ArgumentList);
    }

    [Fact]
    public void A_cancelled_windows_prompt_is_reported_without_stopping_the_program()
    {
        var escalation = new ElevationEscalation(_ => true, _ => throw new Win32Exception(1223));

        var result = escalation.Offer(@"C:\ZoneManager.exe", []);

        Assert.Equal(ElevationEscalationStatus.Failed, result.Status);
        Assert.Contains("abgebrochen", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_question_explains_the_cause_the_consequence_and_that_no_is_harmless()
    {
        var question = ElevationEscalation.BuildQuestion();

        Assert.Contains("höheren Rechten", question, StringComparison.Ordinal);
        Assert.Contains("Taskmanager", question, StringComparison.Ordinal);
        Assert.Contains("nicht noch einmal", question, StringComparison.Ordinal);
        Assert.True(question.Length >= 300, "Die Nachfrage muss den Sachverhalt erklären, nicht nur melden.");
    }

    [Fact]
    public void The_logon_task_is_only_elevated_when_the_program_itself_is()
    {
        var elevated = ScheduledTaskStartupService.BuildDefinitionXml(
            @"C:\ZoneManager.exe", @"RECHNER\Sascha", elevated: true);
        var ordinary = ScheduledTaskStartupService.BuildDefinitionXml(
            @"C:\ZoneManager.exe", @"RECHNER\Sascha", elevated: false);

        // Sonst waere der Autostart maechtiger als jeder Start von Hand.
        Assert.Contains("<RunLevel>HighestAvailable</RunLevel>", elevated, StringComparison.Ordinal);
        Assert.Contains("<RunLevel>LeastPrivilege</RunLevel>", ordinary, StringComparison.Ordinal);
    }

    [Fact]
    public void The_settings_page_names_both_choices_and_says_when_they_take_effect()
    {
        WpfThemeHost.Invoke(() =>
        {
            var window = new MainWindow();
            window.AttachViewModel(new MainViewModel(ConfigurationSamples.TwoLayouts(), []));

            var selector = Assert.IsType<ComboBox>(window.FindName("ElevationModeSelector"));
            var hint = Assert.IsType<TextBlock>(window.FindName("ElevationHintText"));
            var help = Assert.IsType<Button>(window.FindName("ElevationInfoButton"));

            Assert.Equal(
                "Settings.ElevationModes",
                selector.GetBindingExpression(ItemsControl.ItemsSourceProperty)!.ParentBinding.Path.Path);
            Assert.Equal(
                "Settings.ElevationMode",
                selector.GetBindingExpression(Selector.SelectedItemProperty)!.ParentBinding.Path.Path);
            Assert.Contains("nächsten Start", hint.Text, StringComparison.Ordinal);

            var tooltip = Assert.IsType<string>(help.ToolTip);
            Assert.True(tooltip.Length >= 120);
            Assert.Contains("Vertrauensstufen", tooltip, StringComparison.Ordinal);
        });
    }
}
