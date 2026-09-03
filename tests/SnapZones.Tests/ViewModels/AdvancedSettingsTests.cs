using System.Windows.Controls;
using SnapZones.App.ViewModels;
using SnapZones.App.Views;
using SnapZones.Core.Geometry;
using SnapZones.Core.Models;
using SnapZones.Core.Persistence;
using SnapZones.Core.Placement;
using SnapZones.Tests.Support;
using SnapZones.Tests.Theme;
using SnapZones.Windows.Hotkeys;
using Xunit;

namespace SnapZones.Tests.ViewModels;

/// <summary>
/// Die Feinabstimmung fuer erfahrene Anwender (02.09.2026): jeder Wert hat einen sicheren Standard, wird
/// beim Laden geprueft, im Modell begrenzt, in der Oberflaeche erst auf Wunsch gezeigt und laesst sich
/// gesamthaft zuruecksetzen.
/// </summary>
public sealed class AdvancedSettingsTests
{
    [Fact]
    public void Defaults_match_the_previous_fixed_behaviour()
    {
        var settings = AppSettings.Default(Guid.Empty);

        Assert.False(settings.ShowAdvancedSettings);
        Assert.Equal(0, settings.OverlayShowDelayMilliseconds);
        Assert.Equal(2, settings.PlacementTolerancePixels);
        Assert.Equal(40, settings.SnappedTolerancePixels);
        Assert.Equal(500, settings.RememberedWindowLimit);
        Assert.Equal(250, settings.RuleRetryDelayMilliseconds);
        Assert.Equal(400, settings.MoveHookEventLimit);
        Assert.Equal(120, settings.DragWatchdogSeconds);
        Assert.Equal(ZoneHotkeyModifiers.ControlShift, settings.ZoneHotkeyModifiers);
        Assert.Equal(OverlayStyle.Default with { HighlightColor = "#707070" }, OverlayStyle.From(settings));
        Assert.True(settings.CatchNewWindowsInMainZone);
        Assert.True(settings.PreferRememberedZone);
    }

    [Fact]
    public void The_view_model_clamps_every_fine_tuning_value_to_its_range()
    {
        var viewModel = new SettingsViewModel(AppSettings.Default(Guid.Empty));

        viewModel.OverlayShowDelayMilliseconds = 5000;
        viewModel.PlacementTolerancePixels = -3;
        viewModel.SnappedTolerancePixels = 1;
        viewModel.RememberedWindowLimit = 7;
        viewModel.MoveHookEventLimit = 99999;
        viewModel.DragWatchdogSeconds = 0;
        viewModel.OverlayBorderThickness = 40;
        viewModel.HighlightOpacityPercent = 3;

        var settings = viewModel.CreateSettings();
        Assert.Equal(1000, settings.OverlayShowDelayMilliseconds);
        Assert.Equal(0, settings.PlacementTolerancePixels);
        Assert.Equal(8, settings.SnappedTolerancePixels);
        Assert.Equal(50, settings.RememberedWindowLimit);
        Assert.Equal(5000, settings.MoveHookEventLimit);
        Assert.Equal(5, settings.DragWatchdogSeconds);
        Assert.Equal(6, settings.OverlayBorderThickness);
        Assert.Equal(0.10, settings.HighlightOpacity, 3);
    }

    [Fact]
    public async Task Fine_tuning_survives_a_round_trip_and_out_of_range_files_are_rejected()
    {
        using var directory = new TemporaryDirectory();
        var repository = new JsonConfigurationRepository(directory.Path);
        var configuration = ConfigurationSamples.TwoLayouts();
        configuration = configuration with
        {
            Settings = configuration.Settings with
            {
                ShowAdvancedSettings = true,
                SnappedTolerancePixels = 24,
                ZoneHotkeyModifiers = ZoneHotkeyModifiers.AltShift,
                HighlightColor = "#2F6FED",
                OverlayLabelStyle = OverlayLabelStyle.NumberOnly
            }
        };

        await repository.SaveAsync(configuration, CancellationToken.None);
        var loaded = await repository.LoadAsync(CancellationToken.None);

        Assert.True(loaded.Configuration.Settings.ShowAdvancedSettings);
        Assert.Equal(24, loaded.Configuration.Settings.SnappedTolerancePixels);
        Assert.Equal(ZoneHotkeyModifiers.AltShift, loaded.Configuration.Settings.ZoneHotkeyModifiers);
        Assert.Equal("#2F6FED", loaded.Configuration.Settings.HighlightColor);

        var broken = configuration with { Settings = configuration.Settings with { DragWatchdogSeconds = 1 } };
        await Assert.ThrowsAsync<InvalidDataException>(() => repository.SaveAsync(broken, CancellationToken.None));
        var badColour = configuration with { Settings = configuration.Settings with { HighlightColor = "blau" } };
        await Assert.ThrowsAsync<InvalidDataException>(() => repository.SaveAsync(badColour, CancellationToken.None));
    }

    [Fact]
    public void Reset_restores_the_defaults_but_keeps_appearance_autostart_and_rights()
    {
        var viewModel = new SettingsViewModel(AppSettings.Default(Guid.Empty) with
        {
            ThemeMode = ThemeMode.Dark,
            StartWithWindows = true,
            ElevationMode = ElevationMode.Always,
            ZoneGap = 12,
            OverlayCornerRadius = 20,
            ShowAdvancedSettings = true
        });

        viewModel.ResetToDefaults();

        var settings = viewModel.CreateSettings();
        Assert.Equal(ThemeMode.Dark, settings.ThemeMode);
        Assert.True(settings.StartWithWindows);
        Assert.Equal(ElevationMode.Always, settings.ElevationMode);
        Assert.True(settings.ShowAdvancedSettings);
        Assert.Equal(0, settings.ZoneGap);
        Assert.Equal(4, settings.OverlayCornerRadius);
    }

