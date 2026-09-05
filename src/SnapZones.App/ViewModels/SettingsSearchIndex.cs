namespace SnapZones.App.ViewModels;

/// <summary>Die Seiten der Oberflaeche, in der Reihenfolge der Seitenleiste.</summary>
public enum NavigationPage
{
    Overview,
    Monitors,
    Layouts,
    Rules,
    Exclusions,
    Behaviour,
    Program
}

/// <summary>Ein Treffer der Einstellungssuche: wohin er fuehrt und wie er heisst.</summary>
/// <param name="Label">Die Einstellung, wie sie auf der Seite steht.</param>
/// <param name="Path">Seite und Untertab in Worten, etwa «Verhalten › Darstellung».</param>
/// <param name="Page">Die Seite, zu der der Treffer fuehrt.</param>
/// <param name="BehaviourTab">Der Untertab der Seite «Verhalten», falls der Treffer dort liegt.</param>
public sealed record SettingsSearchResult(string Label, string Path, NavigationPage Page, int? BehaviourTab = null);

/// <summary>
/// Ein statischer Index aller Einstellungen fuer das Suchfeld in der Seitenleiste. Gesucht wird ueber
/// Beschriftung, Pfad und Synonyme; die Reihenfolge der Treffer folgt der Reihenfolge der Seiten.
/// </summary>
public static class SettingsSearchIndex
{
    public const int MaximumResults = 6;

    private sealed record Entry(SettingsSearchResult Result, string Keywords);

