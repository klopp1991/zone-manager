# SnapZones: Designspezifikation

**Datum:** 30. August 2026  
**Status:** Zur Freigabe  
**Zielplattform:** Windows 11 x64  
**Technologie:** .NET 8, WPF, Win32

## 1. Ziel und Erfolgskriterien

SnapZones ist ein lokaler Windows-Fenstermanager für frei definierbare Snap-Bereiche auf einem oder mehreren Monitoren. Sobald eine Benutzerin oder ein Benutzer ein geeignetes Fenster an der Titelleiste zu ziehen beginnt, zeigt die Anwendung ohne zusätzliche Klicks die konfigurierten Bereiche als nicht interaktive Overlays an. Beim Loslassen über einem Bereich wird das Fenster wiederhergestellt und so positioniert, dass es den nutzbaren Bereich der Zone ausfüllt. Das Fenster wird dabei nicht in den nativen Maximiert-Zustand versetzt, weil Windows maximierte Fenster immer auf die gesamte Arbeitsfläche eines Monitors ausdehnt.

Der erste Prototyp gilt als erfolgreich, wenn folgende Abläufe funktionieren:

1. Die Anwendung startet als einzelne Instanz und bleibt über das Infosymbol im Infobereich erreichbar.
2. Im Editor lassen sich pro erkanntem Monitor Zonen frei erstellen, verschieben, skalieren und löschen.
3. Beim Ziehen eines normalen Desktopfensters erscheint das Overlay spätestens innerhalb von 100 ms; die Zone unter dem Mauszeiger wird eindeutig hervorgehoben.
4. Beim Loslassen füllt das Fenster die gewählte Zone unter Berücksichtigung von Arbeitsfläche, Rand und Abstand.
5. Mindestens zwei Profile lassen sich im Infobereich und per Tastenkürzel in weniger als einer Sekunde wechseln.
6. Einstellungen und Layouts bleiben nach einem Neustart erhalten; ab- und wieder angeschlossene Monitore werden möglichst demselben Monitorprofil zugeordnet.
7. Autostart lässt sich ohne Administratorrechte aktivieren und deaktivieren.
8. Beim ersten Start sind Snap-Engine und Autostart ausgeschaltet; vor einer bewussten Aktivierung läuft kein systemweiter Hook.

## 2. Produktumfang

### 2.1 Enthalten

- Hauptfenster mit den Bereichen **Layouts**, **Profile** und **Einstellungen**.
- Frei bearbeitbare rechteckige Zonen je Monitor mit prozentualen Koordinaten.
- Vorlagen für zwei Spalten, drei Spalten, Hauptbereich mit Seitenbereich und ein Raster.
- Profile mit Name, Monitorkonfiguration und optionalem Schnellwahlplatz 1 bis 9.
- Profilwechsel im Infobereich und über registrierte Tastenkürzel `Ctrl+Alt+1` bis `Ctrl+Alt+9`.
- Sofortiger Drag-Modus als Standard sowie optionaler Modus «nur mit Umschalttaste».
- Option, Overlays auf allen Monitoren oder nur auf dem Monitor unter dem Mauszeiger anzuzeigen.
- Einstellbare Aussenränder, Zonenabstände, Overlay-Farbe und Deckkraft.
- Temporäres Deaktivieren der Snap-Funktion im Infobereich.
- Sicherer Startmodus: Editor und Diagnose funktionieren ohne aktiven Fenster-Hook.
- Not-Aus über `Ctrl+Alt+Shift+F12`, der Hook und Overlays sofort deaktiviert.
- Schutzschalter, der die Snap-Sitzung bei Callback-Fehlern oder mehr als 100 Hook-Ereignissen in zehn Sekunden beendet.
- Autostart pro Benutzer über `HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Run`.
- Lokale, atomar geschriebene JSON-Konfiguration und lokale Diagnoseprotokolle.
- Per-Monitor-DPI-Unterstützung und negative Desktopkoordinaten.

### 2.2 Nicht enthalten

- Windows 10, ARM64 und 32-Bit-Systeme.
- Behandlung von Fenstern, die mit höheren Administratorrechten als SnapZones laufen.
- Anwendungsspezifische Fensterregeln, automatische Startpositionen und Fenstergruppen.
- Nicht rechteckige oder überlappende Zonen.
- Cloud-Synchronisation, Benutzerkonto, Telemetrie und automatische Updates.
- Virtuelle Desktops als eigene Layoutdimension.
- Installer im ersten Durchlauf; geliefert wird zunächst ein eigenständig ausführbarer x64-Publish-Ordner.

## 3. Architektur

Die Lösung wird in vier Projekte getrennt:

