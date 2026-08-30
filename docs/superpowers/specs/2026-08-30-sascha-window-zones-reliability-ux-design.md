# Sascha Window Zones: Zuverlässigkeits- und UX-Ausbau

**Datum:** 30. August 2026  
**Status:** Zur Freigabe  
**Zielplattform:** Windows 11 x64  
**Technologie:** .NET 8, WPF, dokumentierte Win32-Schnittstellen

## 1. Zielbild

Sascha Window Zones bleibt eine schnelle, lokale Windows-Anwendung für frei definierbare Fensterzonen auf mehreren Monitoren. Der Ausbau behebt die bekannten Inkonsistenzen im Profil- und Layouteditor, macht jede gültige Änderung unmittelbar sichtbar und speichert sie automatisch ab. Die Verwaltung bleibt in einem Hauptfenster; für den Alltag kommen ein kompakter Modus und das bestehende Infobereichsmenü hinzu.

Der Ausbau gilt als abgeschlossen, wenn:

- der Profilname im Editor, in der Profilliste, im Kopfbereich und im Infobereich gleichzeitig aktualisiert wird;
- Zonen mit acht echten Griffen zuverlässig verschoben und skaliert werden können;
- Prozent-, Pixel-, Namens- und Regleränderungen ohne Anwenden-Schaltfläche im Vorschaubereich erscheinen;
- Vorlagen grafisch dargestellt und passend zum Seitenverhältnis des gewählten Monitors vorgeschlagen werden;
- jede gültige Änderung automatisch, atomar und wiederherstellbar gespeichert wird;
- die Hauptanwendung selbst in eine Zone gezogen werden kann, ohne dass Overlay- oder Hilfsfenster erfasst werden;
- portable Konfiguration, Import, Export, App-Regeln und Arbeitsbereiche vollständig integriert sind;
- die dunkle Oberfläche und das Overlay neutrale Windows-Grautöne statt dunkelblauer Flächen verwenden.

## 2. Sicherheitsgrenzen

Die Anwendung verwendet weiterhin keinen Treiber, keinen Windows-Dienst, keine Code-Injection, keine Explorer-Manipulation, keine undokumentierten DPI-Strukturen und keine privaten Registry-Werte. Sie fordert keine Administratorrechte an. Snap-Funktion und Autostart bleiben bei einer neuen Konfiguration ausgeschaltet; der bestehende Not-Aus `Ctrl+Alt+Shift+F12` bleibt erhalten.

Fenster werden nur über dokumentierte Windows-Ereignisse und Fensterfunktionen beobachtet und positioniert. Skalierung, Textgrösse und Taskleistengrösse werden nicht durch unsichere Umgehungen verändert. Nicht unterstützte Windows-Anzeigeoptionen bleiben als erklärende Verknüpfungen zu den zuständigen Windows-Einstellungsseiten umgesetzt.

## 3. Informationsarchitektur und Einfensterprinzip

Der Kopfbereich enthält links Produktname und Status, in der Mitte das aktive Profil und rechts den Speicherzustand. Die Hauptnavigation ordnet **Profile** vor **Layouts**, danach **App-Regeln**, **Arbeitsbereiche**, **Windows-Anzeige** und **Einstellungen**. Alle Verwaltungsseiten werden innerhalb desselben Hauptfensters gewechselt. Nur systemeigene Datei-Auswahldialoge für Import und Export dürfen ausserhalb des Hauptfensters erscheinen.

```text
┌ Sascha Window Zones ─────────────────────────────────────────────────────────┐
│ [Symbol] SASCHA WINDOW ZONES     Profil [Arbeit ▾]     ● Gespeichert   [—][□][×] │
├──────────────┬───────────────────────────────────────────────────────────────┤
│ Profile      │ Seitentitel                                      [Kompakt]   │
│ Layouts      │ ┌───────────────────────────────────────────────────────────┐ │
│ App-Regeln   │ │ Inhalt der gewählten Seite                               │ │
│ Arbeitsbereiche│ │                                                          │ │
│ Anzeige      │ │                                                           │ │
│ Einstellungen│ └───────────────────────────────────────────────────────────┘ │
├──────────────┴───────────────────────────────────────────────────────────────┤
│ Statusmeldung · letzte erfolgreiche Speicherung 14:32:08                    │
└──────────────────────────────────────────────────────────────────────────────┘
```

