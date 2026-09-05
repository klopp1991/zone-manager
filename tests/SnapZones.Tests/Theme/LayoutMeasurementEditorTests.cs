using System.Windows;
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

public sealed class LayoutMeasurementEditorTests
{
    [Fact]
    public void Layout_editor_uses_eight_direct_fields_and_one_shared_unit_switch()
    {
        WpfThemeHost.Invoke(() =>
        {
            var (_, panel) = CreateWindow();
            var fieldNames = new[]
            {
                "ZonePositionXText", "ZonePositionYText", "ZoneWidthText", "ZoneHeightText",
                "ZoneMarginLeftText", "ZoneMarginTopText", "ZoneMarginRightText", "ZoneMarginBottomText"
            };

            Assert.All(fieldNames, name => Assert.IsType<TextBox>(panel.FindName(name)));

            // Die Einheit wird an einer einzigen Stelle umgeschaltet und gilt fuer alle acht Felder.
            var percent = Assert.IsType<Button>(panel.FindName("ZoneUnitPercentButton"));
            var pixels = Assert.IsType<Button>(panel.FindName("ZoneUnitPixelButton"));

            Assert.Equal("%", percent.Content);
            Assert.Equal("px", pixels.Content);
            Assert.Contains("Prozent", AutomationProperties.GetName(percent));
            Assert.Contains("Pixel", AutomationProperties.GetName(pixels));
            Assert.Same(panel.FindResource("UnitSegmentActive"), percent.Style);
            Assert.Same(panel.FindResource("UnitSegment"), pixels.Style);
        });
    }

    [Fact]
    public void Zone_measurement_fields_are_wide_enough_for_six_digit_pixel_values()
    {
        WpfThemeHost.Invoke(() =>
        {
            var (_, panel) = CreateWindow();
            var fieldNames = new[]
            {
                "ZonePositionXText", "ZonePositionYText", "ZoneWidthText", "ZoneHeightText",
                "ZoneMarginLeftText", "ZoneMarginTopText", "ZoneMarginRightText", "ZoneMarginBottomText"
            };

            Assert.All(fieldNames, name =>
            {
                var field = Assert.IsType<TextBox>(panel.FindName(name));
                Assert.True(field.MinWidth >= 110d, $"{name} ist zu schmal fuer sechsstellige Pixelwerte.");
            });
        });
    }

    [Fact]
    public void Layout_editor_populates_all_measurements_immediately_for_the_selected_zone()
    {
        WpfThemeHost.Invoke(() =>
        {
            var (_, panel) = CreateWindow();

            Assert.Equal("0", Assert.IsType<TextBox>(panel.FindName("ZonePositionXText")).Text);
            Assert.Equal("0", Assert.IsType<TextBox>(panel.FindName("ZonePositionYText")).Text);
            Assert.Equal("100", Assert.IsType<TextBox>(panel.FindName("ZoneWidthText")).Text);
            Assert.Equal("100", Assert.IsType<TextBox>(panel.FindName("ZoneHeightText")).Text);
            Assert.Equal("0", Assert.IsType<TextBox>(panel.FindName("ZoneMarginLeftText")).Text);
            Assert.Equal("0", Assert.IsType<TextBox>(panel.FindName("ZoneMarginTopText")).Text);
            Assert.Equal("0", Assert.IsType<TextBox>(panel.FindName("ZoneMarginRightText")).Text);
            Assert.Equal("0", Assert.IsType<TextBox>(panel.FindName("ZoneMarginBottomText")).Text);
            Assert.Equal("0 · 0 · 0 · 0", Assert.IsType<TextBlock>(panel.FindName("MarginsSummaryText")).Text);
        });
    }

