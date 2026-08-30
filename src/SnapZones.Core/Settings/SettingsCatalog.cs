namespace SnapZones.Core.Settings;

/// <summary>
/// The single source of truth for every user-visible setting: its caption, its
/// help texts, its accepted range and its factory value.
/// <para>
/// The user interface reads ranges and defaults from here instead of repeating
/// them as literals, so a slider, a text box and the value clamping can never
/// disagree about what is allowed.
/// </para>
/// </summary>
public static class SettingsCatalog
{
    public static NumericSettingRange OuterMarginRange { get; } =
        new(Minimum: 0, Maximum: 400, Default: 8, Step: 1, Unit: "px");

    public static NumericSettingRange ZoneGapRange { get; } =
        new(Minimum: 0, Maximum: 80, Default: 8, Step: 1, Unit: "px");

    public static NumericSettingRange MagnetThresholdRange { get; } =
        new(Minimum: 0, Maximum: 40, Default: 10, Step: 1, Unit: "px");

    public static NumericSettingRange OverlayOpacityRange { get; } =
        new(Minimum: 8, Maximum: 75, Default: 24, Step: 1, Unit: "%");

    /// <summary>Default overlay colour, a neutral Windows grey.</summary>
    public const string DefaultOverlayColor = "#707070";

    /// <summary>
    /// Minimum visual gap the overlay always keeps, even when the configured
    /// spacing is zero, so that adjacent zones stay distinguishable while
    /// dragging. Windows are still placed exactly on the zone bounds.
    /// </summary>
    public const int OverlayMinimumVisualGap = 8;

