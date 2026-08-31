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

/// <summary>
/// Das Merken der Fensterpositionen lief bisher unsichtbar. Es hat jetzt einen Schalter, nennt die
/// Anzahl der Einträge und lässt sich verwerfen.
/// </summary>
public sealed class RememberedWindowPositionsTests
{
    [Fact]
    public void Remembering_is_switched_on_by_default_and_survives_a_round_trip()
    {
        // Die Funktion lief bisher immer; ein neuer Schalter darf sie nicht stillschweigend abschalten.
        Assert.True(AppSettings.Default(Guid.Empty).RememberWindowPositions);

        var settings = new SettingsViewModel(AppSettings.Default(Guid.Empty)) { RememberWindowPositions = false };

        Assert.False(settings.CreateSettings().RememberWindowPositions);

        settings.Apply(AppSettings.Default(Guid.Empty));

        Assert.True(settings.RememberWindowPositions);
    }

    [Fact]
    public void The_summary_names_the_number_of_remembered_windows()
    {
        var viewModel = new MainViewModel(ConfigurationSamples.TwoLayouts(), []);

        Assert.Equal("Es ist noch keine Fensterposition gemerkt.", viewModel.RememberedWindowSummary);
        Assert.False(viewModel.HasRememberedWindows);

        viewModel.RememberedWindowCount = 1;
        Assert.Equal("Eine Fensterposition ist gemerkt.", viewModel.RememberedWindowSummary);
        Assert.True(viewModel.HasRememberedWindows);

        viewModel.RememberedWindowCount = 7;
        Assert.Equal("7 Fensterpositionen sind gemerkt.", viewModel.RememberedWindowSummary);
    }

    [Fact]
    public void Discarding_is_only_a_request_so_the_catalog_stays_with_the_placement_module()
    {
        var viewModel = new MainViewModel(ConfigurationSamples.TwoLayouts(), []);
        var requests = 0;
        viewModel.ForgetWindowPositionsRequested += () => requests++;

        viewModel.ForgetWindowPositions();

        Assert.Equal(1, requests);
    }

    [Fact]
    public void The_settings_page_exposes_the_switch_the_count_and_the_discard_button()
    {
        WpfThemeHost.Invoke(() =>
        {
            var window = new MainWindow();
            window.AttachViewModel(new MainViewModel(ConfigurationSamples.TwoLayouts(), []));

            var toggle = Assert.IsType<CheckBox>(window.FindName("RememberWindowPositionsCheckBox"));
            var discard = Assert.IsType<Button>(window.FindName("ForgetWindowPositionsButton"));
            var help = Assert.IsType<Button>(window.FindName("RememberWindowPositionsInfoButton"));

            Assert.Equal(
                "Settings.RememberWindowPositions",
                toggle.GetBindingExpression(ToggleButton.IsCheckedProperty)!.ParentBinding.Path.Path);
            Assert.Equal(
                "HasRememberedWindows",
                discard.GetBindingExpression(UIElement.IsEnabledProperty)!.ParentBinding.Path.Path);
            Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(discard)));

            // Der Hilfetext muss die grobe Erkennung benennen, sonst wundert sich der Benutzer, warum
            // sich zwei Fenster desselben Programms dieselbe Position teilen.
            var tooltip = Assert.IsType<string>(help.ToolTip);
            Assert.True(tooltip.Length >= 120);
            Assert.Contains("nicht am Titel", tooltip, StringComparison.Ordinal);
        });
    }
}