```text
SnapZones.sln
├── src/SnapZones.Core       Modelle, Geometrie, Validierung, Profile
├── src/SnapZones.Windows    Win32, Monitore, Hooks, Fensterpositionierung
├── src/SnapZones.App        WPF-Oberfläche, Tray, Editor, Komposition
└── tests/SnapZones.Tests    automatisierte Kern- und Integrationstests
```

`SnapZones.Core` hat keine WPF- oder Win32-Abhängigkeit. `SnapZones.Windows` kapselt alle nativen Aufrufe hinter kleinen Schnittstellen. `SnapZones.App` verbindet diese Dienste, enthält aber keine eigene Geometrie- oder Persistenzlogik. Dadurch können Zonenberechnung, Validierung, Profilwechsel und Monitorzuordnung ohne sichtbare Fenster getestet werden.

Die Anwendung verwendet keine externe MVVM-Bibliothek. Kleine eigene Basisklassen für `INotifyPropertyChanged` und Befehle vermeiden zusätzliche Laufzeitabhängigkeiten. Für das Infobereichssymbol wird `System.Windows.Forms.NotifyIcon` aus dem Windows-Desktop-Framework verwendet.

## 4. Datenmodell und Speicherung

### 4.1 Modelle

- `AppSettings`: aktive Profil-ID, Overlay-Modus, Aktivierungsmodus, Farbe, Deckkraft, Autostart und globaler Ein-/Aus-Zustand.
- `LayoutProfile`: stabile ID, Name, optionaler Schnellwahlplatz und eine Liste von Monitorlayouts.
- `MonitorLayout`: Monitor-Fingerabdruck, Anzeigename, gespeicherte Auflösung und Liste von Zonen.
- `ZoneDefinition`: stabile ID, Name sowie `X`, `Y`, `Width` und `Height` im normierten Bereich 0 bis 1.
- `MonitorDescriptor`: zur Laufzeit ermittelte Gerätekennung, Gerätepfad, Anzeigename, Arbeitsfläche, DPI und Primärstatus.

Zonen werden relativ zur jeweiligen Monitor-Arbeitsfläche gespeichert. Dadurch bleiben Layouts bei DPI-Wechseln und moderaten Auflösungsänderungen proportional erhalten. Die effektive Pixelposition entsteht erst beim Anzeigen oder Einrasten; Aussenrand und halber Abstand werden dabei konsistent eingerechnet und anschliessend auf die Arbeitsfläche begrenzt.

### 4.2 Monitorzuordnung

Die bevorzugte Identität stammt aus `QueryDisplayConfig` und `DisplayConfigGetDeviceInfo`: Ziel-Gerätepfad, Anzeigename und Adapter-/Ziel-ID. Falls Windows keine vollständigen Daten liefert, verwendet die Anwendung gestuft Gerätepfad, GDI-Gerätename, Auflösung/Position und zuletzt den Primärmonitor. Nicht zugeordnete gespeicherte Layouts bleiben erhalten und werden nicht überschrieben. Ein neuer Monitor erhält zunächst eine Zone über die gesamte Arbeitsfläche.

### 4.3 Dateien

- `%APPDATA%\\SnapZones\\settings.json`: Einstellungen und Profile.
- `%LOCALAPPDATA%\\SnapZones\\logs\\snapzones.log`: begrenztes lokales Diagnoseprotokoll.

Gespeichert wird zuerst in eine temporäre Datei im selben Verzeichnis; danach ersetzt ein atomarer Dateivorgang die bisherige Konfiguration. Vor dem Laden werden Version, IDs, Wertebereiche, Mindestgrössen und Überschneidungen validiert. Bei einer defekten Datei wird sie mit Zeitstempel gesichert, eine verständliche Fehlermeldung angezeigt und eine Standardkonfiguration geladen.

## 5. Fenstererkennung und Snap-Ablauf

### 5.1 Ereignisse

`SetWinEventHook` registriert erst nach bewusster Aktivierung ausserhalb fremder Prozesse die Ereignisse `EVENT_SYSTEM_MOVESIZESTART` und `EVENT_SYSTEM_MOVESIZEEND`. Beim Start werden unsichtbare Fenster, Kindfenster, eigene Fenster, Toolfenster sowie nicht positionierbare Systemfenster ausgeschlossen. Ein sicher begrenzter `WM_NCHITTEST`-Aufruf prüft, ob der Mauszeiger im verschiebbaren Titelbereich liegt; für Anwendungen mit eigener Titelleiste gibt es eine geometrische Fallback-Prüfung ausserhalb der Grössenänderungsränder.

