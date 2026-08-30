# Sascha Window Zones: Fensterplatzierungs-Engine

**Datum:** 30. August 2026  
**Status:** Zur schriftlichen Freigabe  
**Zielplattform:** Windows 11 x64  
**Technologie:** .NET 8, WPF und dokumentierte Windows-Schnittstellen

## 1. Ziel

Sascha Window Zones soll geeignete Fenster beim erneuten Öffnen automatisch an ihrer zuletzt verwendeten Platzierung darstellen. Das gilt als globaler Standard und nicht nur für einzelne Programme wie Windows-Einstellungen oder Excel.

Die Wiederherstellung umfasst:

- den zuletzt verwendeten Monitor;
- die zuletzt verwendete Sascha-Window-Zone, sofern eine zugeordnet war;
- die letzte normale Fensterposition und Fenstergrösse;
- den maximierten Zustand;
- eine einmalige Positionierung nach dem Öffnen, ohne spätere manuelle Änderungen zu verhindern.

Mehrere Fenster desselben Programms teilen die letzte Platzierung, wenn sie zum gleichen Fenstertyp gehören. Hauptfenster und Dialogtypen werden getrennt behandelt. Optionale Regeln können für einen Fenstertyp eine feste Zone erzwingen oder ihn vollständig von der Verwaltung ausschliessen.

## 2. Abnahmekriterien

Der Ausbau gilt als fachlich abgeschlossen, wenn:

1. ein geeignetes Fenster nach einer manuellen Verschiebung oder Grössenänderung beim nächsten Öffnen einmalig mit dieser Platzierung erscheint;
2. ein maximiert geschlossenes Fenster beim nächsten Öffnen wieder maximiert wird;
3. ein minimiertes Fenster nie minimiert wiederhergestellt wird;
4. Windows-Einstellungen ohne Sondercode wie jedes andere unterstützte Fenster behandelt werden;
5. Excel-Hauptfenster eine gemeinsame Platzierung verwenden, während Excel-Dialoge getrennte Fenstertypen bleiben;
6. eine feste App-Regel die gelernte Platzierung zuverlässig übersteuert;
7. eine Ausschlussregel jede automatische Positionierung und Aufzeichnung für den betroffenen Fenstertyp verhindert;
8. fehlende Monitore, geänderte Auflösungen und geänderte DPI-Werte kein Fenster ausserhalb sichtbarer Arbeitsflächen erzeugen;
9. die Wiederherstellung weder den Fokus stiehlt noch ein Fenster nach der einmaligen Positionierung dauerhaft festhält;
10. der bestehende Not-Aus auch die neue Fensterautomatik dauerhaft deaktiviert.

## 3. Funktionsumfang

### 3.1 Globaler Standard

Die Einstellung **Fenster automatisch platzieren** ist der Hauptschalter der gesamten Fensterautomatik. Sie ist bei neuen Konfigurationen und bei der Migration der bestehenden Konfiguration aktiviert, kann aber jederzeit ausgeschaltet werden. Im ausgeschalteten Zustand werden weder gelernte Platzierungen noch feste Regeln angewendet oder aktualisiert; vorhandene Daten bleiben für eine spätere Reaktivierung erhalten.

Die Fensterplatzierung arbeitet unabhängig davon, ob die manuelle Snap-Funktion aktiviert ist. Dadurch kann die Anwendung gelernte Fensterpositionen wiederherstellen, ohne dass beim manuellen Ziehen das Zonenoverlay verwendet werden muss.

### 3.2 Geeignete Fenster

Standardmässig werden nur sichtbare, positionierbare Top-Level-Fenster verwaltet. Nicht verwaltet werden:

- Kindfenster;
- unsichtbare oder durch DWM verdeckte Fenster;
- reine Werkzeug-, Overlay- und Hinweisfenster;
- Desktop, Taskleiste, Shell-Flächen und andere bekannte Systemoberflächen ohne normales Anwendungsfenster;
- minimierte Hilfsfenster;
- Fenster mit höherer Prozessintegrität, solange Sascha Window Zones nicht gleich hoch ausgeführt wird;
- eigene Overlay-, Toast- und Hilfsfenster der Anwendung.

Ein normaler Top-Level-Dialog kann verwaltet werden. Er erhält durch seine Fensterklasse und Fensterart einen vom Hauptfenster getrennten Eintrag.

### 3.3 Einmalige Wirkung

Eine Wiederherstellung wird pro Lebensdauer eines Fensterhandles höchstens einmal erfolgreich angewendet. Danach darf der Benutzer das Fenster frei verschieben, skalieren, maximieren oder auf einen anderen Monitor ziehen. Diese Änderungen bilden die Grundlage für das nächste Öffnen.

