using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using SnapZones.App.Controls;
using SnapZones.Presentation.ViewModels;
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
    public void Layout_editor_populates_all_measurements_immediately_for_the_selected_zone()
    {
        WpfThemeHost.Invoke(() =>
        {
            var window = CreateWindow();

            Assert.Equal("0", Assert.IsType<TextBox>(window.FindName("ZonePositionXText")).Text);
            Assert.Equal("0", Assert.IsType<TextBox>(window.FindName("ZonePositionYText")).Text);
            Assert.Equal("100", Assert.IsType<TextBox>(window.FindName("ZoneWidthText")).Text);
            Assert.Equal("100", Assert.IsType<TextBox>(window.FindName("ZoneHeightText")).Text);
            Assert.Equal("0", Assert.IsType<TextBox>(window.FindName("ZoneMarginLeftText")).Text);
            Assert.Equal("0", Assert.IsType<TextBox>(window.FindName("ZoneMarginTopText")).Text);
            Assert.Equal("0", Assert.IsType<TextBox>(window.FindName("ZoneMarginRightText")).Text);
            Assert.Equal("0", Assert.IsType<TextBox>(window.FindName("ZoneMarginBottomText")).Text);
        });
    }

    [Fact]
    public void Layout_editor_refreshes_all_measurements_when_the_editor_geometry_changes()
    {
        WpfThemeHost.Invoke(() =>
        {
            var window = CreateWindow();
            var editor = Assert.IsType<LayoutEditorViewModel>(
                Assert.IsType<MainViewModel>(window.DataContext).Editor);
            var selectedZoneId = Assert.IsType<Guid>(editor.SelectedZone?.Id);

            editor.MoveOrResizeZone(selectedZoneId, new NormalizedRect(0.1, 0.2, 0.3, 0.4));

            Assert.Equal("10", Assert.IsType<TextBox>(window.FindName("ZonePositionXText")).Text);
            Assert.Equal("20", Assert.IsType<TextBox>(window.FindName("ZonePositionYText")).Text);
            Assert.Equal("30", Assert.IsType<TextBox>(window.FindName("ZoneWidthText")).Text);
            Assert.Equal("40", Assert.IsType<TextBox>(window.FindName("ZoneHeightText")).Text);
            Assert.Equal("10", Assert.IsType<TextBox>(window.FindName("ZoneMarginLeftText")).Text);
            Assert.Equal("20", Assert.IsType<TextBox>(window.FindName("ZoneMarginTopText")).Text);
            Assert.Equal("60", Assert.IsType<TextBox>(window.FindName("ZoneMarginRightText")).Text);
            Assert.Equal("40", Assert.IsType<TextBox>(window.FindName("ZoneMarginBottomText")).Text);
        });
    }

    [Fact]
    public void Layout_editor_switches_all_fields_to_pixels_and_applies_the_active_position_group()
    {
        WpfThemeHost.Invoke(() =>
        {
            var window = CreateWindow();
            var positionX = Assert.IsType<TextBox>(window.FindName("ZonePositionXText"));
            var positionXUnit = Assert.IsType<Button>(window.FindName("ZonePositionXUnitButton"));
            var positionY = Assert.IsType<TextBox>(window.FindName("ZonePositionYText"));
            var width = Assert.IsType<TextBox>(window.FindName("ZoneWidthText"));
            var height = Assert.IsType<TextBox>(window.FindName("ZoneHeightText"));

            positionXUnit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            positionX.Text = "320";
            positionY.Text = "10";
            width.Text = "50";
            height.Text = "50";
            var editor = Assert.IsType<LayoutEditorViewModel>(Assert.IsType<MainViewModel>(window.DataContext).Editor);
            Assert.Equal("px", positionXUnit.Content);
            Assert.Equal(new NormalizedRect(0.1, 10d / 1080d, 50d / 3200d, 50d / 1080d), editor.SelectedZone?.Bounds);
            Assert.Equal("2830", Assert.IsType<TextBox>(window.FindName("ZoneMarginRightText")).Text);
        });
    }

    [Fact]
    public void Layout_editor_shows_a_changed_zone_name_immediately_without_changing_geometry()
    {
        WpfThemeHost.Invoke(() =>
        {
            var window = CreateWindow();
            var name = Assert.IsType<TextBox>(window.FindName("ZoneNameText"));
            var canvas = Assert.IsType<LayoutCanvas>(window.FindName("EditorCanvas"));
            var width = Assert.IsType<TextBox>(window.FindName("ZoneWidthText"));
            var editor = Assert.IsType<LayoutEditorViewModel>(
                Assert.IsType<MainViewModel>(window.DataContext).Editor);
            var originalBounds = editor.SelectedZone?.Bounds;
            var originalWidthText = width.Text;

            name.Text = "Arbeitsbereich";

            Assert.Equal("Arbeitsbereich", editor.SelectedZone?.Name);
            Assert.Equal("Arbeitsbereich", Assert.Single(canvas.Zones).Name);
            Assert.Equal(originalBounds, editor.SelectedZone?.Bounds);
            Assert.Equal(originalWidthText, width.Text);
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
