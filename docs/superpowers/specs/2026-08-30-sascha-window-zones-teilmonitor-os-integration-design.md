# Sascha Window Zones: Teilmonitor- und OS-Integrationsarchitektur

**Datum:** 30. August 2026  
**Status:** Freigegeben
**Zielplattform:** Windows 11 x64  
**Technologie:** .NET 8, WPF, dokumentierte Win32- und COM-Schnittstellen

## 1. Verbindliche Produktentscheide

Sascha Window Zones verwendet ausschliesslich **Teilmonitore**. Ein Teilmonitor ist eine logisch eigenstaendige Arbeitsflaeche innerhalb der Windows-Arbeitsflaeche eines physischen Monitors. Windows und andere Programme erkennen weiterhin nur den physischen Monitor.

Folgende Entscheide sind verbindlich:

- Es gibt keinen virtuellen Display-Treiber.
- Es gibt keinen Windows-Dienst, keine Explorer-Injektion und keine In-Process-Injektion in fremde Programme.
- Es werden keine privaten oder undokumentierten Windows-Schnittstellen eingesetzt.
- Normales Windows-Maximieren belegt weiterhin den ganzen physischen Monitor.
- `Win + Pfeil`, `Win + Z`, native Snap-Layouts, Taskleiste und virtuelle Windows-Desktops bleiben unter Kontrolle von Windows.
- Teilmonitor-Aktionen sind eigene, ausdrueckliche Befehle der Anwendung.
- Hooks und globale Hotkeys existieren nur waehrend der laufenden Anwendung und werden bei Not-Aus oder Programmende entfernt.
- Die Anwendung veraendert keine Windows-Anzeigeeinstellung, keine Shell-Konfiguration und keine globale DPI-Einstellung.

## 2. Zielbild

Die vorhandenen Zonen werden fachlich zu Teilmonitoren ausgebaut. Sie bilden die gemeinsame Grundlage fuer:

- Einrasten beim Ziehen;
- gezieltes Fuellen eines Teilmonitors;
- Verschieben und zyklisches Wechseln zwischen Teilmonitoren;
- Fokuswechsel innerhalb oder zwischen Teilmonitoren;
- Profile pro physischer Monitorkonfiguration;
- App-Regeln;
- Arbeitsbereiche;
- sichere Wiederherstellung der vorherigen Fensterposition innerhalb der laufenden Sitzung.

Das Programm soll tief in den normalen Fensterworkflow integriert sein, ohne Windows-Funktionen umzudefinieren. Jede Automatik ist einzeln deaktivierbar. Ein nicht laufendes Sascha Window Zones hinterlaesst kein veraendertes Windows-Verhalten.

## 3. Nicht-Ziele

- Teilmonitore erscheinen nicht in den Windows-Anzeigeeinstellungen.
- Anwendungen erhalten keine neuen Display-Handles oder EDID-Daten.
- Exklusives Vollbild wird nicht in einen Teilmonitor gezwungen.
- Native Windows-Snap-Gruppen werden nicht erweitert oder ersetzt.
- Der Maximieren-Button fremder Fenster wird nicht abgefangen.
- Windows-Tastenkombinationen werden nicht ueberschrieben.
- Fenster mit hoeherer Prozessintegritaet werden ohne gleich hohe Rechte nicht gesteuert.
- Virtuelle Windows-Desktops werden nicht erstellt, geloescht oder automatisch gewechselt.

## 4. Domaenenmodell

`ZoneDefinition` bleibt aus Kompatibilitaetsgruenden die persistierte Quelle der Geometrie. In der Bedienung und in neuen Anwendungsdiensten wird eine Zone als Teilmonitor behandelt; es entsteht keine zweite, konkurrierende Rechteckstruktur.

Ein Teilmonitor besitzt:

- die bestehende stabile Zonen-ID;
- einen Namen;
- die stabile ID des physischen Elternmonitors ueber `MonitorLayout`;
- normierte Grenzen relativ zur Windows-Arbeitsflaeche des Elternmonitors;
- eine feste Reihenfolge fuer Wechselbefehle und Hotkey-Ziele;
- einen Aktivstatus;
- optional spaeter eine semantische Rolle wie `Haupt`, `Kommunikation` oder `Video`.

Die Windows-Arbeitsflaeche, nicht die rohe Displayflaeche, bleibt die Bezugsflaeche. Dadurch respektieren Teilmonitore die von Windows verwaltete Taskleiste. Teilmonitore duerfen sich im gespeicherten gueltigen Zustand nicht ueberlappen und muessen die bestehende Mindestgroesse einhalten.

