using System.IO;
using System.Text.Json;
using SnapZones.Windows.Displays;
using SnapZones.Windows.Startup;

namespace SnapZones.App.Services;

public static class DiagnosticRunner
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public static async Task<int> RunAsync(string configurationDirectory, IStartupService startupService)
    {
        ArgumentNullException.ThrowIfNull(startupService);
        var report = await CreateReportAsync(configurationDirectory, startupService.IsEnabled, CancellationToken.None);
        Console.WriteLine(Serialize(report));
        return report.Monitors.Count > 0 ? 0 : 2;
    }

    public static Task<DiagnosticReport> RunForTestAsync(string configurationDirectory, CancellationToken cancellationToken) =>
        CreateReportAsync(configurationDirectory, startupEnabled: false, cancellationToken);

    public static string Serialize(DiagnosticReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return JsonSerializer.Serialize(report, JsonOptions);
    }

    private static async Task<DiagnosticReport> CreateReportAsync(
        string configurationDirectory,
        bool startupEnabled,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationDirectory);

        var settings = await ReadSettingsAsync(Path.Combine(configurationDirectory, "settings.json"), cancellationToken);
        var windowPlacement = await ReadWindowPlacementAsync(
            Path.Combine(configurationDirectory, "placements.json"),
            settings.Enabled,
            settings.RuleCount,
            cancellationToken);
        var monitors = new WindowsMonitorService().GetMonitors()
            .Select(monitor => new DiagnosticMonitor(
                monitor.Identity.StableId,
                monitor.Identity.FriendlyName,
                monitor.WorkArea,
                monitor.DpiX,
                monitor.DpiY,
                monitor.IsPrimary))
            .ToArray();

        return new DiagnosticReport(
            "Sascha Window Zones",
            settings.Status,
            settings.SchemaVersion,
            monitors,
            OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000),
            HookRegistered: false,
            startupEnabled,
            SettingsChanged: false,
            windowPlacement);
    }

    private static async Task<SettingsDiagnostic> ReadSettingsAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return new SettingsDiagnostic("missing", null, Enabled: true, RuleCount: 0);
        }

        try
        {
            using var document = await ReadDocumentAsync(path, cancellationToken);
            var root = document.RootElement;
            var schemaVersion = TryGetProperty(root, "schemaVersion", out var version) && version.TryGetInt32(out var parsedVersion)
                ? (int?)parsedVersion
                : null;
            var enabled = !TryGetProperty(root, "restoreWindowPlacementEnabled", out var restoreEnabled) ||
                          restoreEnabled.ValueKind is not JsonValueKind.False;
            var ruleCount = TryGetProperty(root, "windowPlacementRules", out var rules) && rules.ValueKind == JsonValueKind.Array
                ? rules.GetArrayLength()
                : 0;
            return new SettingsDiagnostic("valid-json", schemaVersion, enabled, ruleCount);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (JsonException)
        {
            return new SettingsDiagnostic("invalid-json", null, Enabled: true, RuleCount: 0);
        }
        catch (IOException)
        {
            return new SettingsDiagnostic("unreadable", null, Enabled: true, RuleCount: 0);
        }
        catch (UnauthorizedAccessException)
        {
            return new SettingsDiagnostic("unreadable", null, Enabled: true, RuleCount: 0);
        }
    }

    private static async Task<WindowPlacementDiagnostic> ReadWindowPlacementAsync(
        string path,
        bool enabled,
        int ruleCount,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return new WindowPlacementDiagnostic(enabled, 0, ruleCount, LifecycleHookRegistered: false, "missing");
        }

        try
        {
            using var document = await ReadDocumentAsync(path, cancellationToken);
            var root = document.RootElement;
            var learnedEntryCount = TryGetProperty(root, "entries", out var entries) && entries.ValueKind == JsonValueKind.Array
                ? entries.GetArrayLength()
                : 0;
            return new WindowPlacementDiagnostic(enabled, learnedEntryCount, ruleCount, LifecycleHookRegistered: false, "valid-json");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (JsonException)
        {
            return new WindowPlacementDiagnostic(enabled, 0, ruleCount, LifecycleHookRegistered: false, "invalid-json");
        }
        catch (IOException)
        {
            return new WindowPlacementDiagnostic(enabled, 0, ruleCount, LifecycleHookRegistered: false, "unreadable");
        }
        catch (UnauthorizedAccessException)
        {
            return new WindowPlacementDiagnostic(enabled, 0, ruleCount, LifecycleHookRegistered: false, "unreadable");
        }
    }

    private static async Task<JsonDocument> ReadDocumentAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private sealed record SettingsDiagnostic(string Status, int? SchemaVersion, bool Enabled, int RuleCount);
}

public sealed record DiagnosticReport(
    string Application,
    string ConfigurationStatus,
    int? SchemaVersion,
    IReadOnlyList<DiagnosticMonitor> Monitors,
    bool WinEventApiAvailable,
    bool HookRegistered,
    bool StartupEnabled,
    bool SettingsChanged,
    WindowPlacementDiagnostic WindowPlacement);

public sealed record DiagnosticMonitor(
    string StableId,
    string FriendlyName,
    object WorkArea,
    uint DpiX,
    uint DpiY,
    bool IsPrimary);

public sealed record WindowPlacementDiagnostic(
    bool Enabled,
    int LearnedEntryCount,
    int RuleCount,
    bool LifecycleHookRegistered,
    string Status);
