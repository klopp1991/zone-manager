using System.Text.Json;
using System.IO;
using ZoneManager.App.ViewModels;
using ZoneManager.Core.Models;
using ZoneManager.Windows.Displays;
using ZoneManager.Windows.Startup;

namespace ZoneManager.App.Services;

public static class DiagnosticRunner
{
    public static async Task<int> RunAsync(
        string configurationDirectory,
        IStartupService startupService,
        ElevationCapability elevation)
    {
        ArgumentNullException.ThrowIfNull(elevation);
        var settingsPath = Path.Combine(configurationDirectory, "settings.json");
        var configurationStatus = "missing";
        int? schemaVersion = null;
        if (File.Exists(settingsPath))
        {
            try
            {
                await using var stream = File.OpenRead(settingsPath);
                using var document = await JsonDocument.ParseAsync(stream);
                schemaVersion = document.RootElement.TryGetProperty("schemaVersion", out var version)
                    ? version.GetInt32()
                    : null;
                configurationStatus = "valid-json";
            }
            catch (JsonException)
            {
                configurationStatus = "invalid-json";
            }
        }

        var monitors = new WindowsMonitorService().GetMonitors();
        var startupViewModel = new MainViewModel(SnapConfiguration.CreateDefault(), monitors);
        var report = new
        {
            application = "Sascha’s Zone Manager",
            configurationStatus,
            schemaVersion,
            startupConfigurationReady = true,
            startupLayoutCount = startupViewModel.Configuration.Layouts.Count,
            monitors = monitors.Select(monitor => new
            {
                monitor.Identity.StableId,
                monitor.Identity.FriendlyName,
                monitor.WorkArea,
                monitor.DpiX,
                monitor.DpiY,
                monitor.IsPrimary
            }),
            winEventApiAvailable = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000),
            isElevated = elevation.IsElevated,
            canElevate = elevation.CanElevate,
            elevationReason = elevation.Reason.ToString(),
            hookRegistered = false,
            startupEnabled = startupService.IsEnabled,
            settingsChanged = false
        };
        Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        return monitors.Count > 0 ? 0 : 2;
    }
}
