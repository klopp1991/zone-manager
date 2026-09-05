using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;
using SnapZones.Core.Models;
using SnapZones.Windows.Theme;

namespace SnapZones.App.Services;

public sealed class ThemeService : IDisposable
{
    private readonly HashSet<Window> windows = [];
    private ThemeMode mode = ThemeMode.System;
    private bool isDark;

    public ThemeService()
    {
        SystemEvents.UserPreferenceChanged += SystemEvents_UserPreferenceChanged;
    }

    public void Track(Window window)
    {
        if (!windows.Add(window))
        {
            return;
        }

        window.SourceInitialized += Window_SourceInitialized;
        window.Closed += Window_Closed;
        ApplyFrame(window);
    }

    public void Apply(ThemeMode newMode)
    {
        mode = newMode;
        isDark = newMode == ThemeMode.Dark ||
            newMode == ThemeMode.System && WindowsThemeReader.IsSystemDark();
        ApplyPalette();
        foreach (var window in windows)
        {
            ApplyFrame(window);
        }
    }

    public void Dispose()
    {
        SystemEvents.UserPreferenceChanged -= SystemEvents_UserPreferenceChanged;
        foreach (var window in windows.ToArray())
        {
            window.SourceInitialized -= Window_SourceInitialized;
            window.Closed -= Window_Closed;
        }
        windows.Clear();
    }

    private void ApplyPalette()
    {
        var dictionary = System.Windows.Application.Current.Resources.MergedDictionaries
            .FirstOrDefault(resource => resource.Contains("CanvasBrush"));
        if (dictionary is null)
        {
            return;
        }

        var colours = isDark
            ? new Dictionary<string, string>
            {
                ["CanvasBrush"] = "#202020",
                ["SurfaceBrush"] = "#2B2B2B",
                ["SurfaceRaisedBrush"] = "#333333",
                ["BorderBrush"] = "#454545",
                ["ControlBorderBrush"] = "#7D7D7D",
                ["InkBrush"] = "#F3F3F3",
                ["MutedBrush"] = "#C6C6C6",
                ["SubtleInkBrush"] = "#A8A8A8",
                ["AccentBrush"] = "#A6A6A6",
                ["AccentInkBrush"] = "#202020",
                ["AccentHoverBrush"] = "#B8B8B8",
                ["AccentPressedBrush"] = "#929292",
                ["AccentStatusBrush"] = "#D0D0D0",
                ["AccentSoftBrush"] = "#3A3A3A",
                ["HoverBrush"] = "#3A3A3A",
                ["PressedBrush"] = "#414141",
                ["DisabledSurfaceBrush"] = "#383838",
                ["DisabledInkBrush"] = "#A6A6A6",
                ["DisabledCheckBrush"] = "#B7B7B7",
                ["WarningSoftBrush"] = "#3A3124",
                ["WarningBrush"] = "#FFD28A",
                ["WarningBorderBrush"] = "#A67A2E",
                ["DangerBrush"] = "#FF8D8D",
                ["SuccessBrush"] = "#8FD18F",
                ["DropTargetBrush"] = "#2F6FED",
                ["DropTargetInkBrush"] = "#8FB3F5",
                ["MonitorFrameBrush"] = "#111111",
                ["MonitorScreenBrush"] = "#262626",
                ["ZoneFillBrush"] = "#707070",
                ["ZoneBorderBrush"] = "#A0A0A0"
            }
            : new Dictionary<string, string>
            {
                ["CanvasBrush"] = "#F3F6FA",
                ["SurfaceBrush"] = "#FFFFFF",
                ["SurfaceRaisedBrush"] = "#F8FAFD",
                ["BorderBrush"] = "#CBD5E3",
                ["ControlBorderBrush"] = "#748196",
                ["InkBrush"] = "#172033",
                ["MutedBrush"] = "#58667C",
                ["SubtleInkBrush"] = "#66707F",
                ["AccentBrush"] = "#2F6FED",
                ["AccentInkBrush"] = "#FFFFFF",
                ["AccentHoverBrush"] = "#245ED0",
                ["AccentPressedBrush"] = "#194BAF",
                ["AccentStatusBrush"] = "#245AC5",
                ["AccentSoftBrush"] = "#E8F0FF",
                ["HoverBrush"] = "#EDF3FC",
                ["PressedBrush"] = "#DDE8F8",
                ["DisabledSurfaceBrush"] = "#E9EDF3",
                ["DisabledInkBrush"] = "#5F6B80",
                ["DisabledCheckBrush"] = "#5F6B80",
                ["WarningSoftBrush"] = "#FFF2D8",
                ["WarningBrush"] = "#754900",
                ["WarningBorderBrush"] = "#B5842A",
                ["DangerBrush"] = "#B52424",
                ["SuccessBrush"] = "#2E7D32",
                ["DropTargetBrush"] = "#2F6FED",
                ["DropTargetInkBrush"] = "#245AC5",
                ["MonitorFrameBrush"] = "#172033",
                ["MonitorScreenBrush"] = "#F4F7FB",
                ["ZoneFillBrush"] = "#8A8A8A",
                ["ZoneBorderBrush"] = "#686868"
            };

        foreach (var pair in colours)
        {
            dictionary[pair.Key] = new SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(pair.Value));
        }
    }

    private void ApplyFrame(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle != 0)
        {
            WindowThemeFrame.Apply(handle, isDark);
        }
    }

    private void SystemEvents_UserPreferenceChanged(object sender, UserPreferenceChangedEventArgs eventArgs)
    {
        _ = sender;
        _ = eventArgs;
        if (mode == ThemeMode.System && System.Windows.Application.Current is { } application)
        {
            _ = application.Dispatcher.BeginInvoke(() => Apply(mode));
        }
    }

    private void Window_SourceInitialized(object? sender, EventArgs eventArgs)
    {
        _ = eventArgs;
        if (sender is Window window)
        {
            ApplyFrame(window);
        }
    }

    private void Window_Closed(object? sender, EventArgs eventArgs)
    {
        _ = eventArgs;
        if (sender is Window window)
        {
            window.SourceInitialized -= Window_SourceInitialized;
            window.Closed -= Window_Closed;
            windows.Remove(window);
        }
    }
}