Eine feste Regel legt ebenfalls nur die Startplatzierung fest. Sie erzwingt die Zone nicht fortlaufend.

## 4. Fensteridentität

### 4.1 Anwendungsschlüssel

Für klassische Desktopprogramme ist der kanonische vollständige Prozesspfad der Anwendungsschlüssel. Für paketierte, WinUI- oder gehostete Anwendungen wird eine dokumentiert lesbare AppUserModelId bevorzugt; nur wenn sie fehlt, wird auf den Prozesspfad zurückgefallen. Dadurch werden gehostete Anwendungen wie Windows-Einstellungen nicht pauschal dem gemeinsamen Hostprozess zugeordnet.

Es werden keine Befehlszeilen, Dokumentpfade oder Fensterinhalte gespeichert.

### 4.2 Fenstertypschlüssel

Der Fenstertypschlüssel besteht aus:

- Anwendungsschlüssel;
- Fensterklasse;
- Fensterart `Hauptfenster` oder `Dialog`.

Der wechselnde Fenstertitel ist standardmässig kein Bestandteil des Schlüssels. Dadurch teilen beispielsweise Excel-Arbeitsmappen dieselbe Hauptfensterplatzierung. Ein optionales Titelmuster steht nur in erweiterten festen Regeln zur Verfügung.

## 5. Platzierungsdaten

Ein gelernter Eintrag enthält mindestens:

```text
WindowPlacementEntry
├─ ApplicationKey
├─ WindowClass
├─ WindowKind
├─ MonitorStableId
├─ ZoneId                  optional
├─ SourceWorkArea
├─ NormalBoundsPixels
├─ NormalBoundsNormalized
├─ WasMaximized
└─ LastUpdatedUtc
```

`NormalBoundsPixels` bezeichnet die nicht maximierte Windows-Normalposition. Bei einem maximierten Fenster wird deshalb nicht das bildschirmfüllende Rechteck gespeichert, sondern die von Windows geführte Normalposition zusammen mit `WasMaximized = true`.

Die normalisierte Geometrie dient nur als Rückfallwert bei einer geänderten Arbeitsfläche. Auf derselben unveränderten Arbeitsfläche werden die exakten Pixelwerte wiederverwendet.

### 5.1 Zonenzuordnung

Wird ein Fenster durch Sascha Window Zones eingerastet, wird die betreffende Zonen-ID unmittelbar übernommen. Nach einer freien manuellen Bewegung ordnet der `PlacementClassifier` das Fenster der Zone mit der grössten sinnvollen Überdeckung zu; ohne eindeutige Überdeckung bleibt `ZoneId` leer.

Die gelernte Zonen-ID ist ein Anker und keine feste Regel. Auf unveränderter Monitorgeometrie gewinnt die exakt gespeicherte Position und Grösse. Nach einer Layout-, Auflösungs- oder DPI-Änderung hilft die Zone, das Fenster auf demselben logischen Bereich zu halten.

## 6. Regeln und Priorität

Eine optionale `WindowPlacementRule` enthält:

```text
WindowPlacementRule
├─ Id
├─ IsEnabled
├─ ApplicationKey
├─ WindowClass             optional
├─ WindowKind              optional
├─ TitlePattern            optional, erweitert
├─ Action                  RememberLast | FixedZone | Exclude
├─ ProfileId               nur FixedZone
├─ MonitorStableId         nur FixedZone
└─ ZoneId                  nur FixedZone
```

Beim Öffnen gilt folgende feste Priorität:

1. Not-Aus oder global deaktivierte Wiederherstellung: keine Aktion;
2. passende Ausschlussregel: weder wiederherstellen noch lernen;
3. passende feste Zonenregel: aktuelle Geometrie der Zielzone verwenden;
4. passende `RememberLast`-Regel oder globaler Standard: letzten gelernten Eintrag verwenden;
5. kein gelernter Eintrag: Fenster unverändert lassen und ab dann beobachten.

Spezifischere Regeln gewinnen vor allgemeineren Regeln. Die Reihenfolge lautet Titelmuster, Fensterklasse und Fensterart, Anwendung allein. Gleich spezifische Konflikte werden in der Oberfläche als Fehler markiert und nicht stillschweigend nach Listenreihenfolge entschieden.

## 7. Architektur

### 7.1 Komponenten

