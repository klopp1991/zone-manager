# UI-Richtlinien

Verbindlich für jede Änderung an der Oberfläche von Zone Manager. Die Werte stehen zentral in
`src\SnapZones.App\Themes\Theme.xaml`; einzelne Seiten dürfen sie nicht lokal überschreiben. Abweichungen
gehören begründet in diesen Abschnitt, nicht als Einzelfall in eine Seite.

Geprüft wird die Einhaltung in `tests\SnapZones.Tests\Theme\ThemeResourceTests.cs` und den übrigen Tests
unter `tests\SnapZones.Tests\Theme`.

---

## 1. Texthierarchie

Vier Ebenen, immer in dieser Reihenfolge und nie mehr. Jede Ebene ist an Grösse, Gewicht **und** Farbe erkennbar,
damit auf einen Blick klar ist, was Titel, was Beschriftung und was Erklärung ist.

| Ebene | Stil | Grösse | Gewicht | Farbe | Verwendung |
|---|---|---|---|---|---|
| Kartentitel | `SectionTitle` | 20 | SemiBold | `InkBrush` | Überschrift einer Karte (`SectionCard`) |
| Gruppentitel | `GroupTitle` | 16 | SemiBold | `InkBrush` | Überschrift einer Feldgruppe (`FieldGroup`) oder einer Karte mit Einstellungszeilen |
| Einstellungsbeschriftung | `SettingLabel` | 16 | SemiBold | `InkBrush` | Links in einer Einstellungszeile, gefolgt vom «?» |
| Feldbeschriftung | `FieldLabel` | 15 | SemiBold | `InkBrush` | Direkt über genau einem Eingabefeld |
| Fliesstext | — | 16 | Normal | `InkBrush` | Inhalte, Listeneinträge, Werte |
| Hilfetext | `HelpText` | 14 | Normal | `SubtleInkBrush` | Erklärung unter einem Titel, Untertitel einer Zeile |
| Sekundäre Daten | `ListSubtitle`, `UnitLabel` | 14 / 15 | Normal | `MutedBrush` | Zweite Zeile in einer Liste, Einheit neben einem Zahlenfeld |
| Gruppenüberschrift der Seitenleiste | — | 11 | SemiBold, Versalien | `MutedBrush` | EINRICHTEN · VERHALTEN · PROGRAMM |

**Seiten tragen keinen Titel.** Der Name der Seite steht in der Seitenleiste; ihn oben zu wiederholen
kostet eine Bildschirmzeile und sagt nichts Neues. `PageTitle` ist deshalb entfallen. Jede Seite beginnt
unmittelbar mit ihrem Inhalt und nutzt die volle Breite neben der Seitenleiste.

`HelpText` ist der einzige Fliesstextstil mit `SubtleInkBrush`. `MutedBrush` bleibt sekundären **Daten**
vorbehalten (Monitordetails, Einheiten, zweite Zeile in einer Liste, Statuszeile) — nicht Erklärungen.
Zahlen stehen in `Cascadia Mono` (Zahlenfelder 15, Kürzel und Zeitstempel 14, Masse im Vollbild 13).

## 2. Erklärungen: das «?»

Jede Einstellung, jede Gruppe und jede erklärungsbedürftige Aktion trägt rechts neben ihrer Beschriftung ein
**«?»** (`InfoButton`: 18-px-Kreis in 22-px-Schaltfläche, ToolTip nach 250 ms, bleibt 30 s). Es ersetzt die
früheren Info-Schaltflächen **und** die meisten Hilfetexte unter den Feldern. Der ToolTip antwortet auf «was
tut das?»:

1. Wirkung vor Mechanik: erst was passiert, dann wo es gilt, dann die Grenzen.
2. Ein Beispiel («Mit z. B. 300 bleiben kurze Verschiebungen ruhig»).
3. Die Bedeutung des leeren Werts oder des Zustands «Aus».
4. Der zulässige Wertebereich und die Voreinstellung.

Zwei bis fünf Sätze, mindestens 30 Zeichen, bei erklärungsbedürftigen Feldern mindestens 120. Deutsch, ganze
Sätze, Schweizer Schreibweise ohne «ß», Ansprache mit «du». Keine Wiederholung des Feldnamens. Was Windows
nicht unterstützt, wird benannt statt verschwiegen. Jedes «?» braucht ein `AutomationProperties.Name` und ist
per Tastatur erreichbar; der ToolTip öffnet auch bei Tastaturfokus.