    [Fact]
    public void Layout_editor_refreshes_all_measurements_when_the_editor_geometry_changes()
    {
        WpfThemeHost.Invoke(() =>
        {
            var (window, panel) = CreateWindow();
            var editor = Assert.IsType<LayoutEditorViewModel>(
                Assert.IsType<MainViewModel>(window.DataContext).Editor);
            var selectedZoneId = Assert.IsType<Guid>(editor.SelectedZone?.Id);

            editor.MoveOrResizeZone(selectedZoneId, new NormalizedRect(0.1, 0.2, 0.3, 0.4));

            Assert.Equal("10", Assert.IsType<TextBox>(panel.FindName("ZonePositionXText")).Text);
            Assert.Equal("20", Assert.IsType<TextBox>(panel.FindName("ZonePositionYText")).Text);
            Assert.Equal("30", Assert.IsType<TextBox>(panel.FindName("ZoneWidthText")).Text);
            Assert.Equal("40", Assert.IsType<TextBox>(panel.FindName("ZoneHeightText")).Text);
            Assert.Equal("10", Assert.IsType<TextBox>(panel.FindName("ZoneMarginLeftText")).Text);
            Assert.Equal("20", Assert.IsType<TextBox>(panel.FindName("ZoneMarginTopText")).Text);
            Assert.Equal("60", Assert.IsType<TextBox>(panel.FindName("ZoneMarginRightText")).Text);
            Assert.Equal("40", Assert.IsType<TextBox>(panel.FindName("ZoneMarginBottomText")).Text);
        });
    }

    [Fact]
    public void Layout_editor_switches_all_fields_to_pixels_and_applies_the_active_position_group()
    {
        WpfThemeHost.Invoke(() =>
        {
            var (window, panel) = CreateWindow();
            var positionX = Assert.IsType<TextBox>(panel.FindName("ZonePositionXText"));
            var pixelSwitch = Assert.IsType<Button>(panel.FindName("ZoneUnitPixelButton"));
            var percentSwitch = Assert.IsType<Button>(panel.FindName("ZoneUnitPercentButton"));
            var positionY = Assert.IsType<TextBox>(panel.FindName("ZonePositionYText"));
            var width = Assert.IsType<TextBox>(panel.FindName("ZoneWidthText"));
            var height = Assert.IsType<TextBox>(panel.FindName("ZoneHeightText"));

            pixelSwitch.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            positionX.Text = "320";
            positionY.Text = "10";
            width.Text = "50";
            height.Text = "50";
            var editor = Assert.IsType<LayoutEditorViewModel>(Assert.IsType<MainViewModel>(window.DataContext).Editor);
            Assert.Same(panel.FindResource("UnitSegmentActive"), pixelSwitch.Style);
            Assert.Same(panel.FindResource("UnitSegment"), percentSwitch.Style);
            Assert.Equal(new NormalizedRect(0.1, 10d / 1080d, 50d / 3200d, 50d / 1080d), editor.SelectedZone?.Bounds);
            Assert.Equal("2830", Assert.IsType<TextBox>(panel.FindName("ZoneMarginRightText")).Text);
        });
    }

    [Fact]
    public void Layout_editor_shows_a_changed_zone_name_immediately_without_changing_geometry()
    {
        WpfThemeHost.Invoke(() =>
        {
            var (window, panel) = CreateWindow();
            var name = Assert.IsType<TextBox>(panel.FindName("ZoneNameText"));
            var canvas = Assert.IsType<LayoutCanvas>(window.FindName("EditorCanvas"));
            var width = Assert.IsType<TextBox>(panel.FindName("ZoneWidthText"));
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

    [Fact]
    public void The_value_panel_can_be_hidden_and_the_choice_is_stored_as_a_setting()
    {
        WpfThemeHost.Invoke(() =>
        {
            var (window, _) = CreateWindow();
            var viewModel = Assert.IsType<MainViewModel>(window.DataContext);
            var host = Assert.IsType<Border>(window.FindName("ZoneValuesHost"));
            var toggle = Assert.IsType<Button>(window.FindName("ToggleValuePanelButton"));

            Assert.Equal(Visibility.Visible, host.Visibility);
            toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.Equal(Visibility.Collapsed, host.Visibility);
            Assert.False(viewModel.Configuration.Settings.EditorValuePanelOpen);
            Assert.Equal("‹ Werte einblenden", toggle.Content);
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