## 5. Architektur

```text
Windows-Ereignisse     Globale Hotkeys       UI / Tray / Regeln
        |                    |                       |
        +--------------------+-----------------------+
                             v
                  WindowCommandCoordinator
                    serielle Befehlsqueue
                             |
            +----------------+----------------+
            |                                 |
            v                                 v
 PartMonitorPlacementEngine          WindowPolicyEvaluator
 reine Geometrie und Auswahl         Eignung und Sicherheitsgrenzen
            |                                 |
            +----------------+----------------+
                             v
                    WindowsWindowGateway
              validieren, restaurieren, setzen
                             |
                             v
                   dokumentierte Win32-APIs
```

### 5.1 Core

Der plattformunabhaengige Kern enthaelt:

- `PartMonitorResolver`: ordnet Pixelpunkte und Fensterrechtecke Teilmonitoren zu;
- `PartMonitorPlacementEngine`: berechnet Fuellen, Verschieben, zyklisches Ziel und Rueckkehrposition;
- `WindowCommand`: unveraenderliche Befehle ohne Fenster-Handle-spezifische Win32-Logik;
- `WindowPolicyEvaluator`: entscheidet anhand eines neutralen Fenstersnapshots, ob eine Aktion erlaubt ist;
- `PlacementHistory`: begrenzter, sitzungsbezogener Verlauf fuer Rueckkehr und Undo.

Alle Geometrieentscheidungen sind reine Funktionen und werden ohne Windows-Fenster testbar gehalten.

### 5.2 Windows-Adapter

Der Windows-Layer kapselt ausschliesslich dokumentierte Betriebssystemgrenzen:

- `WindowsEventSource` verwendet `SetWinEventHook` mit `WINEVENT_OUTOFCONTEXT` fuer Verschiebe-, Fokus-, Sichtbarkeits- und relevante Fensterlebenszyklusereignisse.
- `WindowsWindowGateway` liest Fenstereigenschaften und `WINDOWPLACEMENT`, stellt minimierte oder maximierte Fenster nur bei einem ausdruecklichen Teilmonitor-Befehl wieder her und setzt anschliessend die berechnete Normalposition.
- `WindowsDisplayTopology` kombiniert die bestehende Monitorerkennung mit `QueryDisplayConfig` und reagiert entprellt auf Topologieaenderungen.
- `WindowsHotkeyHost` registriert ausschliesslich konfigurierbare, freie Tastenkombinationen mit `RegisterHotKey` und meldet Konflikte sichtbar.
- `WindowsVirtualDesktopAwareness` darf mit `IVirtualDesktopManager` lediglich pruefen, ob ein Fenster auf dem aktuellen virtuellen Desktop liegt; automatische Desktopwechsel sind ausgeschlossen.

Der Windows-Layer kennt keine Profile, App-Regeln oder UI-Zustaende.

### 5.3 Anwendungsschicht

Der `WindowCommandCoordinator` ist die einzige Stelle, die Fensteraktionen ausloest. Drag-Erkennung, Hotkeys, UI, App-Regeln und Arbeitsbereiche reichen typisierte Befehle ein und duerfen `SetWindowPos` nicht direkt aufrufen.

Die Anwendungsschicht enthaelt:

- `DragSnapOrchestrator` fuer den vorhandenen Ziehworkflow;
- `PartMonitorCommandService` fuer explizite Benutzeraktionen;
- `AppRuleEngine` fuer Ereignisregeln;
- `WorkspaceOrchestrator` fuer gruppierte Start- und Platzierungsablaeufe;
- `OverlayPresenter` fuer Vorschau und Zielmarkierung;
- `ProfileActivationCoordinator` fuer atomare Profilwechsel.

## 6. Ereignis- und Befehlsfluss

Native Callbacks erstellen nur einen kleinen unveraenderlichen Ereignisdatensatz und stellen ihn in eine begrenzte Queue. Sie lesen keine Konfiguration, schreiben keine Datei, fragen keine Monitortopologie ab und aktualisieren keine WPF-Oberflaeche.

Der Koordinator verarbeitet Befehle seriell:

1. Fenster-Handle und Prozesszuordnung erneut validieren.
2. Sichtbarkeit, Top-Level-Status, Toolwindow-, Cloaking-, Vollbild- und Integritaetsgrenzen pruefen.
3. aktuellen Profil- und Topologie-Snapshot erfassen.
4. Zielteilmonitor deterministisch aufloesen.
5. bisherige `WINDOWPLACEMENT`-Information fuer die Sitzung sichern.
6. Fensterzustand nur falls fuer den ausdruecklichen Befehl erforderlich restaurieren.
7. Zielrechteck anwenden und Ergebnis nachpruefen.
8. Erfolg, Ablehnung oder Fehler an UI, Log und aufrufenden Dienst melden.