Der Speicherzustand kennt `Gespeichert`, `Speichert …`, `Ungültige Eingabe` und `Speicherfehler`. Der Zustand ist nicht nur farblich unterscheidbar, sondern besitzt immer Text und ein Symbol. Die bisherige primäre Schaltfläche **Speichern** entfällt, weil gültige Änderungen automatisch gespeichert werden. Import, Export, Diagnose und Beenden liegen in einem klar bezeichneten Menü, nicht in einem unbeschrifteten Drei-Punkte-Menü.

### 3.1 Kompakter Alltagsmodus

Der kompakte Modus ist ein zweiter Zustand desselben Hauptfensters, kein zusätzliches Werkzeugfenster. Er zeigt das aktive Profil, die Profile als Direktwahl, Snap Ein/Aus, den gewählten Monitor und die vier zuletzt verwendeten Layoutvorlagen. Ein Klick auf **Verwalten** stellt im selben Fenster die vollständige Oberfläche wieder her. Fensterposition und Modus werden gespeichert; beim Autostart startet die Anwendung standardmässig minimiert im Infobereich und baut den vollständigen Editor erst beim Öffnen auf.

```text
┌ Sascha Window Zones ───────────────────────────┐
│ Profil [Arbeit ▾]   Snap [Ein]   [Verwalten]   │
│ Monitor [PHL 498P9 ▾]                          │
│ [▌▌] [▌█▌] [▌▌▌] [▦]                          │
└────────────────────────────────────────────────┘
```

## 4. Zustandsmodell und unmittelbare Aktualisierung

### 4.1 Stabile Ansichtsmodelle

Die Oberfläche bindet nicht mehr direkt an austauschbare unveränderliche Profildatensätze. `ProfileItemViewModel`, `MonitorLayoutViewModel` und `ZoneEditorViewModel` besitzen stabile IDs, implementieren Änderungsbenachrichtigungen und bleiben während einer Bearbeitung dieselben Objektinstanzen. Der aktive Profilverweis wird ausschliesslich über die stabile Profil-ID aufgelöst.

Beim Umbenennen eines Profils läuft ein einzelner Datenfluss:

```text
Namensfeld
  → ProfileItemViewModel.Name
  → Profilliste + Kopf-Dropdown + Infobereich
  → validierter Profil-Snapshot
  → AutoSaveCoordinator
```

Ein Profilname wird nach jeder gültigen Eingabe sofort überall angezeigt. Führende und nachfolgende Leerzeichen werden beim Verlassen des Feldes entfernt; eine leere Eingabe, ein Duplikat ohne Beachtung der Gross-/Kleinschreibung oder mehr als 80 Zeichen wird sichtbar als ungültig markiert und nicht gespeichert. Während einer vorübergehend ungültigen Eingabe bleibt der letzte gültige Name im Kopfbereich und im Infobereich bestehen.

### 4.2 Live-Bearbeitung

Zonenname, Einheit, Position, Grösse, Ränder, Layoutabstand, Magnetdistanz, Overlayfarbe und Overlaydeckkraft werden unmittelbar an das Ansichtsmodell gebunden. Bei jedem gültigen Zahlenwert wird die kanonische normierte Geometrie neu berechnet und der Vorschaubereich im selben Dispatcher-Durchlauf aktualisiert. Eine Schaltfläche **Werte anwenden** existiert nicht mehr.

Zahlenfelder akzeptieren während der Eingabe vorübergehend unvollständige Texte wie `-` oder `12,`; diese verändern die Zone noch nicht. Sobald der Text als gültige Zahl gelesen werden kann, wird die Vorschau aktualisiert. Beim Verlassen eines ungültigen Feldes wird der letzte gültige Wert wiederhergestellt und eine kurze neutrale Fehlermeldung direkt am Feld angezeigt.

## 5. Layouteditor

### 5.1 Aufbau