    private static readonly IReadOnlyList<SettingDescriptor> Descriptors =
    [
        new SettingDescriptor(
            SettingKey.ThemeMode,
            SettingCategory.Program,
            Label: "Erscheinungsbild",
            ShortHelp: "Bestimmt, ob das Programmfenster hell oder dunkel dargestellt wird.",
            LongHelp:
                "«Wie Windows» übernimmt automatisch den App-Modus aus den Windows-Einstellungen und wechselt mit, "
                + "sobald du ihn dort änderst. «Hell» und «Dunkel» legen die Darstellung fest und gelten nur für "
                + "Sascha’s Zone Manager, nicht für andere Programme. Die Auswahl wirkt sofort und betrifft "
                + "ausschliesslich das Aussehen; Zonen, Layouts und das Verhalten beim Ziehen bleiben unverändert.",
            Keywords: ["Theme", "Dark Mode", "Hell", "Dunkel", "Farbschema", "Aussehen"]),

        new SettingDescriptor(
            SettingKey.StartWithWindows,
            SettingCategory.Program,
            Label: "Mit Windows starten",
            ShortHelp: "Startet das Programm automatisch nach der Anmeldung.",
            LongHelp:
                "Legt einen Autostarteintrag ausschliesslich für dein Benutzerkonto an. Es werden weder "
                + "Administratorrechte noch ein Windows-Dienst benötigt, und andere Benutzer des Computers sind "
                + "nicht betroffen. Beim Ausschalten wird der Eintrag wieder entfernt. Da das Programm für das "
                + "Platzieren fremder Fenster erhöhte Rechte benötigt, kann Windows beim automatischen Start "
                + "weiterhin eine Bestätigung anzeigen.",
            Keywords: ["Autostart", "Anmeldung", "Boot", "automatisch starten"]),

        new SettingDescriptor(
            SettingKey.OverlayScope,
            SettingCategory.Activation,
            Label: "Overlay anzeigen auf",
            ShortHelp: "Legt fest, auf welchen Bildschirmen die Zonen beim Ziehen erscheinen.",
            LongHelp:
                "«Alle Monitore» blendet die Zonen gleichzeitig auf jedem Bildschirm ein. Du siehst damit sofort "
                + "alle Ziele und kannst ein Fenster in einem Zug auf einen anderen Monitor ziehen. "
                + "«Aktiver Monitor» zeigt nur die Zonen des Bildschirms, auf dem sich der Mauszeiger gerade "
                + "befindet. Das wirkt ruhiger und ist auf Systemen mit vielen Monitoren sparsamer, weil weniger "
                + "Overlayfenster gezeichnet werden. Auf das Platzieren selbst hat die Einstellung keinen Einfluss.",
            Keywords: ["Monitor", "Bildschirm", "Multi-Monitor", "Anzeige"]),

        new SettingDescriptor(
            SettingKey.TriggerMode,
            SettingCategory.Activation,
            Label: "Zonen einblenden",
            ShortHelp: "Legt fest, wann das Overlay während des Ziehens erscheint.",
            LongHelp:
                "«Sofort beim Ziehen» blendet die Zonen ein, sobald du ein geeignetes Fenster an der Titelleiste "
                + "bewegst. «Nur mit Umschalttaste» blendet sie erst ein, wenn du zusätzlich Shift gedrückt hältst. "
                + "Wähle die zweite Variante, wenn du Fenster oft frei verschiebst und das Overlay dabei stört: "
                + "ohne Shift verhält sich das Ziehen dann genau wie ohne das Programm. Ein bereits begonnener "
                + "Ziehvorgang lässt sich jederzeit mit Escape abbrechen.",
            Keywords: ["Shift", "Umschalttaste", "Auslöser", "Trigger", "sofort"]),

        new SettingDescriptor(
            SettingKey.ShowZoneNames,
            SettingCategory.Activation,
            Label: "Zonennamen anzeigen",
            ShortHelp: "Blendet den Namen jeder Zone im Overlay ein.",
            LongHelp:
                "Zeigt beim Ziehen den Namen jeder Zone auf einer kleinen Beschriftungsfläche an. Das hilft bei "
                + "Layouts mit vielen ähnlich grossen Zonen. Ohne Namen bleibt das Overlay ruhiger; Kontur und "
                + "hervorgehobene Zielfläche sind weiterhin sichtbar, sodass du das Ziel auch dann eindeutig "
                + "erkennst.",
            Keywords: ["Beschriftung", "Label", "Namen", "Text"]),

        new SettingDescriptor(
            SettingKey.OverlayColor,
            SettingCategory.OverlayAppearance,
            Label: "Overlayfarbe",
            ShortHelp: "Grundfarbe der Zonenflächen beim Ziehen, als Hexwert #RRGGBB.",
            LongHelp:
                "Bestimmt die Farbe, mit der Zonen während des Ziehens gefüllt und umrandet werden. Das neutrale "
                + $"Grau {DefaultOverlayColor} wirkt auf hellen wie dunklen Hintergründen zurückhaltend. Eine "
                + "kräftige Farbe hebt das Ziel deutlicher hervor, überdeckt aber auch mehr vom Bildschirminhalt. "
                + "Die Farbe betrifft ausschliesslich die Vorschau; die platzierten Fenster verändern ihr Aussehen "
                + "nicht.",
            Keywords: ["Farbe", "Hex", "RGB", "Colour", "Akzent"]),

        new SettingDescriptor(
            SettingKey.OverlayOpacity,
            SettingCategory.OverlayAppearance,
            Label: "Deckkraft",
            ShortHelp: "Wie stark die Zonenflächen den Bildschirminhalt überdecken.",
            LongHelp:
                "Ein niedriger Wert hält das Overlay dezent, sodass du den Inhalt darunter noch erkennst. Ein "
                + "hoher Wert macht die Zonen deutlicher, verdeckt aber mehr. Die Zone unter dem Mauszeiger wird "
                + "unabhängig davon immer etwas stärker hervorgehoben, damit das aktuelle Ziel erkennbar bleibt. "
                + "Werte unterhalb des Minimums sind nicht möglich, weil das Overlay sonst praktisch unsichtbar "
                + "wäre.",
            Range: OverlayOpacityRange,
            Keywords: ["Transparenz", "Sichtbarkeit", "Alpha", "durchsichtig"]),

        new SettingDescriptor(
            SettingKey.OuterMargins,
            SettingCategory.Spacing,
            Label: "Aussenabstand des Overlays",
            ShortHelp: "Abstand der Zonenflächen zum Bildschirmrand, je Seite einstellbar.",
            LongHelp:
                "Rückt die gezeichneten Zonenflächen an der jeweiligen Seite vom Bildschirmrand ab. Die Werte "
                + "wirken ausschliesslich auf die Vorschau: Fenster werden weiterhin exakt nach der "
                + "Layoutdefinition platziert und können den Bildschirmrand berühren. Unterschiedliche Werte pro "
                + "Seite sind sinnvoll, wenn eine Seite von einer Taskleiste oder einem Randbereich verdeckt wird. "
                + $"Auch bei 0 bleibt ein visueller Mindestabstand von {OverlayMinimumVisualGap} px erhalten, "
                + "damit die Flächen nicht mit dem Bildschirmrand verschmelzen.",
            Range: OuterMarginRange,
            Keywords: ["Rand", "Margin", "Abstand", "aussen", "Bildschirmrand"]),

        new SettingDescriptor(
            SettingKey.ZoneGap,
            SettingCategory.Spacing,
            Label: "Abstand zwischen Zonen",
            ShortHelp: "Lücke zwischen benachbarten Zonenflächen im Overlay.",
            LongHelp:
                "Bestimmt, wie weit benachbarte Zonenflächen in der Vorschau voneinander abgerückt werden. Ein "
                + "grösserer Wert trennt dicht beieinanderliegende Zonen optisch deutlicher. Wie beim "
                + "Aussenabstand betrifft dies nur die Darstellung: platzierte Fenster liegen weiterhin exakt an "
                + $"den Zonengrenzen und damit direkt aneinander. Bei 0 bleiben {OverlayMinimumVisualGap} px "
                + "visueller Mindestabstand bestehen.",
            Range: ZoneGapRange,
            Keywords: ["Lücke", "Gap", "Zwischenraum", "Abstand"]),

        new SettingDescriptor(
            SettingKey.MagnetThreshold,
            SettingCategory.Spacing,
            Label: "Magnetdistanz im Editor",
            ShortHelp: "Ab welcher Nähe eine Zonenkante im Editor einrastet.",
            LongHelp:
                "Beim Bearbeiten eines Layouts rastet eine gezogene Kante an einer Monitor- oder Zonenkante ein, "
                + "sobald sie näher als dieser Wert ist. Ein grösserer Wert erleichtert bündige Layouts, macht "
                + "kleine Feinkorrekturen aber schwieriger. Der Wert 0 schaltet das Einrasten ganz ab. Beim Ziehen "
                + "kannst du es jederzeit mit gedrückter Alt-Taste vorübergehend aussetzen. Diese Einstellung "
                + "betrifft nur den Layout-Editor, nicht das Platzieren von Fenstern.",
            Range: MagnetThresholdRange,
            Keywords: ["Magnet", "Einrasten", "Snap", "Andocken", "Ausrichten"])
    ];

