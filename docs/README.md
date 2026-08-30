# Sascha Window Zones

Sascha Window Zones erstellt frei bearbeitbare Fensterbereiche pro Monitor. Beim Ziehen eines geeigneten Fensters an der Titelleiste zeigt die aktivierte Snap-Funktion die Bereiche als Overlay; beim Loslassen füllt das Fenster die gewählte Zone.

## Schnellstart

1. `SaschaWindowZones.exe` direkt im Rootverzeichnis starten.
2. Zuerst unter **Profile** das gewünschte Profil wählen oder erstellen.
3. Unter **Layouts** einen Monitor wählen, die vorhandene Zone anpassen und mit **+ Neue Zone** die grösste freie Fläche belegen.
4. Zonen ziehen, über acht Griffe skalieren oder als Prozent/Pixel mit Position/Grösse beziehungsweise vier Aussenabständen eingeben.
5. Unter **Einstellungen** die Snap-Funktion bewusst aktivieren; jede gültige Änderung wird automatisch gespeichert.

Konfiguration und bestehende Installationen bleiben unter `%APPDATA%\SnapZones\settings.json` kompatibel. Die fünf letzten Stände liegen daneben als `settings.backup-1.json` bis `settings.backup-5.json`; bei einer beschädigten Hauptdatei wird die neueste gültige Sicherung automatisch wiederhergestellt. Snap-Funktion und Autostart sind beim ersten Start ausgeschaltet.

**Export** schreibt jederzeit ein vollständiges JSON-Backup mit sämtlichen Einstellungen, Profilen, Monitorlayouts, Zonen, IDs und Parametern. **Import** validiert die komplette Datei, zeigt den exakten Ersetzungsumfang und sichert den bisherigen Zustand unmittelbar vor der bestätigten Übernahme.

## Layouteditor

- **+ Neue Zone** belegt die grösste freie achsenparallele Fläche; ohne ausreichenden freien Bereich wird nichts verändert.
- Zonen docken innerhalb der eingestellten Magnetdistanz an Monitor- und Zonenkanten an; `Alt` deaktiviert den Magnetismus während des Ziehens.
- **Prozent** bleibt bei Auflösungsänderungen proportional; **Pixel** bezieht sich auf die aktuelle Windows-Arbeitsfläche des Monitors.
- **Position und Grösse** bearbeitet Links, Oben, Breite und Höhe; **Aussenabstände** bearbeitet Links, Oben, Rechts und Unten.
- Überlappende, zu kleine oder ausserhalb liegende Zonen werden markiert und können nicht gespeichert werden.

## Monitore und Windows-Anzeige

Monitornamen werden bevorzugt aus dem aktiven Displaypfad und den EDID-Daten gelesen. Die Seite **Windows-Anzeige** zeigt den erkannten monitorbezogenen Skalierungswert und öffnet die zuständigen Windows-Seiten.

Windows 11 stellt normalen Desktopanwendungen keine unterstützte Schnittstelle für frei wählbare monitorweise Textskalierung oder monitorweise Taskleisten-/Icongrössen bereit. Benutzerdefinierte Windows-Skalierung von 100 bis 500 % und Textskalierung von 100 bis 225 % sind globale Windows-Einstellungen; Sascha Window Zones verwendet dafür keine Explorer-Injektion, privaten DPI-Pakete oder undokumentierten Registry-Werte.

## Einstellungen

- System-, helles oder dunkles Theme; Systemänderungen werden ohne Neustart übernommen.
- Overlay auf allen Monitoren oder nur auf dem aktiven Monitor.
- Sofortige Aktivierung oder Aktivierung mit Umschalttaste.
- Separate Overlay-Aussenabstände links, oben, rechts und unten, Overlay-Zonenabstand und Magnetdistanz für den Layouteditor.
- Overlayfarbe, Deckkraft und ein-/ausblendbare Zonennamen.
- Autostart pro Benutzer ohne Administratorrechte.

Jede Einstellung erklärt direkt in der Oberfläche Wirkung, Gültigkeitsbereich und Einschränkungen.

## Fensterplatzierung