```text
Layouts                                      [+ Neue Zone] [Rückgängig] [Wiederholen]
┌───────────────┬─────────────────────────────────────┬──────────────────────────┐
│ Monitore      │ Vorlagen für 5120 × 1382            │ Zone                     │
│               │ [▌█▌] [▌▌▌] [█▌▌] [▌▌▌▌] [▦]       │ Name [Browser________]   │
│ PHL 498P9     ├─────────────────────────────────────┤ Einheit  (● %) (○ px)    │
│ 5120 × 1382   │ ┌─────────────────────────────────┐ │ Links   [ 50,0 ] %       │
│               │ │┌──────────────┬─────────┬──────┐│ │ Oben    [  0,0 ] %       │
│ Dell U2723QE  │ ││     Haupt   ◇│ Browser │ Chat ││ │ Breite  [ 25,0 ] %       │
│ 3840 × 2160   │ ││              │         │      ││ │ Höhe    [100,0 ] %       │
│               │ │└──────────────┴─────────┴──────┘│ │ [Zone löschen]           │
│               │ └─────────────────────────────────┘ │                          │
└───────────────┴─────────────────────────────────────┴──────────────────────────┘
```

Die Monitorliste zeigt Anzeigename und Auflösung. Die Windows-Skalierung wird im Layoutbereich entfernt und nur noch auf der Seite **Windows-Anzeige** gezeigt.

### 5.2 Echte Skaliergriffe

Der Editor zeichnet Griffe nicht mehr nur als Pixelrechtecke auf einer benutzerdefinierten Zeichenfläche. Jede gewählte Zone erhält acht echte WPF-`Thumb`-Elemente für oben, unten, links, rechts und die vier Ecken. Die sichtbare Fläche eines Griffes beträgt 8 bis 10 Pixel, die unsichtbare Trefferfläche mindestens 20 × 20 geräteunabhängige Pixel. Jeder Griff besitzt den passenden Grössenänderungszeiger, fängt die Maus zuverlässig ab und verwendet `DragStarted`, `DragDelta` und `DragCompleted`.

Die zentrale Geometriefunktion erhält Ausgangsrechteck, aktiven Griff, Pixelbewegung, Monitor-Arbeitsfläche, Mindestgrösse und Magnetdistanz. Sie liefert ein validiertes normiertes Rechteck zurück. Während des Ziehens wird nur die betroffene Geometrie aktualisiert; die gesamte Editorseite wird nicht bei jeder Mausbewegung neu aufgebaut. Dadurch bleiben Mauserfassung und Griffinstanz erhalten. `Escape` stellt die Geometrie vom Beginn des Ziehvorgangs wieder her.

Die Zone kann zusätzlich per Tastatur bearbeitet werden: Pfeiltasten verschieben um 1 Pixel, `Shift` + Pfeiltaste um 10 Pixel, `Ctrl` + Pfeiltaste skaliert an der zuletzt aktiven Kante. Der Fokusrahmen bleibt sichtbar. Mindestgrösse, Monitorgrenzen, Magnetismus und Überlappungsvalidierung gelten für Maus und Tastatur identisch.

### 5.3 Prozent und Pixel

`ZoneDefinition.Bounds` bleibt die kanonische normierte Geometrie. Prozentwerte werden mit höchstens zwei Dezimalstellen angezeigt; Pixelwerte beziehen sich auf die aktuelle Monitor-Arbeitsfläche. Ein Einheitenwechsel verändert die Zone nicht, sondern nur Darstellung und Interpretation der Eingabefelder.

Die Eingabearten bleiben eindeutig:

- **Position und Grösse:** Links, Oben, Breite und Höhe sind editierbar; Rechts und Unten werden berechnet.
- **Aussenabstände:** Links, Oben, Rechts und Unten sind editierbar; Breite und Höhe werden berechnet.

Ändert der Benutzer Breite oder Höhe in Pixeln oder Prozenten, ändert sich das Rechteck sofort. Ändert er den Zonennamen, erscheint der neue Name sofort in der Zone. Alle Änderungen erzeugen einen Undo-Schritt, wobei fortlaufende Tastatureingaben und ein einzelner Ziehvorgang jeweils zu genau einem Schritt zusammengefasst werden.

### 5.4 Neue Zone und Magnetismus