- `IWindowLifecycleHook` empfängt die benötigten dokumentierten Windows-Ereignisse und übergibt sie auf den Anwendungskontext.
- `WindowInspector` liest Sichtbarkeit, Stile, Besitzer, aktuelle Normalposition, Zustand, Prozessintegrität und Monitor.
- `WindowIdentityResolver` bildet AppUserModelId oder Prozesspfad, Fensterklasse und Fensterart auf einen stabilen Fenstertypschlüssel ab.
- `PlacementClassifier` erkennt Monitor und optionale Zonenbeziehung und erzeugt speicherbare Platzierungsdaten.
- `PlacementRuleResolver` wählt deterministisch Ausschluss, feste Zone oder gelernte Platzierung.
- `PlacementRestorer` berechnet eine sichtbare Zielgeometrie und wendet sie ohne Aktivierung des Fensters an.
- `IWindowPlacementRepository` lädt und speichert die dynamischen Platzierungsdaten getrennt von den normalen Einstellungen.
- `WindowPlacementEngine` koordiniert Ereignisse, Deduplizierung, begrenzte Wiederholungen, Unterdrückung eigener Bewegungen und Speicherung.

Die bestehende `WindowDragCoordinator`-Logik bleibt für das manuelle Einrasten zuständig. Die neue Engine hängt nicht von der noch zu verbessernden Titelleisten-Erkennung des manuellen Drag-Ablaufs ab.

### 7.2 Beobachtete Ereignisse

Die Implementierung verwendet getrennte, eng begrenzte `SetWinEventHook`-Registrierungen für die tatsächlich benötigten Ereignisse, insbesondere Anzeigen oder Erstellen eines Fensters, Bewegungs- und Grössenende, stabile Positionsänderungen, Minimierungsende sowie Ausblenden oder Zerstören.

Hochfrequente Positionsereignisse werden pro Fenster zusammengefasst. Sie dürfen weder die UI blockieren noch bei normalem Ziehen fortlaufende Dateischreibvorgänge erzeugen.

## 8. Datenfluss

### 8.1 Öffnen und Wiederherstellen

1. Ein sichtbares Top-Level-Fenster wird gemeldet.
2. Die Engine dedupliziert das Fensterhandle und prüft Eignung sowie Berechtigung.
3. Identität und Regel werden ermittelt.
4. Die Engine wartet kurz, bis das Fenster positionierbar und seine Normalposition stabil lesbar ist.
5. Ausschluss, feste Zone oder gelernte Platzierung werden nach der definierten Priorität gewählt.
6. Die Zielgeometrie wird auf eine aktuelle sichtbare Monitorarbeitsfläche abgebildet.
7. Die Normalposition wird ohne Aktivierung gesetzt; danach wird bei Bedarf maximiert.
8. Das Handle wird als einmalig verarbeitet markiert.

Wenn das Fenster noch nicht bereit ist, erfolgen höchstens drei Versuche mit kurzen, steigenden Verzögerungen innerhalb eines begrenzten Zeitfensters. Nach dem dritten Fehlschlag bleibt das Fenster unverändert.

### 8.2 Lernen

Nach einer stabilen manuellen Bewegung, Grössenänderung oder Zustandsänderung aktualisiert die Engine einen Arbeitsspeicher-Cache. Bei Ausblenden oder Zerstören wird der letzte gültige Cachezustand verwendet, weil das Fensterhandle dann bereits nicht mehr lesbar sein kann.

Eigene Bewegungen des `PlacementRestorer` werden durch einen kurzlebigen Unterdrückungseintrag gekennzeichnet. Sie gelten nicht als neue manuelle Platzierung. Eine spätere Benutzeränderung wird wieder normal gelernt.

Minimierte Rechtecke werden nie als Ziel gespeichert. Bei Maximierung bleiben Normalposition und maximierter Zustand erhalten.

## 9. Monitor-, Zonen- und DPI-Änderungen

Die Wiederherstellung verwendet folgende Reihenfolge:

1. gleicher stabiler Monitor und unveränderte Arbeitsfläche: exakte Pixelposition;
2. gleicher Monitor mit geänderter Arbeitsfläche: normalisierte Geometrie, durch die gespeicherte Zone logisch verankert;
3. Monitor fehlt, Zielzone existiert auf einem anderen passenden Monitor des aktiven Profils: dorthin abbilden;
4. kein passendes Ziel: auf die primäre oder aktuell aktive Arbeitsfläche abbilden.

Jedes berechnete Rechteck wird auf eine sichtbare Arbeitsfläche begrenzt. Mindestens Titelleiste und ein sinnvoller Arbeitsbereich des Fensters müssen erreichbar bleiben. Eine feste Zonenregel fällt bei fehlender Zielzone nicht stillschweigend auf eine andere Zone zurück, sondern wird pausiert und sichtbar als unvollständig markiert.