    private const string Behaviour = "Verhalten";
    private static readonly Entry[] Entries =
    [
        Overview("Übersicht", "Start Startseite Monitore Zähler"),
        Page("Monitor umbenennen", "Monitore", NavigationPage.Monitors, "Name Monitorname Bezeichnung"),
        Page("Monitorreihenfolge", "Monitore", NavigationPage.Monitors, "Reihenfolge nach oben unten sortieren"),
        Page("Auf Monitor zeigen", "Monitore", NavigationPage.Monitors, "identifizieren Nummer anzeigen"),
        Page("Erkannte Werte", "Monitore", NavigationPage.Monitors, "Skalierung Auflösung Arbeitsfläche Diagonale DPI EDID"),
        Page("Windows-Einstellungen öffnen", "Monitore", NavigationPage.Monitors, "Anzeige Textgrösse Taskleiste ms-settings"),
        Page("Layout anlegen", "Zonen & Layouts", NavigationPage.Layouts, "neu Vorlage duplizieren Layouts"),
        Page("Zone hinzufügen", "Zonen & Layouts", NavigationPage.Layouts, "Zonen zeichnen bearbeiten Editor"),
        Page("Auffangzone", "Zonen & Layouts", NavigationPage.Layouts, "Hauptzone neue Fenster auffangen"),
        Page("Zonen auf dem Monitor zeichnen", "Zonen & Layouts", NavigationPage.Layouts, "Vollbild Editor echte Grösse"),
        Page("Fenster zuordnen", "Fenster zuordnen", NavigationPage.Rules, "Regel Zuordnung Programm Zone öffnen Fokus Layoutwechsel"),
        Page("Fenster in Ruhe lassen", "In Ruhe lassen", NavigationPage.Exclusions, "Ausschluss ausschliessen ignorieren frei"),
        Tab("Zonen anzeigen auf", "Beim Ziehen", 0, "Overlay Monitor alle Mauszeiger Ziehbeginn"),
        Tab("Zonen einblenden", "Beim Ziehen", 0, "Umschalttaste Shift sofort auslösen"),
        Tab("Zonennamen anzeigen", "Beim Ziehen", 0, "Beschriftung Namen Overlay"),
        Tab("Anzeigeverzögerung", "Beim Ziehen", 0, "Millisekunden Verzögerung warten aufblitzen"),
        Tab("Fenster nach dem Einrasten in den Vordergrund holen", "Beim Ziehen", 0, "aktivieren Fokus"),
        Tab("Grösse beim Herausziehen wiederherstellen", "Beim Ziehen", 0, "Zonengrösse zurück"),
        Tab("Farbe der Zonen", "Darstellung", 1, "Overlayfarbe Hex Grau"),
        Tab("Deckkraft der Zonen", "Darstellung", 1, "Transparenz Prozent Overlay"),
        Tab("Beschriftung", "Darstellung", 1, "Nummer Name Overlay Label"),
        Tab("Rahmenbreite", "Darstellung", 1, "Overlay-Stil Rahmen Pixel Kontur"),
        Tab("Eckenradius", "Darstellung", 1, "Overlay-Stil Ecken abgerundet"),
        Tab("Hervorhebung", "Darstellung", 1, "Zielzone Farbe Deckkraft Overlay-Stil"),
        Tab("Schriftgrösse der Beschriftung", "Darstellung", 1, "Overlay Punkt Schrift"),
        Tab("Abstand zum Bildschirmrand", "Abstände", 2, "Rand aussen Pixel links oben rechts unten"),
        Tab("Abstand zwischen Zonen", "Abstände", 2, "Zonenabstand Zwischenraum Lücke"),
        Tab("Andocken im Editor", "Abstände", 2, "Magnetismus Ausrichtung Alt Hilfslinien"),
        Tab("Fensterpositionen merken", "Fenster merken", 3, "gemerkt zurückkehren verwerfen Katalog"),
        Tab("Vollbild in der Zone halten", "Fenster merken", 3, "YouTube Video Player Fullscreen"),
        Tab("Fenster mit fester Grösse", "Fenster merken", 3, "Dialog zentrieren oben links nicht anfassen"),
        Tab("Toleranz beim Nachmessen", "Fenster merken", 3, "Pixel Feinabstimmung Platzieren"),
        Tab("Toleranz für «eingerastet»", "Fenster merken", 3, "Pixel Kanten Feinabstimmung"),
        Tab("Neue Fenster in der Auffangzone auffangen", "Fenster merken", 3, "Hauptzone neue Fenster"),
        Tab("Gemerkte Zone vor gemerkter Lage", "Fenster merken", 3, "Pixel Zone bevorzugen"),
        Tab("Maximierte Fenster maximiert wiederherstellen", "Fenster merken", 3, "maximiert"),
        Tab("Katalog gemerkter Positionen", "Fenster merken", 3, "Höchstzahl Einträge"),
        Tab("Wartezeit vor dem Beurteilen neuer Fenster", "Fenster merken", 3, "Millisekunden settle"),
        Tab("Abstand zwischen Regelversuchen", "Fenster merken", 3, "Millisekunden Wiederholung Zuordnung"),
        Tab("Schutzschalter des Verschiebe-Hooks", "Fenster merken", 3, "Schutz Ereignisse Grenze Sicherheitsstopp"),
        Tab("Wachhund für hängende Ziehvorgänge", "Fenster merken", 3, "Sekunden Watchdog"),
        Tab("Versuche beim Zonen-Vollbild", "Fenster merken", 3, "Vollbild Korrekturen"),
        Tab("Zonenkürzel aktiv", "Tastenkürzel", 4, "Hotkeys Tasten Kürzel"),
        Tab("Zusatztasten", "Tastenkürzel", 4, "Ctrl Shift Alt Win AltGr Modifier"),
        Tab("Not-Aus", "Tastenkürzel", 4, "F12 anhalten Einrasten pausieren"),
        Page("Erscheinungsbild (hell/dunkel)", "Programm", NavigationPage.Program, "Theme Dunkelmodus Hell Dunkel Windows-System"),
        Page("Mit Windows starten", "Programm", NavigationPage.Program, "Autostart Anmeldung Aufgabenplanung"),
        Page("Updates", "Programm", NavigationPage.Program, "Aktualisierung Version suchen installieren"),
        Page("Administratorrechte", "Programm", NavigationPage.Program, "UAC Rechte erhöht Admin"),
        Page("Installation", "Programm", NavigationPage.Program, "Programme installieren Startmenü"),
        Page("Sicherung", "Programm", NavigationPage.Program, "Export Import Backup JSON"),
        Page("Frühere Stände", "Programm", NavigationPage.Program, "Sicherung wiederherstellen Backup zurück"),
        Page("Fensterhelfer ohne Administratorrechte", "Programm", NavigationPage.Program, "Zertifikat uiAccess Helfer Assistent"),
        Page("Alle Einstellungen zurücksetzen", "Programm", NavigationPage.Program, "Voreinstellung Standard Reset")
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

    private static Entry Overview(string label, string keywords) =>
        new(new SettingsSearchResult(label, "Übersicht", NavigationPage.Overview), keywords);

    private static Entry Page(string label, string path, NavigationPage page, string keywords) =>
        new(new SettingsSearchResult(label, path, page), keywords);

    private static Entry Tab(string label, string tab, int tabIndex, string keywords) =>
        new(new SettingsSearchResult(label, $"{Behaviour} › {tab}", NavigationPage.Behaviour, tabIndex), keywords);
}