**Neue Zone** bleibt oberhalb der Monitorvorschau sichtbar. Die neue Zone belegt die grösste freie achsenparallele Fläche; bei Gleichstand gewinnt oben, danach links. Ist keine konfliktfreie Fläche mit mindestens vier Prozent Breite und Höhe verfügbar, wird keine Zone erzeugt und die Statuszeile erklärt den Grund.

Beim Verschieben und Skalieren docken Kanten an Monitorränder und Zonenkanten an. Die Magnetdistanz ist von 0 bis 40 Pixel frei eingebbar; `0 px` deaktiviert die Funktion. `Alt` schaltet das Andocken während des Ziehens vorübergehend aus. Überlappungen werden während der Bearbeitung sichtbar markiert und nie als gültiger Zustand gespeichert.

## 6. Adaptive grafische Layoutvorlagen

Vorlagen sind normierte Zonensätze mit semantischen Rollen, nicht fest gerenderte Bilder. Die Vorschaukarten werden für das tatsächliche Seitenverhältnis des gewählten Monitors erzeugt. Jede Karte zeigt die resultierende Aufteilung grafisch; Name und kurze Eignung stehen darunter. Eine Vorlage ersetzt die Zonen des aktiven Monitors sofort, erzeugt aber einen einzelnen rückgängig machbaren Änderungsschritt.

Die Klassifizierung verwendet das Verhältnis `Arbeitsflächenbreite / Arbeitsflächenhöhe`:

| Klasse | Verhältnis | Bevorzugte Vorschläge |
|---|---:|---|
| Hochformat | unter 0,90 | zwei Reihen; Hauptbereich oben mit zwei Bereichen unten; drei Reihen |
| Klassisch | 0,90 bis unter 1,45 | zwei Spalten; Hauptbereich links 60 %; 2 × 2 Raster |
| Breitbild | 1,45 bis unter 2,40 | zwei Spalten; drei Spalten; Hauptbereich 50 % mit zwei Seitenbereichen |
| Ultrawide | 2,40 bis unter 3,20 | drei Spalten; Hauptbereich 50 % mittig; vier Spalten; 25/50/25 |
| Super-Ultrawide | ab 3,20 | vier Spalten; Hauptbereich 40 % mittig mit Seitenpaaren; fünf Spalten; zwei 16:9-Arbeitsgruppen |

Die Kartengrösse bildet das Monitorverhältnis innerhalb einer begrenzten Vorschaufläche ab, damit Hoch- und Querformat sichtbar unterscheidbar bleiben. Nicht passende Vorschläge verschwinden nicht vollständig, sondern stehen unter **Weitere Vorlagen**. Zuletzt verwendete Vorlagen stehen zuerst, ohne die fachliche Eignung der Hauptvorschläge zu verändern.

## 7. Regler, Zahlenwerte und Color Picker

Jeder Schieberegler zeigt rechts ein synchronisiertes Zahlenfeld mit Einheit. Das Zahlenfeld ist direkt editierbar und verwendet denselben Wertebereich wie der Regler. Tastatur, Mausrad und Regler aktualisieren Zahlenwert und Vorschau ohne Verzögerung.

```text
Overlaydeckkraft   [────────●──────────] [ 18 ] %
Zonenabstand       [────●──────────────] [  8 ] px
Magnetdistanz      [──────●────────────] [ 12 ] px
```

Die Overlayfarbe erhält einen vollständig eingebetteten Color Picker auf der Einstellungsseite. Er besteht aus Farbtonleiste, Sättigungs-/Helligkeitsfläche, Hex-Feld, aktueller Vorschau, sechs neutralen Grauvorlagen und den zuletzt verwendeten Farben. Alpha wird nicht in den Hexwert gemischt, sondern ausschliesslich über die Deckkraft gesteuert.

```text
Overlayfarbe
┌────────────────────────┐  Farbton [────────────●]
│ Sättigung / Helligkeit │  Hex     [#8A8A8A]
│          ○             │  Vorschau [████████]
└────────────────────────┘  Grau     [■][■][■][■][■][■]
```

