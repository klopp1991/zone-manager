using System.Text.Json.Serialization;

namespace SnapZones.Core.Models;

public sealed record SnapConfiguration
{
    public const int CurrentSchemaVersion = 2;

    public SnapConfiguration()
    {
    }

    public SnapConfiguration(
        int schemaVersion,
        AppSettings settings,
        IReadOnlyList<MonitorLayout> layouts)
    {
        SchemaVersion = schemaVersion;
        Settings = settings;
        Layouts = layouts;
    }

    public int SchemaVersion { get; init; }
    public AppSettings Settings { get; init; } = AppSettings.Default(Guid.Empty);
    public IReadOnlyList<MonitorLayout> Layouts { get; init; } = [];
    public IReadOnlyDictionary<string, string> MonitorNames { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("Profiles")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<LayoutProfile>? LegacyProfiles { get; init; }

    public static SnapConfiguration CreateDefault()
        => new(CurrentSchemaVersion, AppSettings.Default(Guid.Empty), Array.Empty<MonitorLayout>());
}
