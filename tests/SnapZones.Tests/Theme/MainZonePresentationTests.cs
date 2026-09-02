using System.Windows.Automation;
using System.Windows.Controls;
using SnapZones.App.Controls;
using SnapZones.App.ViewModels;
using SnapZones.App.Views;
using SnapZones.Core.Geometry;
using SnapZones.Core.Models;
using SnapZones.Core.Monitors;
using Xunit;

namespace SnapZones.Tests.Theme;

/// <summary>
/// Die Hauptzone wird an der Zone selbst festgelegt. Geprüft wird, dass die Karte «Ausgewählte Zone» die
/// eine Schaltfläche in beide Richtungen führt, den Zustand im Klartext nennt und die Zeichenfläche die
/// Markierung mitbekommt.
/// </summary>
public sealed class MainZonePresentationTests
{
    [Fact]
    public void The_selected_zone_card_offers_one_button_for_both_directions()
    {
        WpfThemeHost.Invoke(() =>
        {
            var window = CreateWindow();
            var toggle = Assert.IsType<Button>(window.FindName("MainZoneToggleButton"));
            var state = Assert.IsType<TextBlock>(window.FindName("MainZoneStateText"));

            Assert.Equal("Als Hauptzone festlegen", toggle.Content);
            Assert.Equal("Keine Zone dieses Layouts ist Hauptzone.", state.Text);
            Assert.Contains("Hauptzone", AutomationProperties.GetName(toggle));

            toggle.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));

            Assert.Equal("Hauptzone aufheben", toggle.Content);
            Assert.Equal("Diese Zone ist die Hauptzone.", state.Text);

            toggle.RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));

            Assert.Equal("Als Hauptzone festlegen", toggle.Content);
            Assert.Equal("Keine Zone dieses Layouts ist Hauptzone.", state.Text);
        });
    }

    [Fact]
    public void Marking_a_zone_reaches_the_canvas_and_the_configuration()
    {
        WpfThemeHost.Invoke(() =>
        {
            var window = CreateWindow();
            var viewModel = Assert.IsType<MainViewModel>(window.DataContext);
            var editor = Assert.IsType<LayoutEditorViewModel>(viewModel.Editor);
            var zoneId = Assert.IsType<Guid>(editor.SelectedZone?.Id);

            Assert.IsType<Button>(window.FindName("MainZoneToggleButton"))
                .RaiseEvent(new System.Windows.RoutedEventArgs(Button.ClickEvent));

            Assert.Equal(zoneId, Assert.IsType<LayoutCanvas>(window.FindName("EditorCanvas")).MainZoneId);
            Assert.Equal(zoneId, viewModel.Configuration.Layouts.Single().MainZoneId);
        });
    }

    private static MainWindow CreateWindow()
    {
        var identity = new MonitorIdentity("MONITOR", "DISPLAY1", "Testmonitor");
        var monitor = new LiveMonitor(
            identity,
            new MonitorWorkArea(0, 0, 3200, 1080),
            96,
            96,
            true,
            70,
            30);
        var zone = new ZoneDefinition(Guid.NewGuid(), "Voll", NormalizedRect.Full);
        var configuration = new SnapConfiguration(
            SnapConfiguration.CurrentSchemaVersion,
            AppSettings.Default(Guid.Empty),
            [new MonitorLayout(identity, 3200, 1080, [zone])]);
        var window = new MainWindow();
        window.AttachViewModel(new MainViewModel(configuration, [monitor]));
        return window;
    }
}
