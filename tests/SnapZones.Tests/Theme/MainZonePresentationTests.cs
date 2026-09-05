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
/// Die Auffangzone wird an der Zone selbst festgelegt: eine Checkbox im Werte-Panel, die in beide
/// Richtungen führt, den Zustand im Klartext nennt und die Zeichenfläche mitnimmt.
/// </summary>
public sealed class MainZonePresentationTests
{
    [Fact]
    public void The_value_panel_offers_one_checkbox_for_both_directions()
    {
        WpfThemeHost.Invoke(() =>
        {
            var (_, panel) = CreateWindow();
            var toggle = Assert.IsType<CheckBox>(panel.FindName("MainZoneCheckBox"));
            var state = Assert.IsType<TextBlock>(panel.FindName("MainZoneStateText"));

            Assert.Equal("Auffangzone", toggle.Content);
            Assert.False(toggle.IsChecked);
            Assert.Equal("Keine Zone dieses Layouts ist Auffangzone.", state.Text);
            Assert.Contains("Auffangzone", AutomationProperties.GetName(toggle));

            toggle.IsChecked = true;
            toggle.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

            Assert.True(toggle.IsChecked);
            Assert.Equal("Diese Zone ist die Auffangzone dieses Layouts.", state.Text);

            toggle.RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

            Assert.False(toggle.IsChecked);
            Assert.Equal("Keine Zone dieses Layouts ist Auffangzone.", state.Text);
        });
    }

    [Fact]
    public void Marking_a_zone_reaches_the_canvas_and_the_configuration()
    {
        WpfThemeHost.Invoke(() =>
        {
            var (window, panel) = CreateWindow();
            var viewModel = Assert.IsType<MainViewModel>(window.DataContext);
            var editor = Assert.IsType<LayoutEditorViewModel>(viewModel.Editor);
            var zoneId = Assert.IsType<Guid>(editor.SelectedZone?.Id);

            Assert.IsType<CheckBox>(panel.FindName("MainZoneCheckBox"))
                .RaiseEvent(new System.Windows.RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));

            Assert.Equal(zoneId, Assert.IsType<LayoutCanvas>(window.FindName("EditorCanvas")).MainZoneId);
            Assert.Equal(zoneId, viewModel.Configuration.Layouts.Single().MainZoneId);
        });
    }

    [Fact]
    public void The_help_text_explains_the_catch_zone_with_an_example()
    {
        WpfThemeHost.Invoke(() =>
        {
            var (_, panel) = CreateWindow();
            var help = Assert.IsType<Button>(panel.FindName("MainZoneInfoButton"));
            var tooltip = Assert.IsType<string>(help.ToolTip);

            Assert.True(tooltip.Length >= 120);
            Assert.Contains("Beispiel", tooltip, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(help)));
        });
    }

    private static (MainWindow Window, ZoneValuesPanel Panel) CreateWindow()
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
        return (window, Assert.IsType<ZoneValuesPanel>(window.FindName("ZoneValues")));
    }
}
