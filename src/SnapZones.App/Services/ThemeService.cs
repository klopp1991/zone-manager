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
                ["CanvasBrush"] = "#0F141D",
                ["SurfaceBrush"] = "#171E29",
                ["SurfaceRaisedBrush"] = "#202938",
                ["BorderBrush"] = "#344052",
                ["ControlBorderBrush"] = "#718198",
                ["InkBrush"] = "#F3F6FB",
                ["MutedBrush"] = "#A8B3C4",
                ["AccentBrush"] = "#78A4FF",
                ["AccentInkBrush"] = "#0A1424",
                ["AccentHoverBrush"] = "#8DB3FF",
                ["AccentPressedBrush"] = "#6795F0",
                ["AccentStatusBrush"] = "#78A4FF",
                ["AccentSoftBrush"] = "#20365F",
                ["HoverBrush"] = "#253247",
                ["PressedBrush"] = "#30425C",
                ["DisabledSurfaceBrush"] = "#252D39",
                ["DisabledInkBrush"] = "#8994A5",
                ["DisabledCheckBrush"] = "#A8B3C4",
                ["WarningSoftBrush"] = "#3B2C16",
                ["WarningBrush"] = "#FFD28A",
                ["DangerBrush"] = "#FF8D8D",
                ["MonitorFrameBrush"] = "#070A10",
                ["MonitorScreenBrush"] = "#121A26"
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
                ["DangerBrush"] = "#B52424",
                ["MonitorFrameBrush"] = "#172033",
                ["MonitorScreenBrush"] = "#F4F7FB"
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