`HelpText` bleibt für zwei Fälle: den Untertitel einer Einstellungszeile und die Bedeutung eines leeren
Felds direkt unter dem Feld («Leer = automatischer Name «Monitor 1».»). Erklärt wird nicht doppelt: steht
etwas im «?», steht es nicht noch einmal darunter.

**Untertitel-Regel (verbindlich).** Der Untertitel einer Einstellungszeile sagt in Alltagssprache, *was
passiert* — «Auf welchen Monitoren die Zonen erscheinen», «Das ✕ beendet das Programm nicht». Keine
Fachbegriffe, keine Mechanik, keine Pfade: nicht «über eine Anmeldeaufgabe», nicht «Hooks», nicht «UAC».
Alles Technische gehört in den ToolTip des «?».

## 3. Steuerelementhöhe

`Button`, `TextBox` und `ComboBox` haben dieselbe `MinHeight` von 40. Keine Seite setzt `Height` auf einem
dieser Elemente. Vier benannte Ausnahmen mit eigenem Stil: `ListIconButton` (36 × 36) in einer Listenzeile,
`ListButton` (36), die Werkzeugleiste des Vollbild-Editors (36) und `CompactButton` (30) in einer
Tabellenzeile wie den früheren Ständen. `IconButton` ist 40 × 40 und trägt genau ein Zeichen.

Der Standardrand eines Buttons ist `0`. Abstände werden dort gesetzt, wo sie gebraucht werden — sonst streckt
ein `StackPanel` die randlose Schaltfläche auf die Höhe der gerandeten daneben, und zwei nebeneinander
stehende Buttons wirken unterschiedlich hoch.

Einzeilige Eingabefelder werden ausschliesslich waagrecht gepolstert (`Padding="12,0"`) und senkrecht zentriert
(`VerticalContentAlignment="Center"`). Zahlenfelder in Einstellungszeilen sind `NumberField`: 100 breit,
rechtsbündig, `Cascadia Mono`, die Einheit steht als `UnitLabel` daneben. Ein Platzhalter kommt über
`controls:Chrome.Placeholder`, nie über einen vorbelegten Text.

## 4. Seiten und Gruppierung

- **Seitenleiste** 220 px mit Suchfeld oben und vierzehn Einträgen in drei Gruppen. Die Liste ist scrollbar,
  damit alle Einträge auch auf kleinen Bildschirmen erreichbar bleiben. Aktiver Eintrag mit `HoverBrush`-Grund
  und 3 × 16 px Balken in `AccentBrush`, rechts ein Zähler in `MutedBrush` 12. Gruppe und Zähler hängen als
  `controls:Chrome.Group` und `controls:Chrome.Badge` am `TabItem`.

  | Gruppe | Seiten |
  |---|---|
  | EINRICHTEN | Zonen & Layouts · Monitore · Fenster zuordnen · In Ruhe lassen |
  | VERHALTEN | Ziehen & Einrasten · Aussehen der Zonen · Abstände & Raster · Fenster merken · Vollbild & Videos · Tastenkürzel |
  | PROGRAMM | Aussehen & Start · Sicherung & Stände · System & Rechte · Über Zone Manager |

  Startseite ist **Zonen & Layouts**; ein Klick auf das Logo in der Kopfzeile führt dorthin zurück. Untertabs
  gibt es nicht: jede Seite steht für sich.
- **Inhalt** Padding 22 28, **keine** Breitenbegrenzung. Jede Seite füllt, was neben der Seitenleiste bleibt.
- `SectionCard` — eine Karte pro Thema, Titel als `SectionTitle` oder, bei Einstellungszeilen, `GroupTitle`.
- `FieldGroup` — eine Gruppe innerhalb einer Karte, aufklappbar als `Expander`, wenn sie selten gebraucht wird
  (Fenster eingrenzen, Feinheiten, Windows-Einstellungen).
- **Einstellungszeile** (`SettingRow`, letzte Zeile `SettingRowLast`) — Grid `* | 300`, Padding 0 12, Trennlinie
  `BorderBrush`. Links `SettingLabel`, optional ein `StatusChip`, das «?» und darunter **ein** Untertitel; rechts
  genau ein Steuerelement: ComboBox, `NumberField` + Einheit, Slider + Zahlenfeld + `≙ n px`, Schalter
  rechtsbündig, Farbfeld 36 × 36 + Hexwert oder ein bis zwei Schaltflächen.
- **Feinabstimmung** (`TuningExpander`) — die Expertenwerte einer Seite stehen zugeklappt am Seitenfuss, mit
  Pfeil, dem Titel «Feinabstimmung» und rechts einem Stichwort. Ein Klick auf die ganze Zeile schaltet um; der
  Zustand gilt nur für die laufende Sitzung. Ein Suchtreffer, der dorthin führt, klappt sie beim Springen auf.
