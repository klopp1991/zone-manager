using System.Text.Json.Serialization;
using SnapZones.Core.AppRules;

namespace SnapZones.Core.Models;

public sealed record SnapConfiguration
{
    /// <summary>
    /// Schema 6 (02.09.2026): Monitore tragen eine Hardwarekennung aus der EDID, und je
    /// Monitorkombination wird die zuletzt aktive Layoutauswahl gemerkt.
    /// </summary>
    public const int CurrentSchemaVersion = 6;

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
    public IReadOnlyList<string> MonitorOrder { get; init; } = [];
    public IReadOnlyList<AppRule> AppRules { get; init; } = [];

    /// <summary>
    /// Fenster, die die Anwendung vollstaendig in Ruhe laesst. Siehe <see cref="AppExclusion"/>.
    /// </summary>
    public IReadOnlyList<AppExclusion> AppExclusions { get; init; } = [];

    /// <summary>Die zuletzt aktive Layoutauswahl je Monitorkombination. Siehe <see cref="MonitorSetSelection"/>.</summary>
    public IReadOnlyList<MonitorSetSelection> MonitorSets { get; init; } = [];

    [JsonPropertyName("Profiles")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<LayoutProfile>? LegacyProfiles { get; init; }

    public static SnapConfiguration CreateDefault()
        => new(CurrentSchemaVersion, AppSettings.Default(Guid.Empty), Array.Empty<MonitorLayout>());
}