Das Hex-Feld akzeptiert `#RRGGBB` und `RRGGBB`, normalisiert beim Verlassen auf Grossbuchstaben und zeigt ungültige Eingaben direkt an. Die Vorschau reagiert während der Auswahl live; gespeichert wird nur eine gültige Farbe. Standard ist ein neutrales Grau `#8A8A8A` mit 18 Prozent Deckkraft. Bei einer bestehenden Konfiguration werden nur die exakt bisherigen Standardwerte `#2F6FED` und 24 Prozent auf den neuen Standard migriert; individuell geänderte Werte bleiben unverändert.

## 8. Neutrales Windows-Theme und Barrierefreiheit

Das Theme kennt weiterhin `Windows-System`, `Hell` und `Dunkel`. Dunkle Flächen verwenden neutrale Grautöne ohne wahrnehmbaren Blauanteil. Der Windows-Akzent wird nur für Auswahl, Fokus, aktives Snap-Ziel und primäre Aktionen verwendet. Overlayflächen verwenden dieselbe neutrale Farblogik, bleiben aber frei wählbar.

Alle Text- und Bedienelementkombinationen erreichen mindestens WCAG-AA-Kontrast. Zustände werden nie nur über Farbe vermittelt. Die Anwendung unterstützt 100 bis 250 Prozent DPI, Tastaturnavigation, sichtbare Fokusrahmen, Screenreader-Namen und mindestens 20 × 20 Pixel grosse interaktive Trefferflächen. Lange Hilfetexte umbrechen, ohne Eingaben oder Schaltflächen zu verdecken.

## 9. Automatische Speicherung, Wiederherstellung und Undo

### 9.1 AutoSaveCoordinator

Jede gültige Änderung erzeugt eine monoton steigende Konfigurationsrevision. Der `AutoSaveCoordinator` wartet 400 Millisekunden nach der letzten Änderung, erstellt dann einen unveränderlichen vollständigen Snapshot und übergibt ihn an genau einen seriellen Schreiber. Trifft während eines Schreibvorgangs eine neuere Revision ein, wird danach nur der neueste Snapshot geschrieben; eine ältere Revision darf niemals eine neuere Datei überschreiben.

Der Dateivorgang läuft im selben Verzeichnis:

1. Snapshot vollständig validieren und als JSON serialisieren.
2. In eine eindeutige temporäre Datei schreiben und mit `Flush(true)` auf den Datenträger übertragen.
3. Die aktuelle gültige Datei als rotierende Sicherung erhalten.
4. Temporäre Datei atomar an die Stelle der Konfiguration setzen.
5. Datei erneut laden, Schema und Revision prüfen und erst danach `Gespeichert` anzeigen.

Es werden fünf rotierende gültige Sicherungen aufbewahrt. Beim Start werden Hauptdatei, temporäre Reste und Sicherungen nach Revision und Gültigkeit geprüft; die neueste vollständig gültige Revision gewinnt. Eine wiederhergestellte Datei wird nicht stillschweigend verwendet: Die Statuszeile meldet Quelle und Zeitpunkt, und ein Diagnoseeintrag hält den Grund fest.

Beim Schliessen wird eine ausstehende gültige Revision mit einem begrenzten synchronen Abschluss geschrieben. Schlägt das Speichern fehl, bleibt das Hauptfenster offen, zeigt einen auswählbaren und kopierbaren Fehler und bietet **Erneut versuchen** sowie **Exportieren** an. Ungültige Eingaben verändern keinen persistierten Snapshot.

### 9.2 Undo und Redo

Die letzten 30 Benutzeraktionen bleiben pro Programmsitzung rückgängig machbar. Ein Ziehvorgang, eine Vorlagenanwendung oder eine zusammenhängende Texteingabe zählt jeweils als eine Aktion. Undo und Redo erzeugen normale neue Revisionen und werden damit ebenfalls automatisch gespeichert. Der Verlauf selbst muss einen Neustart nicht überstehen.

### 9.3 Import und Export