Haeufige Standortereignisse desselben Fensters duerfen zusammengefasst werden. Verschiebestart, Verschiebeende, Not-Aus und Benutzerbefehle duerfen nicht verworfen werden.

## 7. Fensterverhalten

### 7.1 Ziehen und Einrasten

Beim gueltigen Ziehbeginn erscheinen die Teilmonitor-Overlays. Das Ziel folgt dem Cursor. Beim Loslassen fuellt das Fenster den gewaehlten Teilmonitor als normales, nicht maximiertes Fenster.

`Escape` bricht nur den aktuellen Vorgang ab. Der globale Not-Aus beendet den Vorgang, entfernt Hook und Hotkeys und speichert die Snap-Funktion als deaktiviert.

### 7.2 Normales Maximieren

Der Maximieren-Button, Doppelklick auf die Titelleiste und native Windows-Maximierbefehle bleiben unangetastet. Das Fenster maximiert auf die Windows-Arbeitsflaeche des ganzen physischen Monitors.

### 7.3 Teilmonitor fuellen

`Teilmonitor fuellen` ist ein eigener Befehl. Ein maximiertes oder minimiertes Fenster wird zuerst in den normalen Zustand gebracht und danach auf den Teilmonitor gesetzt. Der Befehl veraendert weder die systemweite Maximierlogik noch das Verhalten des naechsten normalen Maximierens.

### 7.4 Zwischen Teilmonitoren verschieben

Der Standardbefehl verschiebt das aktive Fenster auf den naechsten oder vorherigen Teilmonitor und fuellt das Ziel. Eine spaetere Option darf relative Fenstergroesse und Position erhalten, muss aber dieselbe Platzierungs- und Sicherheitslogik verwenden.

### 7.5 Rueckkehr

`Vorherige Position` stellt die zuletzt durch Sascha Window Zones veraenderte normale Fensterposition derselben Sitzung wieder her. Fenster-Handles werden nie dauerhaft gespeichert. Wenn das Fenster inzwischen ersetzt, geschlossen oder einer anderen Prozessinstanz zugeordnet wurde, wird die Rueckkehr abgelehnt.

## 8. Hotkeys und Bedienoberflaechen

Hotkeys sind Teilmonitor-Befehle und keine Ersetzungen nativer Windows-Kuerzel. Kombinationen mit reservierten Windows-Tasten werden nicht als Standard verwendet. Jede Registrierung wird einzeln geprueft; ein Konflikt deaktiviert nur den betroffenen Hotkey und zeigt die konkrete Kombination an.

Vorgesehene Befehle:

- aktives Fenster in Teilmonitor 1 bis 9 fuellen;
- aktives Fenster zum naechsten oder vorherigen Teilmonitor verschieben;
- vorherige Position wiederherstellen;
- naechstes Fenster innerhalb des aktuellen Teilmonitors fokussieren;
- Profil aktivieren;
- Snap-Funktion ein- oder ausschalten;
- Not-Aus.

Dieselben Befehle stehen in der Anwendung und im Infobereich zur Verfuegung. Es werden keine Menues oder Titelleisten fremder Fenster veraendert.

## 9. App-Regeln und Arbeitsbereiche

App-Regeln reagieren auf `Fenster erstellt`, `Fenster fokussiert` oder `Profil aktiviert`. Vor jeder Aktion werden Regel, Fenster, Prozess, Zielmonitor und Zielteilmonitor erneut geprueft. Fehlende Ziele pausieren die Regel sichtbar; es gibt keinen stillen Fallback auf einen anderen Monitor.

Arbeitsbereiche verwenden dieselbe Befehlsqueue. Sie duerfen Anwendungen ohne erhoehte Rechte starten, auf ein gueltiges Hauptfenster warten und es danach platzieren. Ein Fehler bei einer Anwendung stoppt nicht die uebrigen Eintraege.

Regeln starten keine Programme. Arbeitsbereiche lesen keine Fensterinhalte und speichern keine fremden Befehlszeilen, ausser der Benutzer hat Startargumente selbst erfasst.

## 10. Virtuelle Windows-Desktops

