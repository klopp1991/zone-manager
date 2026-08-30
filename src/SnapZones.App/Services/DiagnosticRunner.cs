using System.Text.Json;
using System.IO;
using SnapZones.Windows.Displays;
using SnapZones.Windows.Startup;

namespace SnapZones.App.Services;

public static class DiagnosticRunner
{
    public static async Task<int> RunAsync(string configurationDirectory, IStartupService startupService)
    {
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
        var report = new
        {
            application = "Sascha Window Zones",
            configurationStatus,
            schemaVersion,
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
            hookRegistered = false,
            startupEnabled = startupService.IsEnabled,
            settingsChanged = false
        };
        Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        return monitors.Count > 0 ? 0 : 2;
    }
}