Nach einem gültigen Drag-Start liest ein Dispatcher-Timer Mausposition, aktuellen Monitor und Zielzone. Der Timer aktualisiert nur Zustandsänderungen, damit keine unnötigen Neuzeichnungen stattfinden. `Escape` verwirft die aktuelle Zielzone. Beim Ende des Verschiebevorgangs werden zuerst alle Overlays verborgen und danach das Fenster mit `ShowWindow(SW_RESTORE)` und `SetWindowPos` in die zuletzt gültige Zone gesetzt. Schlägt dies wegen Prozessrechten oder eines verschwundenen Fensters fehl, bleibt das Fenster unverändert und der Fehler wird protokolliert. Jeder native Callback ist durch eine Ausnahmegrenze geschützt; bei einem Fehler oder mehr als 100 Hook-Ereignissen in zehn Sekunden werden Hook und Overlays automatisch deaktiviert.

### 5.2 Overlays

Pro angeschlossenem Monitor existiert höchstens ein wiederverwendetes WPF-Overlayfenster. Es ist rahmenlos, transparent, nicht aktivierbar, klickdurchlässig, im Alt-Tab-Menü unsichtbar und während eines Drags oberhalb normaler Fenster. Nicht aktive Zonen verwenden eine zurückhaltende Füllung und Kontur; die Zielzone erhält eine kräftigere Füllung und eine eindeutige Kontur. Die Zone zeigt optional ihren Namen, aber keine dekorativen Inhalte.

Im Modus «Aktiver Monitor» ist nur das Overlay des Monitors unter dem Mauszeiger sichtbar. Im Modus «Alle Monitore» sind alle konfigurierten Overlays sichtbar; nur die Zone unter dem Mauszeiger wird aktiv markiert. Display-, DPI- und Arbeitsflächenänderungen bauen den Overlaybestand neu auf.

## 6. Oberfläche und Bedienung

Die visuelle Richtung ist eine ruhige Windows-Arbeitsoberfläche mit hoher Informationsdichte und klarer Monitor-Metapher. Die Oberfläche nutzt Segoe UI Variable, helle oder dunkle Systemfarben, geringe Rundungen und einen einzigen blauen Akzent für Auswahl und Snap-Ziele. Das charakteristische Element ist die massstabsgetreue Monitorfläche: Zonen wirken darin wie tatsächlich greifbare Flächen und behalten beim Ziehen und Skalieren ihre Prozentwerte sichtbar bei.

```text
┌ SnapZones ──────────────────────────────────────────────────────┐
│ Layouts             Profil: Arbeit ▾          Speichern          │
├──────────────┬──────────────────────────────────────┬────────────┤
│ Monitore     │  ┌──── Monitor 1 · 3440 × 1440 ──┐ │ Zone       │
│ ● Monitor 1  │  │ ┌────────────┬───────┬───────┐ │ │ Name       │
│ ○ Monitor 2  │  │ │            │       │       │ │ │ X / Y      │
│              │  │ │   Haupt    │ Web   │ Chat  │ │ │ B / H      │
│ Vorlagen     │  │ │            │       │       │ │ │ Löschen    │
│ [2 Spalten]  │  │ └────────────┴───────┴───────┘ │ │            │
│ [3 Spalten]  │  └────────────────────────────────┘ │            │
├──────────────┴──────────────────────────────────────┴────────────┤
│ + Zone                         Abstand 8 px · Aussenrand 8 px    │
└─────────────────────────────────────────────────────────────────┘
```

Zonen lassen sich durch Ziehen verschieben und über acht Griffe skalieren. Eine Mindestgrösse verhindert unbedienbare Zonen. Werte können zusätzlich numerisch bearbeitet werden. Überlappungen werden während der Bearbeitung rot markiert und verhindern das Speichern. Änderungen bleiben als Entwurf im Editor, bis «Speichern» gewählt wird; «Zurücksetzen» stellt den zuletzt gespeicherten Stand wieder her.

Das Infobereichsmenü enthält den aktiven Profilnamen, die Profilliste, «Snap-Funktion aktiv», «Editor öffnen», «Mit Windows starten» und «Beenden». Ein Profilwechsel wird durch ein kurzes neutrales Desktop-Hinweisfenster bestätigt. Tastenkürzel werden beim Start und nach Änderungen neu registriert; Konflikte erscheinen neben dem betroffenen Schnellwahlplatz.

Beim ersten Start öffnet sich der Editor mit sichtbarem Hinweis «Snap-Funktion ist ausgeschaltet». Die Aktivierung erklärt knapp, dass danach Fenster-Verschiebeereignisse beobachtet werden. `Ctrl+Alt+Shift+F12` bleibt während einer aktiven Snap-Sitzung als Not-Aus registriert und ändert die gespeicherte Einstellung wieder auf ausgeschaltet.

## 7. Zustände, Fehler und Sicherheit