Export erzeugt eine UTF-8-JSON-Datei mit Schema-Version, Erstellzeit, Produktversion, Profilen, Layouts, App-Regeln, Arbeitsbereichen und Einstellungen. Geheimnisse oder personenbezogene Fensterinhalte werden nicht gespeichert. Vor einem Import wird die Datei vollständig in einen getrennten Snapshot geladen und validiert. Die Oberfläche zeigt Anzahl Profile, Monitore, Regeln und Arbeitsbereiche sowie Konflikte; **Importieren** ersetzt erst danach den aktiven Datenbestand und erzeugt unmittelbar eine Sicherung des bisherigen Zustands.

Import unterstützt die aktuelle und alle bisher ausgelieferten Schema-Versionen über explizite Migrationen. Unbekannte neuere Hauptversionen werden abgelehnt statt teilweise übernommen. IDs werden bei Kollisionen eindeutig neu erzeugt, Verknüpfungen werden entsprechend angepasst.

## 10. Portabler Betrieb und Ablage

Die Anwendung bleibt installerfrei und selbständig ausführbar. Liegt `portable.flag` neben `SaschaWindowZones.exe`, verwendet sie ausschliesslich folgende Pfade relativ zur EXE:

```text
App/
├── SaschaWindowZones.exe
├── portable.flag
└── Laufzeitdateien
Data/
├── settings.json
├── backups/
└── logs/
```

Ohne `portable.flag` bleiben die bisherigen Benutzerpfade als Kompatibilitätsmodus erhalten. Im portablen Modus prüft die Anwendung beim Start Schreibbarkeit und zeigt bei einem schreibgeschützten Datenträger einen klaren Fehler statt in einen anderen Ordner auszuweichen. Autostart speichert den vollständig aufgelösten aktuellen EXE-Pfad; nach einem Verschieben erkennt die Anwendung einen veralteten eigenen Eintrag und bietet dessen sichere Aktualisierung an.

Nach abgeschlossener Implementierung, Tests und Veröffentlichung wird das vollständige Repository inklusive `.git`, Quellcode, Tests, Dokumentation, veröffentlichter Anwendung und portabler Datenstruktur nach folgendem zuvor geprüften Ziel verschoben:

```text
T:\PortableApps\SaschaWindowZones
```

Der Quellpfad und der exakte Zielpfad werden unmittelbar vor dem Verschieben erneut aufgelöst. Das Ziel darf nicht bereits existieren. Nach dem Verschieben werden Git-Status, Testlauf, veröffentlichte EXE, `portable.flag` und Schreibtest im `Data`-Ordner am neuen Ort geprüft.

## 11. Eigene Anwendung als Snap-Kandidat

Der Windows-Ereignishook darf Ereignisse aus dem eigenen Prozess empfangen, aber die Kandidatenprüfung verwendet eine ausdrückliche Positivliste. Nur das Handle des sichtbaren Hauptfensters darf als eigenes Fenster eingerastet werden. Overlayfenster, Hinweisfenster, Color-Picker-Bestandteile, Infobereichsobjekte und unsichtbare Hilfsfenster bleiben ausgeschlossen. Während das Hauptfenster gezogen wird, dürfen seine Overlays keine erneute Snap-Sitzung auslösen.

Die bestehende Filterung fremder ungeeigneter Fenster bleibt bestehen: unsichtbare Fenster, Kindfenster, Toolfenster, minimierte Hilfsfenster, nicht positionierbare Systemfenster und Fenster mit höherer Prozessintegrität werden nicht verändert.

## 12. App-Regeln und Arbeitsbereiche

### 12.1 App-Regeln

Eine Regel kann Prozesspfad, optional Fenstertitelmuster und optional Fensterklasse mit Profil, Monitor und Zone verbinden. Der Prozesspfad ist das primäre stabile Merkmal; Titel- und Klassenmuster schränken nur weiter ein. Regeln können auf `Fenster erstellt`, `Fenster fokussiert` oder `Profil aktiviert` reagieren und besitzen Priorität, Aktivstatus, Verzögerung und maximal drei begrenzte Wiederholungen.

```text
App-Regeln                                              [+ Regel]
Chrome.exe   Titel enthält "YouTube"   → Freizeit / PHL 498P9 / Video
Code.exe                               → Arbeit / Dell U2723QE / Haupt
```

