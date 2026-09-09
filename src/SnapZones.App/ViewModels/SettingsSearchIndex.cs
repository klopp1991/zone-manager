namespace SnapZones.App.ViewModels;

/// <summary>
/// Die vierzehn Seiten der Oberflaeche, in der Reihenfolge der Seitenleiste. Es gibt keine Untertabs
/// mehr: jede fruehere Registerkarte von «Verhalten» und «Programm» ist eine eigene Seite.
/// </summary>
public enum NavigationPage
{
    // EINRICHTEN
    Layouts,
    Monitors,
    Rules,
    Exclusions,

    // VERHALTEN
    Drag,
    Appearance,
    Spacing,
    Memory,
    Fullscreen,
    Keys,

    // PROGRAMM
    Startup,
    Backup,
    System,
    About
}

/// <summary>Ein Treffer der Einstellungssuche: wohin er fuehrt und wie er heisst.</summary>
/// <param name="Label">Die Einstellung, wie sie auf der Seite steht.</param>
/// <param name="Path">Die Seite in Worten, etwa «Ziehen &amp; Einrasten › Feinabstimmung».</param>
/// <param name="Page">Die Seite, zu der der Treffer fuehrt.</param>
/// <param name="TuningSection">
/// Der Name des Feinabstimmungs-Abschnitts, der beim Springen aufklappen soll; <c>null</c>, wenn der
/// Treffer offen auf der Seite steht.
/// </param>
public sealed record SettingsSearchResult(
    string Label,
    string Path,
    NavigationPage Page,
    string? TuningSection = null);

/// <summary>
/// Ein statischer Index aller Einstellungen fuer das Suchfeld in der Seitenleiste. Gesucht wird ueber
/// Beschriftung, Pfad und Synonyme; die Reihenfolge der Treffer folgt der Reihenfolge der Seiten.
/// </summary>
public static class SettingsSearchIndex
{
    public const int MaximumResults = 6;

    /// <summary>Der Name des Feinabstimmungs-Abschnitts; jede Seite hat hoechstens einen.</summary>
    public const string TuningSectionName = "Feinabstimmung";

    private sealed record Entry(SettingsSearchResult Result, string Keywords);