- **Status-Chip** (`StatusChip`) — die Pille hinter der Beschriftung einer Systemsache: `● Aktiv` grün,
  `● Nicht installiert` orange, `○ Nicht eingerichtet` grau. Punkt und Wort tragen den Zustand; das Chip bricht
  nie um, die Zeile darum herum darf es.
- **Reihenfolge-Tab** (`ReorderTabItem`) — Monitore und Layouts stehen als Tabs. Der aktive Tab trägt am Ende
  zwei 20 × 20-Pfeile `‹ ›`; am Rand sind sie deaktiviert. Ein Pfeilklick löst keinen Tabwechsel aus
  (`e.Handled = true`). Zusätzlich lässt sich ein Tab an seine neue Stelle ziehen. Die Reihenfolge gilt überall:
  Tabs, Auswahllisten, Infobereich.
- **Listenzeile** (`ListRow`) — Padding 14 20, Abstand 8: Symbol 36 × 36 (echtes Programmsymbol, sonst zwei
  Buchstaben auf `AccentSoftBrush`), Titel SemiBold, `ListSubtitle`, Schalter, `ListIconButton`. Das Detail klappt
  darunter auf (Einzug 52, Rand `ControlBorderBrush`); es ist höchstens eines offen. Am Listenende steht eine
  gestrichelte Zeile über die ganze Breite, die einen neuen Eintrag anlegt.
- **Leerer Zustand** — gestrichelte Karte (`ControlBorderBrush` 4 3, Radius 8, Padding 32) mit Titel, einem
  Beispielsatz und genau einem `PrimaryButton`. Illustrationen sind aus Rechtecken gezeichnet, nie Bilder.
- Optionale Felder tragen «(optional)» oder «(selten nötig)» in der Beschriftung, nicht in jeder Zeile.

## 5. Masseinheiten

Eine Einheit wird **pro Panel an genau einer Stelle** umgeschaltet und gilt dann für alle Felder dieses Panels
(`UnitSegment`/`UnitSegmentActive`, 44 × 28). Keine Umschaltung pro Feld.

Innerhalb eines sichtbaren Blocks darf nie mehr als eine Einheit erscheinen. Wo ein Wert zusätzlich in einer
zweiten Einheit interessant ist, steht er als abgeleitete Anzeige in `SubtleInkBrush` daneben, erkennbar
gekennzeichnet (`≙ 12 px`), nie als eigenständige Beschriftung. Einzige Ausnahme ist der Vollbild-Editor: dort
zeigt die Massangabe jeder Zone Prozent **und** Pixel («50 × 100 % · 2560 × 2100 px»), weil die Zone in echter
Grösse zu sehen ist und beide Zahlen zum Sichtbaren gehören.

Prozentregler laufen in ganzen Prozentschritten (`TickFrequency="1"`, `IsSnapToTickEnabled="True"`), und das
Zahlenfeld daneben zeigt denselben Wert (`StringFormat={}{0:0}`).

## 6. Schaltflächen und Schalter

- `PrimaryButton` (Akzentfarbe) für die Hauptaktion eines Bereichs. Stehen zwei gleichrangige Aktionen
  nebeneinander — wie Exportieren und Importieren —, tragen **beide** denselben Stil; ein farbiger neben einem
  grauen Button liest sich sonst als Rangunterschied, den es nicht gibt.
- Standardstil für alles Weitere; `LinkButton` (unterstrichen, `MutedBrush`) für Verweise im Fliesstext
  («ändern», «alle verwerfen», «Jetzt einrichten»).
- **Icon-Knopf in einer Listenzeile** (`ListIconButton`, 36 × 36, genau ein Zeichen) statt eines Textknopfs.
  Jeder trägt einen ToolTip in ganzen Worten **und** ein `AutomationProperties.Name` mit dem Namen der Sache.

  | Zeichen | Bedeutung | ToolTip-Beispiel |
  |---|---|---|
  | `✎` | bearbeiten, eingrenzen | «Zuordnung bearbeiten» |
  | `✕` | Detail schliessen | «Schliessen» |
  | `!` in `WarningBrush` | Fehler beheben | «Ziel fehlt – beheben» |
  | `⧉` | in die Zwischenablage | «Code in die Zwischenablage kopieren» |
  | `↶` / `↷` | rückgängig, wiederherstellen | «Rückgängig (Strg + Z)» |

  Löschende Aktionen bleiben **Text**knöpfe in `DangerBrush` («Zuordnung entfernen», «Deinstallieren»).