Vor einer automatischen Verschiebung werden Fensterhandle, Prozess und Ziel erneut geprüft. Eine Regel wird pausiert, wenn Monitor oder Zone fehlt; sie fällt nicht stillschweigend auf eine andere Zone zurück. Die Anwendung startet über Regeln keine Programme.

### 12.2 Arbeitsbereiche

Ein Arbeitsbereich speichert eine Gruppe von Anwendungen mit optionalem ausführbarem Pfad, Startargumenten, Arbeitsverzeichnis, Profil, Monitor und Zone. **Arbeitsbereich erfassen** liest nur Metadaten der aktuell sichtbaren geeigneten Fenster. **Starten** aktiviert zuerst das Profil, startet fehlende Anwendungen ohne erhöhte Rechte und positioniert vorhandene oder neu erkannte Hauptfenster anschliessend mit begrenzter Wartezeit.

Der Startstatus zeigt pro Anwendung `Wartet`, `Gestartet`, `Positioniert`, `Bereits geöffnet` oder `Fehler`. Ein fehlender Pfad oder ein nicht auftauchendes Hauptfenster stoppt nicht die übrigen Anwendungen. Es werden keine Fensterinhalte, Dokumente, Passwörter oder privaten Befehlszeilen anderer Prozesse erfasst.

## 13. Startverhalten und Leistung

Konfiguration und aktiver Profilname werden vor langsamen Monitor- oder Diagnoseschritten geladen und sofort angezeigt. Monitorerkennung, Update der Infobereichseinträge und optionale Diagnoseprüfungen laufen danach asynchron. Der Editor wird erst aufgebaut, wenn das Hauptfenster sichtbar ist. Ein zweiter Programmstart aktiviert die bestehende Instanz.

Zielwerte auf einem üblichen Windows-11-System:

- Infobereich und aktives Profil innerhalb von 500 Millisekunden nach Prozessstart verfügbar;
- Hauptfenster innerhalb von einer Sekunde bedienbar;
- Overlayreaktion innerhalb von 100 Millisekunden nach gültigem Ziehbeginn;
- Editor während Ziehen und Skalieren mit mindestens 60 Aktualisierungen pro Sekunde ohne vollständigen Seitenneuaufbau;
- keine Dateischreiboperation und keine Monitorabfrage im nativen Hook-Callback.

## 14. Schema und Migration

Das Konfigurationsschema erhält eine neue Haupt-/Nebenversion. Migrationen sind reine, separat getestete Funktionen und verändern niemals die Quelldatei, bevor der neue Snapshot vollständig validiert und gesichert wurde. Bestehende Profil-, Monitor- und Zonen-IDs bleiben erhalten. Neue Felder erhalten sichere Standardwerte; individuelle Theme-, Farbe-, Deckkraft-, Aktivierungs- und Autostartwerte bleiben erhalten.

Die alte manuelle Speicherlogik wird nach erfolgreicher Migration nicht parallel weitergeführt. Alle Änderungen laufen durch denselben Revisions- und Speicherkoordinator. Direkte Schreibzugriffe aus Ansichtsmodellen, Tray-Menü oder Windows-Diensten sind nicht zulässig.

## 15. Teststrategie und Abnahmekriterien

### 15.1 Automatisierte Kernprüfungen

- Profilumbenennung aktualisiert Liste, Kopfbereich und Tray bei erhaltener aktiver ID.
- Prozent-, Pixel- und Namenseingaben aktualisieren die Zone sofort; ungültige Zwischenstände werden nicht gespeichert.
- Alle acht Griffe verändern die erwarteten Kanten, behalten Mausfang und respektieren Mindestgrösse, Monitorgrenzen, Magnetismus und `Alt`.
- Einheitenwechsel und Prozent-/Pixel-Roundtrips bleiben innerhalb einer Pixel-Rundung stabil.
- Adaptive Vorlagen liefern je Seitenverhältnisklasse die erwartete Reihenfolge und konfliktfreie normierte Zonen.
- Regler, Zahlenfelder und Einheiten bleiben bidirektional synchron; Grenzwerte werden korrekt begrenzt.
- Color Picker, Hex-Feld, Grauvorlagen und Vorschau liefern denselben `#RRGGBB`-Wert.
- Hauptfenster wird als eigener Kandidat akzeptiert; jedes andere eigene Fenster wird abgelehnt.
- AutoSave-Revisionsrennen können keinen älteren Snapshot über einen neueren schreiben.
- Atomarer Schreibfehler, beschädigte Hauptdatei, temporäre Datei und fünf Sicherungen führen zur erwarteten Wiederherstellung.
- Import prüft Schema und Konflikte vor Mutation; Export lässt sich verlustfrei wieder importieren.
- Portabler und installierter Pfadmodus verwenden ausschliesslich ihre vorgesehenen Verzeichnisse.
- Regeln und Arbeitsbereiche behandeln fehlende Monitore, Zonen, Prozesse und Zeitüberschreitungen deterministisch.

