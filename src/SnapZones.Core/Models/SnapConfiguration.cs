using System.Text.Json.Serialization;
using SnapZones.Core.AppRules;

namespace SnapZones.Core.Models;

public sealed record SnapConfiguration
{
    /// <summary>
    /// Schema 8 (08.09.2026): Name, Reihenfolge und Monitorkombination haengen an der Hardwarekennung
    /// des Monitors statt an seinem Anzeigepfad. Der Anzeigepfad enthaelt Grafikkarte und Anschluss;
    /// nach einem Umstecken galt derselbe Monitor als neuer, verlor seinen Namen und trat unter der von
    /// Windows neu vergebenen Anzeigenummer wieder auf.
    ///
    /// <para>
    /// Schema 7 (03.09.2026): die Zonenkuerzel liegen auf Strg + Umschalt statt auf Strg + Alt, weil
    /// Windows AltGr intern als Strg + Alt liefert und ein solches Kuerzel jedes AltGr-Zeichen auf
    /// derselben Taste schluckt. Der Uebergang stellt einen bestehenden Stand einmalig um; wer danach
    /// bewusst Strg + Alt waehlt, behaelt es.
    /// </para>
    ///
    /// <para>
    /// Schema 6 (02.09.2026): Monitore tragen eine Hardwarekennung aus der EDID, und je
    /// Monitorkombination wird die zuletzt aktive Layoutauswahl gemerkt.
    /// </para>
    /// </summary>
    public const int CurrentSchemaVersion = 8;

    /// <summary>Ab diesem Schema ist die Wahl der Zusatztasten die des Benutzers und wird nicht mehr angetastet.</summary>
    public const int ZoneHotkeyModifierMigrationSchemaVersion = 7;

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

    /// <summary>
    /// Programme, die in einer Vollbildzone trotzdem den ganzen Monitor bekommen. Dateinamen ohne Pfad,
    /// etwa «vlc.exe». Steht hier statt in <see cref="AppSettings"/>, weil es wie die Ausschlussliste
    /// eine Aufzaehlung von Programmen ist.
    /// </summary>
    public IReadOnlyList<string> FullscreenZoneExcludedPrograms { get; init; } = [];

    /// <summary>Die zuletzt aktive Layoutauswahl je Monitorkombination. Siehe <see cref="MonitorSetSelection"/>.</summary>
    public IReadOnlyList<MonitorSetSelection> MonitorSets { get; init; } = [];

    [JsonPropertyName("Profiles")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<LayoutProfile>? LegacyProfiles { get; init; }

    public static SnapConfiguration CreateDefault()
        => new(CurrentSchemaVersion, AppSettings.Default(Guid.Empty), Array.Empty<MonitorLayout>());
}