- **Jede Systemsache lässt sich einrichten und entfernen.** Steht eine Sache (Treiber, Zertifikat), zeigt die
  Zeile neben dem Chip beide Wege; sie verschwindet nicht hinter einer grauen Schaltfläche.
- `DangerButton` (Standardrahmen, Text in `DangerBrush`) für löschende Aktionen; Farbe ist nie der einzige
  Träger, der Text sagt, was geschieht («Zuordnung entfernen», «Wieder einrasten lassen»).
- **Schalter** (`ToggleSwitch`, 40 × 20, Knopf 12; an = `AccentBrush` mit `AccentInkBrush`-Knopf, aus =
  `SurfaceRaisedBrush` mit `MutedBrush`-Knopf, Fokusring `AccentBrush` 2 px) ersetzen die Checkbox in Listen
  und Einstellungszeilen. Die Checkbox bleibt dort, wo sie ein Merkmal einer Sache setzt («Startzone» im
  Werte-Panel).
- Menüs (`+ Neu ⌵`, `Vorlage ⌵`, `Reihenfolge ⌵`, Rechtsklick) sind `ContextMenu`s im Theme: `SurfaceBrush`,
  Rand `ControlBorderBrush`, Radius 6, Einträge Padding 7 10, Kürzel rechts in `SubtleInkBrush`, löschender
  Eintrag in `DangerBrush`, Trenner vor ihm.

## 7. Rückgängig statt Nachfrage

Löschende und zurücksetzende Aktionen fragen **nicht** nach. Sie werden sofort ausgeführt und gespeichert, und
ein **Toast** unten in der Mitte (56 px über der Statuszeile, `InkBrush`-Grund mit `CanvasBrush`-Text, Radius 8,
Padding 10 12 10 16, Schatten) nennt, was geschah, mit den Schaltflächen **Rückgängig** und **✕**. Er bleibt
sechs Sekunden, länger, solange der Mauszeiger darauf liegt; ein neuer Toast ersetzt den alten.
Rückgängig schreibt das gelöschte Objekt an seine Stelle zurück (`MainViewModel.ShowToast(text, undo)`).

Das gilt für: Zuordnung entfernen, wieder einrasten lassen, Zone entfernen, Zonen verbinden, Vorlage übernehmen,
Layout löschen, gemerkte Positionen verwerfen, Einstellungen zurücksetzen, früheren Stand wiederherstellen.

Eine `MessageBox` bleibt nur, wo sich nichts zurücknehmen lässt und Windows selbst eingreift: Zertifikat
einrichten oder entfernen, Installation, Update installieren. Sie benennt konkret, was geschieht.

## 8. Barrierefreiheit

- Jedes Bedienelement hat ein `AutomationProperties.Name` in ganzen Worten; in Datenvorlagen mit dem Namen der
  Sache («Zuordnung Explorer.exe bearbeiten»).
- Text auf Fläche erreicht mindestens 4.5:1, Rahmen mindestens 3:1 — in hellem **und** dunklem Theme.
  Neue Farben gehören in beide Paletten in `ThemeService.ApplyPalette` und in die Kontrastprüfung der Tests.
  Seit dem 05.09.2026 dazu: `SuccessBrush` (#2E7D32 / #8FD18F, «● aktiv»), `WarningBorderBrush` (#B5842A /
  #A67A2E, Rand einer pausierten Zuordnung – bewusst kräftiger als im Entwurf, damit 3:1 gegen `SurfaceBrush`
  erreicht wird), `DropTargetBrush` (#2F6FED, Ablagefläche) und `DropTargetInkBrush` (#245AC5 / #8FB3F5, «Hier
  ablegen»). Seit dem 10.09.2026 die drei Gründe der Status-Chips: `SuccessSoftBrush` (#EFF7EF / #1E2A1E –
  heller als im Entwurf, damit `SuccessBrush` darauf 4.5:1 erreicht), `WarningSoftBrush` (#F7EEDC / #2B2418)
  und `NeutralChipBrush` (wie `SurfaceRaisedBrush`).
- Farbe ist nie der einzige Träger einer Information: die pausierte Zuordnung trägt Text und Schaltfläche, die
  Startzone ein beschriftetes Feld, das aktive Layout das Wort «aktiv».
- Doppelklick und Rechtsklick haben immer einen zweiten Weg: das Werte-Panel für den Namen, das Menü für alles
  andere, `Entf` für das Löschen.