- Ein benannter System-Mutex verhindert mehrere gleichzeitig laufende Instanzen; ein zweiter Start aktiviert das bestehende Hauptfenster.
- Native Callback-Delegates bleiben für die Lebensdauer des Hooks referenziert und werden beim Beenden sicher abgemeldet.
- Alle Hook-Callbacks leiten Arbeit sofort an den UI-Dispatcher weiter und führen keine langsamen Datei- oder UI-Operationen im Callback aus.
- Fensterhandles werden vor jeder Positionierung erneut mit `IsWindow` geprüft.
- Externe Fenster erhalten weder Code-Injektion noch Prozesszugriff; verwendet werden ausschliesslich dokumentierte, prozessübergreifende Windows-Ereignisse und Fensterfunktionen.
- Die Anwendung fordert keine Administratorrechte an und verändert keine systemweiten Richtlinien.
- Die Anwendung enthält keinen Treiber, keinen Windows-Dienst, keine Code-Injection und keinen automatischen Neustart nach einem Schutzschalter.
- Autostart und Snap-Funktion sind in der Standardkonfiguration `false`.
- Unbekannte JSON-Felder werden toleriert, damit spätere Versionen abwärtskompatibel erweitert werden können.

## 8. Tests und Verifikation

### 8.1 Automatisiert

- Normierte Zonenvalidierung: Grenzen, Mindestgrösse, Überlappung und Rundungsfälle.
- Umrechnung von Zonen in Pixelrechtecke bei 100 %, 125 %, 150 % und 200 % DPI sowie negativen Monitorpositionen.
- Trefferprüfung auf Kanten und bei benachbarten Zonen.
- Monitorzuordnung mit identischem Gerät, geändertem Gerätenamen, fehlendem Monitor und Fallback.
- Profilwechsel, Schnellwahlkonflikte und Standardprofil.
- JSON-Roundtrip, atomare Speicherung, Versionsmigration und Wiederherstellung nach defekter Datei.
- Filterung von ungeeigneten Fensterhandles über abstrahierte Fenstereigenschaften.

Jede Kernfunktion wird testgetrieben entwickelt: Der neue Test muss zuerst aus dem erwarteten Grund fehlschlagen, danach wird nur der notwendige Produktionscode ergänzt. Der vollständige Build und alle Tests laufen vor jedem ausgelieferten Publish erneut.

### 8.2 Windows-Integration

- Start/Ende eines Verschiebevorgangs mit einem kontrollierten WPF-Testfenster.
- Overlayfenster aktivieren weder sich selbst noch andere Anwendungen und nehmen keine Mausklicks an.
- Profil-Hotkeys lassen sich registrieren, wechseln das Profil und werden beim Beenden freigegeben.
- Bildung und Erkennung des Autostart-Befehls werden ohne Registry-Schreibzugriff getestet; eine reale Änderung erfolgt ausschliesslich durch den Schalter der Benutzeroberfläche.
- Schutzschalter wird mit simulierten Callback-Fehlern und einer kontrollierten Ereignisfolge getestet.

### 8.3 Manuelle Abnahme auf dem Zielsystem

1. Editor mit jedem angeschlossenen Monitor öffnen und Skalierung sowie Monitoranordnung vergleichen.
2. Snap-Funktion bewusst aktivieren und zuerst ausschliesslich Notepad über Monitorgrenzen ziehen und in Rand-, Mittel- und Eckzonen ablegen.
3. Profile über Infobereich und Tastenkürzel wechseln und anschliessend erneut ein Fenster einrasten.
4. Einen Monitor ab- und wieder anschliessen und die Zuordnung kontrollieren.
5. Windows-Skalierungen oberhalb von 100 % sowie eine links vom Primärmonitor liegende Anzeige prüfen.
6. Not-Aus während eines sichtbaren Overlays auslösen und prüfen, dass Hook und alle Overlays verschwinden.
7. Autostart bewusst aktivieren, Anwendung neu starten und danach den Eintrag wieder deaktivieren.

## 9. Lieferumfang

- Vollständiger Quellcode und Solution-Datei.
- Automatisierte Tests und dokumentierter Testbefehl.
- Kurze deutschsprachige Bedienungsanleitung.
- Selbständiger `win-x64`-Publish-Ordner unter `outputs/SnapZones-prototype`.
- Technischer Prüfbericht mit automatisierten Ergebnissen, erkannter Monitoranordnung und verbleibenden Einschränkungen.

## 10. Abgrenzung des nächsten Ausbaus

Nach Praxiserprobung des Prototyps können Fensterregeln, Layouts pro virtuellem Desktop, frei konfigurierbare Tastenkürzel, Zonenüberlappung mit Auswahlpriorität, portabler Export/Import und ein signierter Installer separat geplant werden. Diese Punkte verändern den Kernworkflow des ersten Prototyps nicht.