## 10. Speicherung

### 10.1 Trennung der Daten

Benutzerdefinierte Regeln und die globale Aktivierung bleiben in `settings.json`. Häufig veränderte gelernte Zustände liegen in einer eigenen Datei `placements.json`. Dadurch verursachen normale Fensterbewegungen keine vollständige Neuschreibung von Profilen und Layouts.

Die Ablage folgt dem bestehenden Betriebsmodus:

- normal: `%APPDATA%\SnapZones\placements.json`;
- portabel: `Data\placements.json` neben der Anwendung gemäss bestehendem Portable-Konzept.

### 10.2 Schreibsicherheit

Änderungen werden zeitverzögert gebündelt und atomar über eine temporäre Datei ersetzt. Die letzte gültige Fassung wird als Sicherung aufbewahrt. Eine beschädigte Datei wird mit Zeitstempel umbenannt; die Anwendung startet mit leerem Platzierungsspeicher und meldet den Vorgang in Status und Diagnose.

Der Speicher ist auf 500 zuletzt verwendete Fenstertypen begrenzt. Ältere Einträge werden nach `LastUpdatedUtc` entfernt. Fenster- und Dokumenttitel werden nicht persistiert.

## 11. Oberfläche

Die Hauptnavigation erhält die Seite **Fensterplatzierung**. Sie vereint globalen Standard, gelernte Einträge und optionale Regeln.

```text
Fensterplatzierung                         [Automatik aktiv]
[ Fenster auswählen ]

Windows-Einstellungen · Hauptfenster
Letzte Platzierung: Monitor 1 · Zone Rechts · maximiert
[Jetzt anwenden] [Feste Zone] [Nicht verwalten] [Vergessen]
```

Die Liste zeigt:

- verständlichen Programmnamen und Fenstertyp;
- letzte Zone oder freie Platzierung;
- Monitor, Grösse und maximierten Zustand;
- Zeitpunkt der letzten Aktualisierung;
- Regelstatus und allfällige Fehler.

**Fenster auswählen** lässt den Benutzer ein aktuell geöffnetes Fenster bestimmen und übernimmt die stabile technische Identität. Die technischen Werte bleiben unter **Erweitert** sichtbar und kopierbar.

Pro Eintrag stehen folgende Aktionen zur Verfügung:

- **Jetzt anwenden** verschiebt das aktuell passende Fenster einmalig auf seine gespeicherte Platzierung;
- **Feste Zone** erzeugt oder bearbeitet eine Regel mit Profil, Monitor und Zone;
- **Nicht verwalten** erzeugt eine Ausschlussregel;
- **Vergessen** entfernt nur den gelernten Zustand, nicht eine vorhandene Regel.

Alle Änderungen werden automatisch gespeichert. Konflikte oder fehlende Zielzonen sind textlich sichtbar und nicht nur farblich markiert.

## 12. Sicherheit und Fehlerverhalten

- Die Engine startet keine Programme und sendet keine Tastatureingaben.
- Sie verwendet keine Code-Injection, keinen Treiber, keinen Dienst und keine undokumentierten Windows-Strukturen.
- Fenster werden ohne Fokuswechsel positioniert.
- Ein Fenster wird vor jedem Setzen nochmals auf Existenz, Identität und Berechtigung geprüft.
- Pro Öffnung gibt es höchstens drei begrenzte Versuche und danach keine Endlosschleife.
- Fehler eines einzelnen Fensters deaktivieren nicht sofort die gesamte Anwendung; wiederholte Hook- oder Callback-Fehler lösen jedoch den vorhandenen Schutzschalter aus.
- `Ctrl + Alt + Shift + F12` deaktiviert Snap-Funktion und automatische Wiederherstellung, beendet laufende Platzierungen, entfernt Overlays und speichert beide Funktionen als ausgeschaltet.

## 13. Konfigurationsmigration

Die Hauptkonfiguration erhält eine neue Schema-Version. Version 1 wird ausdrücklich gelesen und verlustfrei auf die neue Version migriert; sie darf nicht als beschädigte Datei behandelt werden.

Neue Felder:

- `RestoreWindowPlacementEnabled`, Standard `true`;
- `WindowPlacementRules`, Standard leere Liste.

Eine fehlende `placements.json` bedeutet lediglich, dass noch keine Fensterplatzierung gelernt wurde. Bestehende Profile, Layouts, Hotkeys, Themes und Sicherheitseinstellungen bleiben unverändert.

## 14. Tests

### 14.1 Automatisierte Kernprüfungen