Die automatische Fensterplatzierung ist ein separater Hauptschalter. Ihr globaler Standard ist **Letzte Platzierung merken**: Ein sichtbares, geeignetes Top-Level-Fenster wird nach dem Öffnen höchstens einmal wiederhergestellt und spätere manuelle Bewegungen bleiben frei. Mehrere gleichartige Hauptfenster einer Anwendung teilen einen Anwendung-/Fenstertyp; Dialoge bleiben durch ihre Fensterart getrennt. Ein maximiertes Ziel wird nur wieder maximiert, wenn es bereits im Vordergrund ist; andernfalls bleibt es ohne Fokusdiebstahl unverändert. Minimierte Zustände und minimierte Rechtecke werden nie gespeichert oder wiederhergestellt.

Unter **Fensterplatzierung** lassen sich gelernte Einträge vergessen, eine **Feste Zone** für ein Profil, einen Monitor und eine Zone festlegen oder ein Fenstertyp mit **Nicht verwalten** ausschliessen. Eine optionale `TitlePattern`-Bedingung ist nur für erweiterte Regeln verfügbar; gleich spezifische Regeln werden als Konflikt nicht ausgeführt. Die festen Zonen und Ausschlüsse gelten beim Öffnen, sie erzwingen keine laufende Position.

Gelerntes liegt getrennt in `placements.json`; Regeln und der Hauptschalter bleiben in `settings.json`. Normal liegt beides unter `%APPDATA%\SnapZones`; bei einer Datei `portable.flag` neben `SaschaWindowZones.exe` liegen die Dateien unter `Data\` neben der EXE. Eine beschädigte Platzierungsdatei wird im normalen Anwendungsbetrieb gesichert und wenn möglich aus `placements.backup-1.json` wiederhergestellt; die Diagnose führt diese Wiederherstellung ausdrücklich nicht aus.

## Sicherheit und Not-Aus

`Ctrl + Alt + Shift + F12` deaktiviert Hook und Overlays sofort, beendet anstehende Platzierungen und speichert Snap-Funktion sowie automatische Fensterplatzierung als ausgeschaltet. `Escape` beendet nur den aktuellen Ziehvorgang. Die Anwendung enthält keinen Treiber, keinen Windows-Dienst und keine Code-Injektion; ein Schutzschalter stoppt die Snap-Funktion bei Callback-Fehlern oder ungewöhnlich vielen Hook-Ereignissen.

## Diagnose

```powershell
SaschaWindowZones.exe --diagnostics
```

Die Diagnose liest Konfigurationsstatus, Monitore, DPI, Autostart und den Platzierungsstatus. Sie registriert keinen Fenster-Hook, startet keine Platzierungs-Engine und verändert weder `settings.json` noch `placements.json` oder die Registry. Auch eine beschädigte `placements.json` wird nur als Diagnosezustand gemeldet.

## Einschränkungen

- Nur Windows 11 x64.
- Fenster mit höheren Administratorrechten können ohne gleich hohe Rechte nicht positioniert werden.
- Nicht rechteckige oder überlappende Zonen, virtuelle Desktops, mehrere individuelle Plätze für gleichartige Fenster und fortlaufendes Erzwingen einer Zone sind nicht enthalten.
- Eigene Layouts können nicht über eine dokumentierte API in das native Windows-Snap-Popup eingefügt werden; die Anwendung verwendet ein eigenes Overlay.
- Der Prototyp ist nicht digital signiert und kann beim ersten Start eine Windows-Sicherheitswarnung auslösen.

## Entwicklung und Prüfung

Voraussetzung ist das .NET 8 SDK. Der vollständige Prüf- und Publish-Lauf lautet:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify.ps1
```

Das Skript erzeugt das Mehrgrössen-Icon, stellt Pakete wieder her, führt alle Tests aus, baut Release, veröffentlicht eine selbständige Einzeldatei für `win-x64`, kopiert `SaschaWindowZones.exe` ins Rootverzeichnis und prüft Diagnose sowie Per-Monitor-DPI ohne aktivierten Hook.

Auch ein normaler `dotnet build` oder Build in Visual Studio veröffentlicht nach erfolgreicher Kompilierung automatisch eine selbständige `win-x64`-Einzeldatei als `SaschaWindowZones.exe` direkt ins Rootverzeichnis. Eine dort noch laufende Vorgängerversion wird atomar ersetzt und bis zu ihrem Prozessende als ignorierte Sicherungsdatei beibehalten.