    /// <summary>All settings, in the order the settings page shows them.</summary>
    public static IReadOnlyList<SettingDescriptor> All => Descriptors;

    /// <summary>Looks up one setting. Throws when the key has no descriptor.</summary>
    public static SettingDescriptor For(SettingKey key) =>
        Descriptors.FirstOrDefault(descriptor => descriptor.Key == key)
        ?? throw new KeyNotFoundException($"No descriptor is registered for the setting '{key}'.");

    /// <summary>Settings of one category, in page order.</summary>
    public static IReadOnlyList<SettingDescriptor> InCategory(SettingCategory category) =>
        Descriptors.Where(descriptor => descriptor.Category == category).ToArray();

    /// <summary>
    /// Free-text search across labels, help texts and keywords. An empty term
    /// returns every setting.
    /// </summary>
    public static IReadOnlyList<SettingDescriptor> Search(string? term) =>
        Descriptors.Where(descriptor => descriptor.Matches(term)).ToArray();

    /// <summary>Caption of a settings section.</summary>
    public static string CategoryLabel(SettingCategory category) => category switch
    {
        SettingCategory.Program => "Programm",
        SettingCategory.Activation => "Ziehen und Einblenden",
        SettingCategory.OverlayAppearance => "Darstellung des Overlays",
        SettingCategory.Spacing => "Abstände und Einrasten",
        _ => category.ToString()
    };

    /// <summary>One line describing what a settings section covers.</summary>
    public static string CategoryDescription(SettingCategory category) => category switch
    {
        SettingCategory.Program =>
            "Aussehen des Programmfensters und Verhalten beim Systemstart.",
        SettingCategory.Activation =>
            "Wann und wo die Zonen erscheinen, während du ein Fenster ziehst.",
        SettingCategory.OverlayAppearance =>
            "Farbe und Deckkraft der Zonenflächen. Betrifft nur die Vorschau, nicht die platzierten Fenster.",
        SettingCategory.Spacing =>
            "Abstände in der Overlay-Vorschau und das Einrasten im Layout-Editor.",
        _ => string.Empty
    };
}