- stabile Schlüsselbildung für klassische, paketierte und gehostete Anwendungen;
- Trennung von Hauptfenster und Dialog sowie gemeinsamer Schlüssel für mehrere gleichartige Excel-Fenster;
- Regelpriorität und Erkennung gleich spezifischer Konflikte;
- einmalige Wiederherstellung trotz doppelter oder verspäteter Fensterereignisse;
- keine Wiederherstellung bei Ausschluss, globaler Deaktivierung oder Not-Aus;
- Speicherung der Normalposition und Wiederherstellung von `maximiert`, niemals `minimiert`;
- exakte Pixelwiederherstellung bei unveränderter Arbeitsfläche;
- proportionale, sichtbare Rückfallplatzierung bei DPI-, Auflösungs- und Monitoränderung;
- pausierte feste Regel bei fehlendem Monitor oder fehlender Zone;
- atomare, gebündelte Speicherung, Sicherung und Verhalten bei beschädigter `placements.json`;
- Unterdrückung selbst ausgelöster Positionsereignisse;
- höchstens drei Wiederholungen bei einem noch nicht bereiten Fenster;
- Schema-Migration ohne Verlust bestehender Einstellungen.

### 14.2 Windows-Integrationstests

Die Windows-Schicht wird mit kontrollierten Testfenstern für Normalposition, Maximierung, Dialoge, Ereignisreihenfolge und Mehrmonitor-Geometrie geprüft. Tests verändern keine fremden Benutzerfenster.

### 14.3 Reale Abnahme

Die abschliessende manuelle Prüfung umfasst mindestens:

1. Windows-Einstellungen normal öffnen, vergrössern, schliessen und erneut öffnen;
2. Windows-Einstellungen maximiert schliessen und maximiert wieder öffnen;
3. mehrere Excel-Hauptfenster und einen Excel-Dialog getrennt prüfen;
4. Explorer und Notepad frei positionieren, schliessen und erneut öffnen;
5. eine feste Zonenregel sowie eine Ausschlussregel anwenden;
6. Monitor trennen, Auflösung oder Skalierung ändern und sichtbaren Rückfall prüfen;
7. Not-Aus während einer anstehenden Wiederherstellung auslösen;
8. nachweisen, dass nach der Startplatzierung jede manuelle Bewegung frei möglich bleibt.

## 15. Leistung

Die Engine führt keine periodische Vollsuche über alle Prozesse durch. Sie arbeitet ereignisbasiert, bündelt hochfrequente Positionsänderungen pro Fenster und hält nur aktive Fenster sowie höchstens 500 gespeicherte Fenstertypen im Zustand.

Die Infobereichs- und Hauptfensterinitialisierung darf durch das Laden von `placements.json` nicht blockieren. Laden, Validieren und Schreiben des Platzierungsspeichers erfolgen ausserhalb des UI-Pfads; erst die konkrete Fensterpositionierung wird auf den Anwendungskontext übergeben.

## 16. Nicht Bestandteil dieser Ausbaustufe

- Starten kompletter Arbeitsbereiche oder Anwendungen;
- Wiederherstellung mehrerer individueller Plätze für gleichartige Fenster desselben Programms;
- virtuelle Desktop-Zuweisungen;
- gemeinsames Bewegen gekoppelter Fenster;
- Transparenz, Immer-im-Vordergrund, Prozesspriorität oder Tastaturmakros;
- fortlaufendes Erzwingen einer Zone;
- inhalts- oder dokumentbezogene Regeln anhand gespeicherter Fenstertitel.

Diese Funktionen können später auf der gemeinsamen Fensteridentität und Ereignisüberwachung aufbauen, ohne die Platzierungs-Engine zu ersetzen.

## 17. Fachliche Referenzen

- [Microsoft PowerToys FancyZones](https://learn.microsoft.com/en-us/windows/powertoys/fancyzones): letzte App-Zone, neue Fenster, Auflösungs- und Layoutänderungen.
- [Microsoft PowerToys Workspaces](https://learn.microsoft.com/en-us/windows/powertoys/workspaces): begrenztes Warten auf gestartete Fenster und sichtbarer Status.
- [DisplayFusion Triggers](https://www.displayfusion.com/HelpGuide/Triggers/?PDF=1): Prozess-, Fensterklassen- und Titelbedingungen sowie verzögerte Fensterereignisse.
- [MaxTo – Remember where windows belong](https://maxto.net/uk/documentation/how-to/remember-window-positions): gelernte Anwendungslage und optionale Unterscheidung nach Fenstertyp.
- [Actual Window Manager – Position](https://www.actualtools.com/windowmanager/help/userinterface/position.php): Speichern und Wiederherstellen von Positionen beim Schliessen und Öffnen.
