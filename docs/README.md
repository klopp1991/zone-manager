# Sascha’s Zone Manager

Sascha’s Zone Manager erstellt frei bearbeitbare Fensterbereiche pro Monitor. Sobald mindestens ein aktives Layout vorhanden ist, zeigt die Snap-Funktion beim Ziehen eines geeigneten Fensters an der Titelleiste die Bereiche als Overlay; beim Loslassen füllt das Fenster die gewählte Zone.

## Schnellstart

1. `ZoneManager.exe` direkt im Rootverzeichnis starten und die Windows-UAC-Abfrage bestätigen.
2. Unter **Layouts** einen Monitor und eines seiner Layouts wählen oder ein neues Layout erstellen.
3. Die vorhandenen Zonen anpassen und mit **+ Neue Zone** die grösste freie Fläche belegen.
4. Zonen ziehen, über acht Griffe skalieren oder als Prozent/Pixel mit Position/Grösse beziehungsweise vier Aussenabständen eingeben.
5. Die Snap-Funktion läuft mit den aktiven Layouts automatisch; jede gültige Änderung wird sofort gespeichert und angewendet.

Konfiguration und bestehende Installationen bleiben unter `%APPDATA%\SnapZones\settings.json` kompatibel. Die fünf letzten Stände liegen daneben als `settings.backup-1.json` bis `settings.backup-5.json`; bei einer beschädigten Hauptdatei wird die neueste gültige Sicherung automatisch wiederhergestellt. Autostart ist beim ersten Start ausgeschaltet.

**Export** schreibt jederzeit ein vollständiges JSON-Backup mit sämtlichen Einstellungen, Monitorlayouts, Zonen, IDs und Parametern. **Import** validiert die komplette Datei, zeigt den exakten Ersetzungsumfang und sichert den bisherigen Zustand unmittelbar vor der bestätigten Übernahme. Bestehende Profilkonfigurationen aus Schema 1 werden beim Laden in unabhängige Layouts pro Monitor migriert.

## App-Regeln

Unter **App-Regeln** verbindet eine Regel einen Prozesspfad oder Programmnamen mit einer Zielzone. Ein optionales Fenstertitelmuster und eine optionale Fensterklasse schränken die Auswahl weiter ein; `*` und `?` dienen als Platzhalter. Als Ereignis stehen **Fenster erstellt**, **Fenster fokussiert** und **Layout aktiviert** zur Verfügung, ergänzt durch 0 bis 30 Sekunden Verzögerung, 0 bis 3 Wiederholungen und eine Priorität von 0 bis 100.

Vor jedem Platzierungsversuch werden Fensteridentität, Regel und Ziel erneut geprüft. Fehlende Layouts, Monitore oder Zonen pausieren die Regel sichtbar; es gibt keinen stillen Fallback und App-Regeln starten keine Programme.

## Layouteditor

- **+ Neue Zone** belegt die grösste freie achsenparallele Fläche; ohne ausreichenden freien Bereich wird nichts verändert.
- Zonen docken innerhalb der eingestellten Magnetdistanz an Monitor- und Zonenkanten an; `Alt` deaktiviert den Magnetismus während des Ziehens.
- **Prozent** bleibt bei Auflösungsänderungen proportional; **Pixel** bezieht sich auf die aktuelle Windows-Arbeitsfläche des Monitors.
- **Position und Grösse** bearbeitet Links, Oben, Breite und Höhe; **Aussenabstände** bearbeitet Links, Oben, Rechts und Unten.
- Überlappende, zu kleine oder ausserhalb liegende Zonen werden markiert und können nicht gespeichert werden.

## Monitore und Windows-Anzeige

Monitornamen werden bevorzugt aus dem aktiven Displaypfad und den EDID-Daten gelesen. Die Seite **Windows-Anzeige** zeigt den erkannten monitorbezogenen Skalierungswert und öffnet die zuständigen Windows-Seiten.

Windows 11 stellt normalen Desktopanwendungen keine unterstützte Schnittstelle für frei wählbare monitorweise Textskalierung oder monitorweise Taskleisten-/Icongrössen bereit. Benutzerdefinierte Windows-Skalierung von 100 bis 500 % und Textskalierung von 100 bis 225 % sind globale Windows-Einstellungen; Sascha’s Zone Manager verwendet dafür keine Explorer-Injektion, privaten DPI-Pakete oder undokumentierten Registry-Werte.