    private static readonly Entry[] Entries =
    [
        // EINRICHTEN
        Page("Layout anlegen", "Zonen & Layouts", NavigationPage.Layouts, "neu Vorlage duplizieren Layouts"),
        Page("Zone hinzufügen", "Zonen & Layouts", NavigationPage.Layouts, "Zonen zeichnen bearbeiten Editor"),
        Page("Startzone", "Zonen & Layouts", NavigationPage.Layouts, "neue Fenster auffangen erste Zone"),
        Page("Vollbildzone", "Zonen & Layouts", NavigationPage.Layouts, "eigener Bildschirm Video Vollbild Zone"),
        Page("Layoutreihenfolge", "Zonen & Layouts", NavigationPage.Layouts, "sortieren schieben links rechts ziehen"),
        Page("Auf dem Monitor zeichnen", "Zonen & Layouts", NavigationPage.Layouts, "Vollbild Editor echte Grösse"),
        Page("Monitor umbenennen", "Monitore", NavigationPage.Monitors, "Name Monitorname Bezeichnung"),
        Page("Monitorreihenfolge", "Monitore", NavigationPage.Monitors, "sortieren schieben links rechts ziehen"),
        Page("Auf Monitor zeigen", "Monitore", NavigationPage.Monitors, "identifizieren Nummer anzeigen"),
        Page("Erkannte Werte", "Monitore", NavigationPage.Monitors, "Skalierung Auflösung Arbeitsfläche Diagonale DPI EDID"),
        Page("Windows-Einstellungen öffnen", "Monitore", NavigationPage.Monitors, "Anzeige Textgrösse Taskleiste ms-settings"),
        Page("Fenster zuordnen", "Fenster zuordnen", NavigationPage.Rules, "Regel Zuordnung Programm Zone öffnen Fokus Layoutwechsel"),
        Page("Fenster in Ruhe lassen", "In Ruhe lassen", NavigationPage.Exclusions, "Ausschluss ausschliessen ignorieren frei"),

        // VERHALTEN
        Page("Zonen anzeigen auf", "Ziehen & Einrasten", NavigationPage.Drag, "Overlay Monitor alle Mauszeiger Ziehbeginn"),
        Page("Zonen einblenden", "Ziehen & Einrasten", NavigationPage.Drag, "Umschalttaste Shift sofort auslösen"),
        Page("Zonennamen anzeigen", "Ziehen & Einrasten", NavigationPage.Drag, "Beschriftung Namen Overlay"),
        Page("Fenster nach dem Einrasten in den Vordergrund holen", "Ziehen & Einrasten", NavigationPage.Drag, "aktivieren Fokus"),
        Page("Grösse beim Herausziehen wiederherstellen", "Ziehen & Einrasten", NavigationPage.Drag, "Zonengrösse zurück"),
        Tuning("Anzeigeverzögerung", "Ziehen & Einrasten", NavigationPage.Drag, "Millisekunden warten aufblitzen"),
        Tuning("Toleranz für «eingerastet»", "Ziehen & Einrasten", NavigationPage.Drag, "Pixel Kanten"),
        Tuning("Schutzschalter des Verschiebe-Hooks", "Ziehen & Einrasten", NavigationPage.Drag, "Schutz Ereignisse Grenze Sicherheitsstopp"),
        Tuning("Wachhund für hängende Ziehvorgänge", "Ziehen & Einrasten", NavigationPage.Drag, "Sekunden Watchdog"),
        Page("Farbe der Zonen", "Aussehen der Zonen", NavigationPage.Appearance, "Overlayfarbe Hex Grau"),
        Page("Deckkraft", "Aussehen der Zonen", NavigationPage.Appearance, "Transparenz Prozent Overlay"),
        Page("Beschriftung", "Aussehen der Zonen", NavigationPage.Appearance, "Nummer Name Overlay Label keine"),
        Page("Schriftgrösse der Beschriftung", "Aussehen der Zonen", NavigationPage.Appearance, "Overlay Punkt Schrift"),
        Tuning("Rahmenbreite", "Aussehen der Zonen", NavigationPage.Appearance, "Rahmen Pixel Kontur"),
        Tuning("Eckenradius", "Aussehen der Zonen", NavigationPage.Appearance, "Ecken abgerundet"),
        Tuning("Hervorhebung der Zielzone", "Aussehen der Zonen", NavigationPage.Appearance, "Zielzone Farbe Deckkraft blau"),
        Page("Rand zum Bildschirm", "Abstände & Raster", NavigationPage.Spacing, "aussen Pixel links oben rechts unten"),
        Page("Lücke zwischen den Zonen", "Abstände & Raster", NavigationPage.Spacing, "Zonenabstand Zwischenraum Abstand"),
        Page("Andocken im Editor", "Abstände & Raster", NavigationPage.Spacing, "Magnetismus Ausrichtung Alt Hilfslinien"),
        Tuning("Toleranz beim Nachmessen", "Abstände & Raster", NavigationPage.Spacing, "Pixel Platzieren"),
        Page("Fensterpositionen merken", "Fenster merken", NavigationPage.Memory, "gemerkt zurückkehren verwerfen"),
        Page("Neue Fenster in der Startzone öffnen", "Fenster merken", NavigationPage.Memory, "Startzone neue Fenster auffangen"),
        Page("Maximierte Fenster maximiert wiederherstellen", "Fenster merken", NavigationPage.Memory, "maximiert"),
        Page("Fenster mit fester Grösse", "Fenster merken", NavigationPage.Memory, "Dialog zentrieren oben links nicht anfassen"),
        Page("Gemerkte Zone vor gemerkter Lage", "Fenster merken", NavigationPage.Memory, "Pixel Zone bevorzugen"),
        Tuning("Katalog gemerkter Positionen", "Fenster merken", NavigationPage.Memory, "Höchstzahl Einträge"),
        Tuning("Wartezeit vor dem Beurteilen neuer Fenster", "Fenster merken", NavigationPage.Memory, "Millisekunden settle"),
        Tuning("Abstand zwischen Regelversuchen", "Fenster merken", NavigationPage.Memory, "Millisekunden Wiederholung Zuordnung"),
        Page("Vollbildzone verwenden", "Vollbild & Videos", NavigationPage.Fullscreen, "Vollbild Video YouTube Zone eigener Bildschirm"),
        Page("Vollbildzonen-Treiber", "Vollbild & Videos", NavigationPage.Fullscreen, "Treiber installieren entfernen prüfen virtueller Monitor"),
        Page("Programme ohne Vollbildzone", "Vollbild & Videos", NavigationPage.Fullscreen, "Ausnahme Spiel Programm eintragen"),
        Page("Zonenkürzel aktiv", "Tastenkürzel", NavigationPage.Keys, "Hotkeys Tasten Kürzel"),
        Page("Zusatztasten", "Tastenkürzel", NavigationPage.Keys, "Ctrl Shift Alt Win AltGr Modifier"),
        Page("Not-Aus", "Tastenkürzel", NavigationPage.Keys, "F12 anhalten Einrasten pausieren"),

        // PROGRAMM
        Page("Erscheinungsbild", "Aussehen & Start", NavigationPage.Startup, "Theme Dunkelmodus Hell Dunkel Windows-System"),
        Page("Mit Windows starten", "Aussehen & Start", NavigationPage.Startup, "Autostart Anmeldung"),
        Page("Beim Schliessen in den Infobereich", "Aussehen & Start", NavigationPage.Startup, "Tray schliessen beenden X"),
        Page("Updates", "Aussehen & Start", NavigationPage.Startup, "Aktualisierung Version suchen installieren"),
        Page("Update-Quelle", "Aussehen & Start", NavigationPage.Startup, "GitHub Konto verbinden trennen Gerätecode privat"),
        Page("Sicherung als Datei", "Sicherung & Stände", NavigationPage.Backup, "Export Import Backup JSON"),
        Page("Frühere Stände", "Sicherung & Stände", NavigationPage.Backup, "Sicherung wiederherstellen Backup zurück"),
        Page("Alle Einstellungen zurücksetzen", "Sicherung & Stände", NavigationPage.Backup, "Voreinstellung Standard Reset von vorn"),
        Page("Administratorrechte", "System & Rechte", NavigationPage.System, "UAC Rechte erhöht Admin abgeben"),
        Page("Fensterhelfer ohne Administratorrechte", "System & Rechte", NavigationPage.System, "Zertifikat uiAccess Helfer einrichten entfernen"),
        Page("Vollbildzonen-Treiber", "System & Rechte", NavigationPage.System, "Treiber installieren entfernen prüfen"),
        Page("Diagnose", "System & Rechte", NavigationPage.System, "Protokoll Log prüfen ausführen"),
        Page("Protokolldatei", "Über Zone Manager", NavigationPage.About, "Log Ordner leeren"),
        Page("Einstellungsdatei", "Über Zone Manager", NavigationPage.About, "settings.json Ordner Layouts"),
        Page("Projektseite", "Über Zone Manager", NavigationPage.About, "GitHub Lizenz MIT Version")
    ];

    /// <summary>Hoechstens sechs Treffer zum Suchbegriff; ein leerer Begriff liefert keine.</summary>
    public static IReadOnlyList<SettingsSearchResult> Search(string? query)
    {
        var normalized = query?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            return [];
        }

        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return Entries
            .Where(entry => words.All(word => Matches(entry, word)))
            .Select(entry => entry.Result)
            .Take(MaximumResults)
            .ToArray();
    }

    private static bool Matches(Entry entry, string word) =>
        entry.Result.Label.Contains(word, StringComparison.CurrentCultureIgnoreCase) ||
        entry.Result.Path.Contains(word, StringComparison.CurrentCultureIgnoreCase) ||
        entry.Keywords.Contains(word, StringComparison.CurrentCultureIgnoreCase);

    private static Entry Page(string label, string path, NavigationPage page, string keywords) =>
        new(new SettingsSearchResult(label, path, page), keywords);

    private static Entry Tuning(string label, string path, NavigationPage page, string keywords) =>
        new(new SettingsSearchResult(label, $"{path} › {TuningSectionName}", page, TuningSectionName), keywords);
}