### 15.2 Oberflächen- und Regressionstests

Die bestehenden Theme-Bildtests laufen für alle Seiten in Hell und Dunkel weiter. Ergänzt werden DPI-Stufen 100, 125, 150, 200 und 250 Prozent, Mindestfenstergrösse, Tastaturnavigation, Fokuskontrast, Textskalierung und deaktivierte Zustände. Der Layouteditor erhält automatisierte Drag-Delta-Tests mit allen Griffen und repräsentativen Monitorformaten Hochformat, 4:3, 16:9, 21:9 und 32:9.

### 15.3 Sichere technische Freigabe

Vor Veröffentlichung müssen Restore, vollständige Tests, Release-Build und self-contained `win-x64`-Publish ohne Warnungen oder Fehler erfolgreich sein. Die sichere Diagnose muss bestätigen, dass kein Hook registriert und keine Windows-Einstellung verändert wurde. Erst danach wird der portabel veröffentlichte Ordner erzeugt und das vollständige Repository an das vorgesehene Ziel verschoben.

Die abschliessende manuelle Prüfung durch den Benutzer umfasst reale Fensterbewegungen, Overlaywirkung, Farbwahl, Mehrmonitorbetrieb, Profilwechsel, App-Regeln, Arbeitsbereiche und Startverhalten. Automatisierte Prüfungen ersetzen diese reale Bedienprüfung nicht.

## 16. Umsetzungsreihenfolge

1. Stabile Ansichtsmodelle, Live-Bindings, echte Griffe und getestete Geometriefunktionen.
2. Revisionsbasiertes Autospeichern, Sicherungen, Wiederherstellung, Undo/Redo sowie Import/Export.
3. Adaptive grafische Vorlagen, neutrale Theme-Tokens, Wertfelder an Reglern und eingebetteter Color Picker.
4. Eigene Hauptfensterfreigabe, kompakter Modus, App-Regeln und Arbeitsbereiche.
5. Portabler Pfadmodus, vollständige Regression, Publish und geprüfter Umzug nach `T:\PortableApps\SaschaWindowZones`.

## 17. Fachliche Referenzen

Die adaptive Vorlagenvorschau und der Griffeditor orientieren sich an den dokumentierten Bedienmustern von [Microsoft PowerToys FancyZones](https://learn.microsoft.com/en-us/windows/powertoys/fancyzones). Arbeitsbereiche übernehmen das sichere Muster «Programme starten, Hauptfenster erkennen, danach positionieren» aus [PowerToys Workspaces](https://learn.microsoft.com/en-us/windows/powertoys/workspaces). Prozess-, Titel- und Fensterklassenfilter für Regeln entsprechen den öffentlich beschriebenen Möglichkeiten von [DisplayFusion Triggers](https://www.displayfusion.com/features/triggers/). Magnetismus und gemeinsames Skalieren angrenzender Fenster berücksichtigen die Interaktionsmuster von [AquaSnap](https://www.nurgo-software.com/products/aquasnap). Aufbau, sofort sichtbare Einstellungswirkung und Theme-Auswahl folgen den [Windows-Richtlinien für App-Einstellungen](https://learn.microsoft.com/en-us/windows/apps/design/app-settings/guidelines-for-app-settings) und den [Windows-App-Empfehlungen für Skalierung und Eingabe](https://learn.microsoft.com/en-us/windows/apps/get-started/best-practices).
