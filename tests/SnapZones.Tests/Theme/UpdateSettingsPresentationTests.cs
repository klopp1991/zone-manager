using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using SnapZones.App.ViewModels;
using SnapZones.App.Views;
using SnapZones.Core.Models;
using SnapZones.Tests.Support;
using Xunit;

namespace SnapZones.Tests.Theme;

public sealed class UpdateSettingsPresentationTests
{
    [Fact]
    public void Searching_at_startup_is_switched_off_until_it_is_asked_for()
    {
        // Eine Abfrage geht ins Netz. Das soll das Programm nur tun, wenn es ausdruecklich gewollt ist.
        Assert.False(AppSettings.Default(Guid.Empty).CheckForUpdatesOnStart);

        var settings = new SettingsViewModel(AppSettings.Default(Guid.Empty)) { CheckForUpdatesOnStart = true };

        Assert.True(settings.CreateSettings().CheckForUpdatesOnStart);
    }

    [Fact]
    public void Installing_is_only_offered_once_a_newer_version_was_actually_found()
    {
        var viewModel = new MainViewModel(ConfigurationSamples.TwoLayouts(), []);

        Assert.True(viewModel.CanCheckForUpdates);
        Assert.False(viewModel.CanInstallUpdate);

        viewModel.IsUpdateAvailable = true;
        Assert.True(viewModel.CanInstallUpdate);

        // Waehrend eines laufenden Vorgangs ist beides gesperrt, sonst liefen zwei Downloads parallel.
        viewModel.IsUpdateBusy = true;
        Assert.False(viewModel.CanCheckForUpdates);
        Assert.False(viewModel.CanInstallUpdate);
    }

    [Fact]
    public void Both_actions_and_the_status_are_bound_on_the_settings_page()
    {
        WpfThemeHost.Invoke(() =>
        {
            var window = new MainWindow { Left = -10000 };
            window.AttachViewModel(new MainViewModel(ConfigurationSamples.TwoLayouts(), [])
            {
                ProductVersion = "2026.0901.01"
            });
            window.ShowSettingsPage();
            window.Show();
            window.UpdateLayout();

            var check = Assert.IsType<Button>(window.FindName("CheckForUpdatesButton"));
            var install = Assert.IsType<Button>(window.FindName("InstallUpdateButton"));
            var status = Assert.IsType<TextBlock>(window.FindName("UpdateStatusText"));
            var onStart = Assert.IsType<CheckBox>(window.FindName("CheckForUpdatesOnStartCheckBox"));
            var help = Assert.IsType<Button>(window.FindName("UpdateInfoButton"));

            Assert.Equal(
                "CanCheckForUpdates",
                check.GetBindingExpression(UIElement.IsEnabledProperty)!.ParentBinding.Path.Path);
            Assert.Equal(
                "CanInstallUpdate",
                install.GetBindingExpression(UIElement.IsEnabledProperty)!.ParentBinding.Path.Path);
            Assert.Equal(
                "Settings.CheckForUpdatesOnStart",
                onStart.GetBindingExpression(ToggleButton.IsCheckedProperty)!.ParentBinding.Path.Path);
            Assert.Equal(
                "UpdateSummary",
                status.GetBindingExpression(TextBlock.TextProperty)!.ParentBinding.Path.Path);
            Assert.StartsWith("Version 2026.0901.01 · Noch nicht nach Updates gesucht.", status.Text, StringComparison.Ordinal);
            Assert.EndsWith("Nur über HTTPS, geprüft per SHA-256.", status.Text, StringComparison.Ordinal);

            // Der Hilfetext muss benennen, dass und was gesendet wird.
            var tooltip = Assert.IsType<string>(help.ToolTip);
            Assert.True(tooltip.Length >= 120);
            Assert.Contains("sendet dabei nichts", tooltip, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(check)));
            window.Close();
        });

    }
}