Teilmonitore und virtuelle Windows-Desktops bleiben getrennte Konzepte. Standardmaessig verarbeitet Sascha Window Zones nur Fenster auf dem aktuell sichtbaren virtuellen Desktop.

Die dokumentierte `IVirtualDesktopManager`-Schnittstelle darf fuer Sichtbarkeitspruefungen und spaetere ausdrueckliche Fensteraktionen verwendet werden. Die Anwendung verwendet keine internen Shell-Schnittstellen zum Auflisten, Erstellen, Loeschen, Benennen oder Umschalten virtueller Desktops.

## 11. Monitor- und Topologieaenderungen

Bei Aufloesungs-, DPI-, Taskleisten-, Docking-, Standby- oder Monitorwechseln wird die Topologie entprellt neu gelesen. Normierte Teilmonitorgrenzen werden gegen die neue Arbeitsflaeche berechnet und validiert.

Verschwindet ein physischer Elternmonitor:

- laufende Platzierungen auf diesen Monitor werden abgebrochen;
- zugehoerige Regeln werden pausiert;
- Fenster werden nicht automatisch auf einen beliebigen Monitor verschoben;
- das Profil und seine Teilmonitore bleiben gespeichert;
- nach Rueckkehr desselben stabil identifizierten Monitors werden sie wieder aktiv.

Ein Topologiewechsel waehrend eines Drag-Vorgangs beendet nur diesen Vorgang und baut danach die Overlayziele neu auf.

## 12. Sicherheitsgrenzen

Vor jeder Fensteraktion gelten folgende Positivkriterien:

- gueltiges sichtbares Top-Level-Fenster;
- kein Kind-, Tool-, Overlay-, Cloaked- oder Shell-Hilfsfenster;
- kein exklusives Vollbild;
- keine hoehere Prozessintegritaet als Sascha Window Zones;
- Hauptfenster der eigenen Anwendung nur ueber eine ausdrueckliche eigene Freigabe;
- Prozess-ID und Fensterklasse stimmen bei der abschliessenden Validierung weiterhin.

Es gibt keine automatische Selbsterhoehung und keinen dauerhaft erhoehten Begleitprozess. Wenn Windows eine Aktion wegen Integritaet, Vordergrundregeln oder eines unkooperativen Fensters ablehnt, wird diese Grenze angezeigt und nicht umgangen.

## 13. Fehlerbehandlung und Wiederherstellung

Jeder Befehl liefert einen strukturierten Status: `Erfolgreich`, `Nicht geeignet`, `Ziel fehlt`, `Hotkey-Konflikt`, `Windows abgelehnt`, `Zeitueberschreitung` oder `Interner Fehler`.

- Ablehnungen eines einzelnen Fensters stoppen nicht den Koordinator.
- Wiederholungen sind auf maximal drei Versuche mit kurzer Verzoegerung begrenzt und nur fuer noch nicht erschienene oder noch nicht positionierbare Fenster erlaubt.
- Hook- oder Queuefehler loesen den bestehenden Schutzschalter aus.
- Bei Not-Aus werden Queue, Overlays, Hooks und Hotkeys deaktiviert; es werden keine fremden Fenster zwangsweise zurueckverschoben.
- Fehlertexte in der UI sind auswaehlbar und kopierbar.
- Das Dateilog enthaelt Befehl, Fensteridentitaet, Ziel-ID, Ergebnis und Win32-/HRESULT-Code, aber keine Fenstertitel oder Dokumentinhalte, sofern diese nicht fuer eine explizite Regel benoetigt werden.

## 14. Nebenlaeufigkeit und Leistung

Es gibt genau einen seriellen Fensterausfuehrer. Damit koennen Drag-Ende, Hotkey, Regel und Profilwechsel nicht gleichzeitig dasselbe Fenster positionieren.

Zielwerte:

- weniger als 10 Millisekunden Arbeit im verwalteten Ereignisempfaenger;
- keine blockierende Arbeit im nativen Callback;
- Overlayreaktion innerhalb von 100 Millisekunden;
- Hotkey bis gestartete Platzierung innerhalb von 100 Millisekunden;
- begrenzte Ereignisqueue mit Zusammenfassung redundanter Standortereignisse;
- Abbruch laufender Befehle bei Not-Aus oder ungueltigem Topologie-Snapshot.

## 15. Persistenz und Migration

Bestehende `ZoneDefinition`-, Profil-, Monitor- und Zonen-IDs bleiben erhalten. Neue optionale Reihenfolge-, Rollen-, Hotkey- und Regelattribute erhalten sichere Standardwerte. Alte Konfigurationen werden vor einer Migration gesichert und erst nach vollstaendiger Validierung ersetzt.

