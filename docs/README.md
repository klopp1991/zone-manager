# Sascha’s Zone Manager

Sascha’s Zone Manager erstellt frei bearbeitbare Fensterbereiche pro Monitor. Sobald mindestens ein aktives Layout vorhanden ist, zeigt die Snap-Funktion beim Ziehen eines geeigneten Fensters an der Titelleiste die Bereiche als Overlay; beim Loslassen füllt das Fenster die gewählte Zone.

## Schnellstart

1. `ZoneManager.exe` direkt im Rootverzeichnis starten. Fordert die Anwendung Administratorrechte an, die Windows-UAC-Abfrage bestätigen; wird sie abgebrochen, läuft die Anwendung eingeschränkt weiter.
2. Unter **Layouts** einen Monitor und eines seiner Layouts wählen oder ein neues Layout erstellen.
3. Die vorhandenen Zonen anpassen und mit **+ Neue Zone** die grösste freie Fläche belegen.
4. Zonen ziehen, über acht Griffe skalieren oder als Prozent/Pixel mit Position/Grösse beziehungsweise vier Aussenabständen eingeben.
5. Die Snap-Funktion läuft mit den aktiven Layouts automatisch; jede gültige Änderung wird sofort gespeichert und angewendet.

Die Konfiguration liegt unter `%APPDATA%\ZoneManager\settings.json`. Die fünf letzten Stände liegen daneben als `settings.backup-1.json` bis `settings.backup-5.json`; bei einer beschädigten Hauptdatei wird die neueste gültige Sicherung automatisch wiederhergestellt. Autostart ist beim ersten Start ausgeschaltet.

Bestehende Installationen bleiben kompatibel: Beim ersten Start übernimmt die Anwendung den Inhalt des früheren Ordners `%APPDATA%\SnapZones` einmalig, sofern am neuen Ort noch nichts liegt. Die Übernahme ist idempotent, überschreibt nichts, wird protokolliert und lässt den alten Ordner als Rückfallebene unverändert liegen. Protokolle liegen neu unter `%LOCALAPPDATA%\ZoneManager\logs\zonemanager.log`; alte Protokolle wandern nicht mit. Der Autostart-Wert heisst neu `ZoneManager`; ein vorhandener Wert `SnapZones` wird beim Schalten des Autostarts entfernt.

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
- Autostart pro Benutzer; fordert die Anwendung beim Login Administratorrechte an, erscheint auch dort die Windows-UAC-Abfrage. Wird sie nicht bestätigt, startet die Anwendung eingeschränkt.

Jede Einstellung erklärt direkt in der Oberfläche Wirkung, Gültigkeitsbereich und Einschränkungen.

## Sicherheit und Not-Aus

Vor dem Laden der Oberfläche prüft die Anwendung ohne Prozessstart, ob eine Erhöhung überhaupt möglich ist: Sie liest dazu nur das eigene Token, die Administratorzugehörigkeit des verknüpften Tokens, `EnableLUA` und die Sitzungsart. Nur wenn eine Erhöhung möglich ist, startet sie sich über die Windows-UAC-Abfrage erneut im Administratormodus und kann dann auch erhöhte Fenster positionieren. Ist eine Erhöhung nicht möglich, wird sie abgebrochen oder schlägt sie fehl, läuft die Anwendung mit den vorhandenen Rechten weiter; ein Banner nennt den Grund und bietet einen erneuten Versuch an, der Tooltip im Infobereich weist den eingeschränkten Betrieb aus. Fehlgeschlagene Platzierungen erhöhter Fremdfenster werden als erwarteter Fall erklärt. Der reine Diagnosemodus bleibt absichtlich ohne Elevation. `Ctrl + Alt + Shift + F12` deaktiviert Hook und Overlays bis zum nächsten Programmstart. `Escape` beendet nur den aktuellen Ziehvorgang. Die Anwendung enthält keinen Treiber, keinen Windows-Dienst und keine Code-Injektion; ein Schutzschalter stoppt die Snap-Funktion bei Callback-Fehlern oder ungewöhnlich vielen Hook-Ereignissen.

## Diagnose

```powershell
ZoneManager.exe --diagnostics
```

Die Diagnose liest Konfigurationsstatus, Monitore, DPI, Autostartstatus sowie `isElevated`, `canElevate` und `elevationReason`. Sie registriert keinen Fenster-Hook und verändert weder Einstellungen noch Registry.

## Einschränkungen

- Nur Windows 11 x64.
- Wird die Windows-UAC-Abfrage abgebrochen, läuft die Anwendung ohne Administratorrechte weiter; Fenster von Programmen mit höheren Rechten lassen sich dann nicht positionieren.
- Nicht rechteckige oder überlappende Zonen, virtuelle Desktops und automatische Updates sind noch nicht enthalten.
- Eigene Layouts können nicht über eine dokumentierte API in das native Windows-Snap-Popup eingefügt werden; die Anwendung verwendet ein eigenes Overlay.
- Der Prototyp ist nicht digital signiert und kann beim ersten Start eine Windows-Sicherheitswarnung auslösen.

## Entwicklung und Prüfung

Voraussetzung ist das .NET 8 SDK. Der vollständige Prüf- und Publish-Lauf lautet:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify.ps1
```

Das Skript erzeugt das Mehrgrössen-Icon, stellt Pakete wieder her, führt alle Tests aus, baut Release, veröffentlicht eine selbständige Einzeldatei für `win-x64`, kopiert `ZoneManager.exe` ins Rootverzeichnis und prüft Diagnose, den Root-Build sowie Per-Monitor-DPI ohne aktivierten Hook. Test-, Build- und Publish-Aufrufe übergeben `-p:SkipRootExecutablePublish=true`, damit die Root-EXE nur einmal aus dem Publish-Artefakt entsteht. Die DPI-Prüfung startet die Oberfläche und braucht eine interaktive Sitzung; sonst `-SkipDpiCheck` verwenden. Sie wartet höchstens `-StartupTimeoutSeconds` (Vorgabe 30) auf die Bedienbereitschaft und bricht danach mit einer Meldung ab, statt zu hängen.

Die Lösung besteht aus `src\ZoneManager.Core`, `src\ZoneManager.Windows`, `src\ZoneManager.App` und `tests\ZoneManager.Tests`; die erzeugte Datei heisst unverändert `ZoneManager.exe`.

Auch ein normaler `dotnet build` oder Build in Visual Studio veröffentlicht nach erfolgreicher Kompilierung automatisch eine selbständige `win-x64`-Einzeldatei als `ZoneManager.exe` direkt ins Rootverzeichnis. Eine dort noch laufende Vorgängerversion wird atomar ersetzt und bis zu ihrem Prozessende als ignorierte Sicherungsdatei beibehalten.