    [Fact]
    public void Hotkey_modifiers_map_to_win32_flags_and_labels()
    {
        Assert.Equal(0x0002u | 0x0001u, GlobalHotkeyService.ModifierFlags(ZoneHotkeyModifiers.ControlAlt));
        Assert.Equal(0x0002u | 0x0004u, GlobalHotkeyService.ModifierFlags(ZoneHotkeyModifiers.ControlShift));
        Assert.Equal(0x0001u | 0x0004u, GlobalHotkeyService.ModifierFlags(ZoneHotkeyModifiers.AltShift));
        Assert.Equal(0x0002u | 0x0008u, GlobalHotkeyService.ModifierFlags(ZoneHotkeyModifiers.ControlWin));
        Assert.Equal("Alt + Shift", SettingsViewModel.DescribeModifiers(ZoneHotkeyModifiers.AltShift));
    }

    [Fact]
    public void Overlay_style_labels_follow_the_chosen_style()
    {
        var style = OverlayStyle.Default;
        Assert.Equal("2 · Rechts", style.Label(2, "Rechts"));
        Assert.Equal("2", (style with { LabelStyle = OverlayLabelStyle.NumberOnly }).Label(2, "Rechts"));
        Assert.Equal("Rechts", (style with { LabelStyle = OverlayLabelStyle.NameOnly }).Label(2, "Rechts"));
    }

    [Fact]
    public void Main_zone_catch_can_be_switched_off_and_uses_the_configured_tolerance()
    {
        var configuration = ConfigurationSamples.TwoLayouts();
        var layout = configuration.Layouts[0];
        configuration = configuration with
        {
            Layouts = [layout with { MainZoneId = layout.Zones[1].Id }, configuration.Layouts[1]]
        };
        var zones = layout.Zones
            .Select(zone => new PlacementZoneTarget(layout.Id, zone.Id, layout.Monitor.StableId,
                ZoneGeometry.ToPixels(zone.Bounds, new MonitorWorkArea(0, 0, 2000, 1000))))
            .ToArray();
        var stray = new PixelRect(300, 300, 400, 300);

        Assert.NotNull(MainZoneFallback.Resolve(configuration, zones, stray));

        var switchedOff = configuration with { Settings = configuration.Settings with { CatchNewWindowsInMainZone = false } };
        Assert.Null(MainZoneFallback.Resolve(switchedOff, zones, stray));

        // 30 px neben der Zone: mit 40 px Toleranz eingerastet, mit 8 px nicht.
        var almost = new PixelRect(30, 0, 1000, 1000);
        Assert.Null(MainZoneFallback.Resolve(configuration, zones, almost));
        var strict = configuration with { Settings = configuration.Settings with { SnappedTolerancePixels = 8 } };
        Assert.NotNull(MainZoneFallback.Resolve(strict, zones, almost));
    }

    [Fact]
    public void Remembered_pixels_win_when_the_zone_preference_is_switched_off()
    {
        var identity = new WindowIdentity("C:\\Apps\\excel.exe", "XLMAIN", WindowKind.MainWindow);
        var zoneId = Guid.NewGuid();
        var zone = new PlacementZoneTarget(Guid.NewGuid(), zoneId, "DISPLAY-A", new PixelRect(960, 0, 960, 1080));
        var entry = new WindowPlacementEntry(
            identity, "DISPLAY-A", zoneId, new MonitorWorkArea(0, 0, 1920, 1080),
            new PixelRect(10, 10, 500, 500),
            PlacementGeometry.Normalize(new PixelRect(10, 10, 500, 500), new MonitorWorkArea(0, 0, 1920, 1080)),
            false, DateTimeOffset.UtcNow);
        var monitors = new[] { new PlacementMonitorTarget("DISPLAY-A", new MonitorWorkArea(0, 0, 1920, 1080), true) };

        Assert.Equal(new PixelRect(10, 10, 500, 500), PlacementGeometry.Resolve(entry, monitors, [zone], preferZone: false));
    }

    [Fact]
    public void Advanced_cards_are_hidden_until_the_expert_switch_is_on()
    {
        WpfThemeHost.Invoke(() =>
        {
            var window = new MainWindow();
            var viewModel = new MainViewModel(ConfigurationSamples.TwoLayouts(), []);
            window.AttachViewModel(viewModel);
            var tabs = Assert.IsType<TabControl>(Assert.IsType<Grid>(window.Content).Children[1]);
            tabs.SelectedItem = tabs.Items.OfType<TabItem>().Single(item => Equals(item.Header, "Verhalten"));
            window.Show();
            var tuning = Assert.IsType<Border>(window.FindName("PlacementTuningCard"));
            var style = Assert.IsType<Border>(window.FindName("OverlayStyleCard"));
            var toggle = Assert.IsType<CheckBox>(window.FindName("ShowAdvancedSettingsCheckBox"));

            Assert.Equal(System.Windows.Visibility.Collapsed, tuning.Visibility);
            Assert.Equal(System.Windows.Visibility.Collapsed, style.Visibility);

            toggle.IsChecked = true;
            window.UpdateLayout();

            Assert.Equal(System.Windows.Visibility.Visible, tuning.Visibility);
            Assert.True(viewModel.Configuration.Settings.ShowAdvancedSettings);
            window.Close();
        });
    }
}