Persistiert werden nur Definitionen und Benutzerentscheide. Fenster-Handles, aktuelle Z-Reihenfolge, Platzierungsverlauf, Callbackdaten und laufende Befehle bleiben sitzungsbezogen.

## 16. Diagnose

Die sichere Diagnose bleibt read-only und darf keinen Hook oder Hotkey registrieren. Sie berichtet:

- erkannte physische Monitore und Arbeitsflaechen;
- Anzahl und Gueltigkeit der Teilmonitore je Profil;
- verfuegbare und konfliktbehaftete Hotkeydefinitionen ohne Registrierung;
- aktive Sicherheitsgrenzen;
- pausierte Regeln und fehlende Ziele;
- zuletzt ausgeloesten Schutzschalter;
- Bestaetigung, dass kein Treiber, Dienst oder In-Process-Hook Bestandteil der Anwendung ist.

## 17. Teststrategie und Abnahme

### 17.1 Automatisierte Kerntests

- Punkt-, Fenster- und Zielzuordnung bei Randpunkten, DPI und negativen Desktopkoordinaten.
- Fuellen, Zyklus, Rueckkehr und Topologiewechsel als reine Geometrie.
- Serielle Befehlsreihenfolge und Zusammenfassung redundanter Ereignisse.
- Erneute Handle-/Prozessvalidierung unmittelbar vor der Platzierung.
- Normales Maximieren wird durch keine Teilmonitor-Komponente abgefangen.
- Maximiertes Fenster wird nur bei ausdruecklichem Teilmonitor-Befehl restauriert.
- Hotkey-Konflikt deaktiviert nur die betroffene Aktion.
- Erhoehte, unsichtbare, fremde Desktop-, Tool- und Vollbildfenster werden abgelehnt.
- Monitorverlust pausiert Ziele ohne stillen Fallback.
- Not-Aus entfernt Hook, Hotkeys, Overlays und ausstehende Befehle.

### 17.2 Windows-Integrationstests

- reale Fenster auf Einzel-, Mehrmonitor- und gemischten DPI-Systemen;
- negative Monitorpositionen und Taskleisten an allen vier Kanten;
- Standby, Docking, Aufloesungswechsel und Monitortrennung;
- minimierte, maximierte, UWP-/WinUI-, klassische Win32- und WPF-Fenster;
- Konflikte mit belegten globalen Hotkeys;
- virtuelle Windows-Desktops ohne automatischen Desktopwechsel;
- Programmende und Neustart ohne verbleibende Hooks oder Hotkeys.

### 17.3 Manuelle Abnahme

1. Ziehen auf einen Teilmonitor fuellt exakt dessen Arbeitsflaeche.
2. Teilmonitor-Hotkey fuellt das Ziel als normales Fenster.
3. Maximieren-Button und Doppelklick maximieren danach den ganzen physischen Monitor.
4. `Win + Pfeil` und `Win + Z` funktionieren unveraendert.
5. Ein ungueltiges oder erhoehtes Fenster bleibt unveraendert und erzeugt einen klaren Status.
6. Not-Aus und Programmende entfernen jede laufzeitbezogene Windows-Integration.

## 18. Umsetzungszerlegung

Die Gesamtarchitektur wird in getrennten, jeweils pruefbaren Ausbaustufen umgesetzt:

1. **Teilmonitor-Kern:** Resolver, Platzierungsbefehle, Rueckkehrverlauf und bestehender Drag-Workflow.
2. **OS-Ereignisbruecke:** serielle Queue, erneute Validierung, Topologieaenderungen und Schutzschalter.
3. **Teilmonitor-Bedienung:** konfigurierbare Hotkeys, UI-/Tray-Befehle und Fokuswechsel.
4. **Automatik:** App-Regeln mit Pausierung, Wiederholungsgrenzen und Diagnose.
5. **Arbeitsbereiche:** Start-, Erkennungs- und Platzierungsorchestrierung.

Nach Freigabe dieser Spezifikation wird zuerst nur fuer Stufe 1 ein detaillierter Implementierungsplan erstellt. Ein Display-Treiber ist in keiner Stufe vorgesehen.

## 19. Fachliche Referenzen

- [SetWinEventHook](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwineventhook)
- [SetWindowPos](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowpos)
- [GetWindowPlacement](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowplacement)
- [RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey)
- [QueryDisplayConfig](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-querydisplayconfig)
- [IVirtualDesktopManager](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-ivirtualdesktopmanager)
