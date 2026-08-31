# UI-Richtlinien

Verbindlich für jede Änderung an der Oberfläche von Sascha’s Zone Manager. Die Werte stehen zentral in
`src\SnapZones.App\Themes\Theme.xaml`; einzelne Seiten dürfen sie nicht lokal überschreiben. Abweichungen
gehören begründet in diesen Abschnitt, nicht als Einzelfall in eine Seite.

Geprüft wird die Einhaltung in `tests\SnapZones.Tests\Theme\ThemeResourceTests.cs`.

---

## 1. Texthierarchie

Vier Ebenen, immer in dieser Reihenfolge und nie mehr. Jede Ebene ist an Grösse, Gewicht **und** Farbe erkennbar,
damit auf einen Blick klar ist, was Titel, was Beschriftung und was Erklärung ist.

| Ebene | Stil | Grösse | Gewicht | Farbe | Verwendung |
|---|---|---|---|---|---|
| Seitentitel | `PageTitle` | 30 | SemiBold | `InkBrush` | Genau einmal pro Navigationsseite, ganz oben |
| Kartentitel | `SectionTitle` | 20 | SemiBold | `InkBrush` | Überschrift einer Karte (`SectionCard`) |
| Gruppentitel | `GroupTitle` | 16 | SemiBold | `InkBrush` | Überschrift einer Feldgruppe (`FieldGroup`) innerhalb einer Karte |
| Feldbeschriftung | `FieldLabel` | 15 | SemiBold | `InkBrush` | Direkt über genau einem Eingabefeld |
| Fliesstext | — | 16 | Normal | `InkBrush` | Inhalte, Listeneinträge, Werte |
| Hilfetext | `HelpText` | 14 | Normal | `SubtleInkBrush` | Erklärung unter einem Feld oder einer Gruppe |

`HelpText` ist der einzige Stil, der `SubtleInkBrush` verwendet. Dadurch bleibt Hilfetext von einer
Feldbeschriftung unterscheidbar, auch wenn beide gleich lang sind. `MutedBrush` bleibt sekundären **Daten**
vorbehalten (Monitordetails, Werteinheiten, zweite Zeile in einer Liste) — nicht Erklärungen.

## 2. Hilfetexte

Es gibt genau zwei Orte für Hilfe, und sie haben getrennte Aufgaben:

1. **Hilfetext unter dem Feld** (`HelpText`) — ein Satz, immer sichtbar, beantwortet «was gehört hier hinein».
   Er nennt möglichst ein konkretes Beispiel und die Bedeutung eines leeren Feldes.
2. **Info-Schaltfläche** (`InfoButton` mit `ToolTip`) — zwei bis fünf Sätze, auf Abruf, beantwortet «wozu ist
   das gut, was ändert sich dadurch, wann brauche ich es nicht». Sie steht rechts neben der Beschriftung,
   nie darunter.

Regeln für beide:

- Deutsch, ganze Sätze, Schweizer Schreibweise ohne «ß».
- Wirkung vor Mechanik: erst was passiert, dann wo es gilt, dann die Grenzen.
- Zulässige Wertebereiche gehören in den ToolTip, nicht in die Beschriftung.
- Keine Wiederholung des Feldnamens («Titelmuster: Das Titelmuster …»).
- Jede Info-Schaltfläche braucht ein `AutomationProperties.Name`; der ToolTip ist mindestens 30 Zeichen lang,
  bei erklärungsbedürftigen Feldern mindestens 120.
- Was Windows nicht unterstützt, wird benannt statt verschwiegen.

## 3. Steuerelementhöhe

`Button`, `TextBox` und `ComboBox` haben dieselbe `MinHeight` von 40. Keine Seite setzt `Height` auf einem
dieser Elemente.

Der Standardrand eines Buttons ist `0`. Abstände werden dort gesetzt, wo sie gebraucht werden — sonst streckt
ein `StackPanel` die randlose Schaltfläche auf die Höhe der gerandeten daneben, und zwei nebeneinander
stehende Buttons wirken unterschiedlich hoch. Genau daran lag die ungleiche Höhe von «Nach oben»/«Nach unten»
und «+ Regel»/«Löschen».

Einzeilige Eingabefelder werden ausschliesslich waagrecht gepolstert (`Padding="12,0"`) und senkrecht zentriert
(`VerticalContentAlignment="Center"`). Senkrechtes Padding zusammen mit fester Höhe hat den Text im
Layoutnamen-Feld abgeschnitten.

## 4. Gruppierung

- `SectionCard` — eine Karte pro Thema, Titel als `SectionTitle`.
- `FieldGroup` — eine Gruppe innerhalb einer Karte, Titel als `GroupTitle`. Ab etwa fünf Feldern in einer Karte
  ist zu gruppieren.
- Gehört eine Gruppe zu einer Abfolge, wird sie nummeriert («1 · Programm», «2 · Fenster eingrenzen»).
- Optionale Felder tragen «(optional)» im Gruppentitel, nicht in jeder einzelnen Beschriftung.

## 5. Masseinheiten

Eine Einheit wird **pro Karte an genau einer Stelle** umgeschaltet und gilt dann für alle Felder dieser Karte
(`UnitSegment`/`UnitSegmentActive`). Keine Umschaltung pro Feld.

Innerhalb eines sichtbaren Blocks darf nie mehr als eine Einheit erscheinen. Wo ein Wert zusätzlich in einer
zweiten Einheit interessant ist, steht er als abgeleitete Anzeige in `SubtleInkBrush` daneben, erkennbar
gekennzeichnet (`≙ 12 px`), nie als eigenständige Beschriftung.

Prozentregler laufen in ganzen Prozentschritten (`TickFrequency="1"`, `IsSnapToTickEnabled="True"`), und das
Zahlenfeld daneben zeigt denselben Wert (`StringFormat={}{0:0}`).

## 6. Schaltflächen

- `PrimaryButton` (Akzentfarbe) für die Hauptaktion eines Bereichs. Stehen zwei gleichrangige Aktionen
  nebeneinander — wie Export und Import —, tragen **beide** denselben Stil; ein farbiger neben einem grauen
  Button liest sich sonst als Rangunterschied, den es nicht gibt.
- Standardstil für alles Weitere.
- Löschende Aktionen fragen über eine `MessageBox` nach und benennen darin konkret, was verloren geht.

## 7. Barrierefreiheit

- Jedes Bedienelement hat ein `AutomationProperties.Name` in ganzen Worten.
- Text auf Fläche erreicht mindestens 4.5:1, Rahmen mindestens 3:1 — in hellem **und** dunklem Theme.
  Neue Farben gehören in beide Paletten in `ThemeService.ApplyPalette` und in die Kontrastprüfung der Tests.
- Farbe ist nie der einzige Träger einer Information.
