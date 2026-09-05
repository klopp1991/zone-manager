using System.Windows.Controls;
using System.Windows.Forms;
using SnapZones.App.Services;
using SnapZones.App.ViewModels;
using SnapZones.App.Views;
using SnapZones.Core.Drag;
using SnapZones.Tests.Support;
using SnapZones.Tests.Theme;
using Xunit;

namespace SnapZones.Tests.Services;

/// <summary>
/// Der Zustand der Snap-Funktion muss sichtbar sein und ein Stopp muss sich aufheben lassen. Bis zum
/// 02.09.2026 gab es weder das eine noch das andere: StatusMessage war nirgends gebunden, und ein Not-Aus
/// hielt bis zum Programmneustart.
/// </summary>
public sealed class SnappingStateTests
{
    [Fact]
    public void The_view_model_names_the_state_in_words()
    {
        var viewModel = new MainViewModel(ConfigurationSamples.TwoLayouts(), []);

        viewModel.SnappingState = SnappingState.Active;
        Assert.Equal("Einrasten aktiv", viewModel.SnappingStateLabel);
        Assert.False(viewModel.IsSnappingPaused);

        viewModel.SnappingState = SnappingState.Paused;
        Assert.Equal("Einrasten angehalten", viewModel.SnappingStateLabel);
        Assert.True(viewModel.IsSnappingPaused);

        viewModel.SnappingState = SnappingState.NoActiveLayout;
        Assert.Equal("Kein aktives Layout", viewModel.SnappingStateLabel);
    }

    [Fact]
    public void Resume_is_only_a_request_the_controller_answers()
    {
        var viewModel = new MainViewModel(ConfigurationSamples.TwoLayouts(), []);
        var requests = 0;
        viewModel.ResumeSnappingRequested += () => requests++;

        viewModel.ResumeSnapping();

        Assert.Equal(1, requests);
    }

    [Fact]
    public void The_status_bar_shows_state_message_and_the_resume_button_only_while_paused()
    {
        WpfThemeHost.Invoke(() =>
        {
            var window = new MainWindow();
            var viewModel = new MainViewModel(ConfigurationSamples.TwoLayouts(), []);
            window.AttachViewModel(viewModel);
            window.Show();
            var pausedBox = Assert.IsType<System.Windows.Controls.Border>(window.FindName("PausedBox"));
            var stateText = Assert.IsType<TextBlock>(window.FindName("SnappingStateText"));
            var messageText = Assert.IsType<TextBlock>(window.FindName("StatusMessageText"));
            var resumeButton = Assert.IsType<System.Windows.Controls.Button>(window.FindName("ResumeSnappingButton"));

            viewModel.SnappingState = SnappingState.Active;
            viewModel.StatusMessage = "Speichern fehlgeschlagen: Datenträger voll";
            window.UpdateLayout();

            // Ein laufendes Einrasten braucht keine Pille; die Warnung erscheint nur, wenn es angehalten ist.
            Assert.Equal(System.Windows.Visibility.Collapsed, pausedBox.Visibility);
            Assert.Equal("Speichern fehlgeschlagen: Datenträger voll", messageText.Text);

            viewModel.SnappingState = SnappingState.Paused;
            window.UpdateLayout();

            Assert.Equal(System.Windows.Visibility.Visible, pausedBox.Visibility);
            Assert.Equal("Einrasten angehalten", stateText.Text);
            Assert.True(resumeButton.IsVisible);
            window.Close();
        });
    }

    [Fact]
    public void The_tray_menu_names_the_state_and_offers_resuming_while_paused()
    {
        WpfThemeHost.Invoke(() =>
        {
            var resumed = 0;
            using var service = new TrayIconService(new MainWindow(), _ => { }, () => { }, () => resumed++);
            service.Update(ConfigurationSamples.TwoLayouts());

            service.SetSnappingState("Einrasten aktiv", paused: false);
            // Flach: kein Eintrag fuer den Normalzustand, kein Kopf «Layouts pro Monitor», keine Untermenues.
            Assert.DoesNotContain(service.Menu.Items.Cast<ToolStripItem>(), item => item.Text == "Einrasten aktiv");
            Assert.DoesNotContain(service.Menu.Items.Cast<ToolStripItem>(), item => item.Text == "Layouts pro Monitor");
            Assert.DoesNotContain(service.Menu.Items.Cast<ToolStripItem>(), item => item.Text == "Einrasten wieder aktivieren");
            Assert.Contains(service.Menu.Items.Cast<ToolStripItem>(), item => item.Text == "MONITOR 1" && !item.Enabled);
            Assert.Contains(service.Menu.Items.OfType<ToolStripMenuItem>(), item => item.Text?.Trim() == "Arbeit" && item.Checked);
            Assert.Contains(service.Menu.Items.OfType<ToolStripMenuItem>(), item => item.Text?.Trim() == "Abend" && !item.Checked);
            Assert.All(service.Menu.Items.OfType<ToolStripMenuItem>(), item => Assert.Empty(item.DropDownItems));

            service.SetSnappingState("Einrasten angehalten", paused: true);
            Assert.Contains(service.Menu.Items.Cast<ToolStripItem>(), item => item.Text == "Einrasten angehalten" && !item.Enabled);
            var resume = service.Menu.Items.Cast<ToolStripItem>().Single(item => item.Text == "Einrasten wieder aktivieren");
            resume.PerformClick();

            Assert.Equal(1, resumed);
            Assert.Contains(service.Menu.Items.Cast<ToolStripItem>(), item => item.Text == "Einstellungen öffnen");
        });
    }

    [Fact]
    public void A_reset_circuit_breaker_accepts_events_again()
    {
        var breaker = new HookCircuitBreaker(2, TimeSpan.FromSeconds(10));
        var start = DateTimeOffset.Parse("2026-09-02T08:00:00Z");
        breaker.RecordEvent(start);
        breaker.RecordEvent(start.AddMilliseconds(1));
        Assert.True(breaker.RecordEvent(start.AddMilliseconds(2)));

        breaker.Reset();

        Assert.False(breaker.IsTripped);
        Assert.Null(breaker.Reason);
        Assert.False(breaker.RecordEvent(start.AddMilliseconds(3)));
    }
}