## Einstellungen

- System-, helles oder dunkles Theme; Systemänderungen werden ohne Neustart übernommen.
- Overlay auf allen Monitoren oder nur auf dem aktiven Monitor.
- Sofortige Aktivierung oder Aktivierung mit Umschalttaste.
- Separate Overlay-Aussenabstände links, oben, rechts und unten, Overlay-Zonenabstand und Magnetdistanz für den Layouteditor.
- Overlayfarbe, Deckkraft und ein-/ausblendbare Zonennamen.
- Autostart pro Benutzer; die Windows-UAC-Abfrage muss auch beim Login bestätigt werden.

Jede Einstellung erklärt direkt in der Oberfläche Wirkung, Gültigkeitsbereich und Einschränkungen.

## Sicherheit und Not-Aus

Normale Programmstarts wechseln vor dem Laden der Oberfläche über die Windows-UAC-Abfrage in den Administratormodus. Dadurch kann die Anwendung auch erhöhte Fenster positionieren; der reine Diagnosemodus bleibt absichtlich ohne Elevation. `Ctrl + Alt + Shift + F12` deaktiviert Hook und Overlays bis zum nächsten Programmstart. `Escape` beendet nur den aktuellen Ziehvorgang. Die Anwendung enthält keinen Treiber, keinen Windows-Dienst und keine Code-Injektion; ein Schutzschalter stoppt die Snap-Funktion bei Callback-Fehlern oder ungewöhnlich vielen Hook-Ereignissen.

## Diagnose

```powershell
ZoneManager.exe --diagnostics
```

Die Diagnose liest Konfigurationsstatus, Monitore, DPI und Autostartstatus. Sie registriert keinen Fenster-Hook und verändert weder Einstellungen noch Registry.

## Einschränkungen

- Nur Windows 11 x64.
- Wird die Windows-UAC-Abfrage abgebrochen, startet die Anwendung nicht.
- Nicht rechteckige oder überlappende Zonen, virtuelle Desktops und automatische Updates sind noch nicht enthalten.
- Eigene Layouts können nicht über eine dokumentierte API in das native Windows-Snap-Popup eingefügt werden; die Anwendung verwendet ein eigenes Overlay.
- Der Prototyp ist nicht digital signiert und kann beim ersten Start eine Windows-Sicherheitswarnung auslösen.

## Entwicklung und Prüfung

Voraussetzung ist das .NET 8 SDK. Der vollständige Prüf- und Publish-Lauf lautet:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify.ps1
```

Das Skript erzeugt das Mehrgrössen-Icon, stellt Pakete wieder her, führt alle Tests aus, baut Release, veröffentlicht eine selbständige Einzeldatei für `win-x64`, kopiert `ZoneManager.exe` ins Rootverzeichnis und prüft Diagnose sowie Per-Monitor-DPI ohne aktivierten Hook.

Der Lauf schliesst eine Per-Monitor-DPI-Prüfung ein, die die Oberfläche startet und deshalb eine interaktive Sitzung mit bestätigter UAC-Abfrage braucht. In nicht interaktiven Umgebungen bleibt dieser Schritt sonst an der unbeantworteten Abfrage stehen; `-SkipDpiCheck` überspringt ihn.

Auch ein normaler `dotnet build` oder Build in Visual Studio veröffentlicht nach erfolgreicher Kompilierung automatisch eine selbständige `win-x64`-Einzeldatei als `ZoneManager.exe` direkt ins Rootverzeichnis. Eine dort noch laufende Vorgängerversion wird atomar ersetzt und bis zu ihrem Prozessende als ignorierte Sicherungsdatei beibehalten.

Dieser Schritt kostet bei jedem Build einen vollständigen Self-contained-Publish. Für schnelle Zwischenbuilds und in Prüfläufen, die die Root-EXE separat erzeugen, lässt er sich mit `-p:SkipRootExecutablePublish=true` überspringen; `scripts\verify-root-build.ps1` prüft den impliziten Weg gezielt in einem Wegwerfverzeichnis unter `work\`.
