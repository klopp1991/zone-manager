using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using SnapZones.App.ViewModels;
using SnapZones.App.Views;
using SnapZones.Core.Geometry;
using SnapZones.Core.Models;
using SnapZones.Core.Monitors;
using Xunit;

namespace SnapZones.Tests.Theme;

public sealed class LayoutMeasurementEditorTests
{
    [Fact]
    public void Layout_editor_uses_eight_direct_unit_fields_without_mode_dropdowns()
    {
        WpfThemeHost.Invoke(() =>
        {
            var window = CreateWindow();
            var fieldNames = new[]
            {
                "ZonePositionXText", "ZonePositionYText", "ZoneWidthText", "ZoneHeightText",
                "ZoneMarginLeftText", "ZoneMarginTopText", "ZoneMarginRightText", "ZoneMarginBottomText"
            };
            var unitButtonNames = new[]
            {
                "ZonePositionXUnitButton", "ZonePositionYUnitButton", "ZoneWidthUnitButton", "ZoneHeightUnitButton",
                "ZoneMarginLeftUnitButton", "ZoneMarginTopUnitButton", "ZoneMarginRightUnitButton", "ZoneMarginBottomUnitButton"
            };

            Assert.All(fieldNames, name => Assert.IsType<TextBox>(window.FindName(name)));
            Assert.All(unitButtonNames, name =>
            {
                var button = Assert.IsType<Button>(window.FindName(name));
                Assert.Equal("%", button.Content);
                Assert.Contains("Einheit", AutomationProperties.GetName(button));
            });
            Assert.Null(window.FindName("ZoneUnitCombo"));
            Assert.Null(window.FindName("ZoneDefinitionCombo"));
        });
    }

    [Fact]
    public void Layout_editor_applies_mixed_units_from_the_active_position_group()
    {
        WpfThemeHost.Invoke(() =>
        {
            var window = CreateWindow();
            var positionX = Assert.IsType<TextBox>(window.FindName("ZonePositionXText"));
            var positionXUnit = Assert.IsType<Button>(window.FindName("ZonePositionXUnitButton"));
            var positionY = Assert.IsType<TextBox>(window.FindName("ZonePositionYText"));
            var width = Assert.IsType<TextBox>(window.FindName("ZoneWidthText"));
            var height = Assert.IsType<TextBox>(window.FindName("ZoneHeightText"));
            var apply = Assert.IsType<Button>(window.FindName("ApplyZoneValuesButton"));

            positionXUnit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            positionX.Text = "320";
            positionY.Text = "10";
            width.Text = "50";
            height.Text = "50";
            apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            var editor = Assert.IsType<LayoutEditorViewModel>(Assert.IsType<MainViewModel>(window.DataContext).Editor);
            Assert.Equal("px", positionXUnit.Content);
            Assert.Equal(new NormalizedRect(0.1, 0.1, 0.5, 0.5), editor.SelectedZone?.Bounds);
            Assert.Equal("40", Assert.IsType<TextBox>(window.FindName("ZoneMarginRightText")).Text);
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
        var configuration = SnapConfiguration.CreateDefault();
        var profile = configuration.Profiles[0];
        configuration = configuration with
        {
            Profiles = [profile with { Monitors = [new MonitorLayout(identity, 3200, 1080, [zone])] }]
        };
        var window = new MainWindow();
        window.AttachViewModel(new MainViewModel(configuration, [monitor]));
        return window;
    }
}
