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
                ["InkBrush"] = "#F3F6FB",
                ["MutedBrush"] = "#A8B3C4",
                ["AccentBrush"] = "#78A4FF",
                ["AccentSoftBrush"] = "#20365F",
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
                ["BorderBrush"] = "#D6DEE9",
                ["InkBrush"] = "#172033",
                ["MutedBrush"] = "#657086",
                ["AccentBrush"] = "#2F6FED",
                ["AccentSoftBrush"] = "#E8F0FF",
                ["WarningSoftBrush"] = "#FFF2D8",
                ["WarningBrush"] = "#8A5600",
                ["DangerBrush"] = "#C63636",
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
