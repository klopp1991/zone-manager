# Sascha’s Zone Manager

Sascha’s Zone Manager erstellt frei bearbeitbare Fensterbereiche pro Monitor. Sobald mindestens ein aktives Layout vorhanden ist, zeigt die Snap-Funktion beim Ziehen eines geeigneten Fensters an der Titelleiste die Bereiche als Overlay; beim Loslassen füllt das Fenster die gewählte Zone.

Hältst du beim Ziehen `Ctrl` gedrückt, sammelst du mehrere Zonen ein; das
Fenster füllt beim Loslassen das umschliessende Rechteck. Eine solche Auswahl
bleibt auf einem Monitor: Wechselst du den Bildschirm, beginnt dort eine neue
Auswahl.

## Schnellstart

1. `SaschaZoneManager.exe` direkt im Rootverzeichnis starten und die Windows-UAC-Abfrage bestätigen.
2. Unter **Layouts** einen Monitor und eines seiner Layouts wählen oder ein neues Layout erstellen.
3. Die vorhandenen Zonen anpassen und mit **+ Neue Zone** die grösste freie Fläche belegen.
4. Zonen ziehen, über acht Griffe skalieren oder als Prozent/Pixel mit Position/Grösse beziehungsweise vier Aussenabständen eingeben.
5. Die Snap-Funktion läuft mit den aktiven Layouts automatisch; jede gültige Änderung wird sofort gespeichert und angewendet.

Konfiguration und bestehende Installationen bleiben unter `%APPDATA%\SnapZones\settings.json` kompatibel. Die fünf letzten Stände liegen daneben als `settings.backup-1.json` bis `settings.backup-5.json`; bei einer beschädigten Hauptdatei wird die neueste gültige Sicherung automatisch wiederhergestellt. Autostart ist beim ersten Start ausgeschaltet.

**Export** schreibt jederzeit ein vollständiges JSON-Backup mit sämtlichen Einstellungen, Monitorlayouts, Zonen, IDs und Parametern. **Import** validiert die komplette Datei, zeigt den exakten Ersetzungsumfang und sichert den bisherigen Zustand unmittelbar vor der bestätigten Übernahme. Bestehende Profilkonfigurationen aus Schema 1 werden beim Laden in unabhängige Layouts pro Monitor migriert.

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

Jede Einstellung zeigt dauerhaft eine einzeilige Erklärung ihrer Wirkung. Das
Info-Symbol klappt eine ausführliche Erklärung mit zulässigem Wertebereich und
Auslieferungswert auf. Numerische Werte stehen in der Einheit, in der sie
wirken: Abstände und Magnetdistanz in Pixel, die Deckkraft in Prozent.

Geänderte Einstellungen werden als solche markiert und lassen sich einzeln
zurücksetzen; **Alle auf Standard zurücksetzen** stellt die ganze Seite wieder
her, ohne Layouts oder Zonen anzutasten. Das Suchfeld durchsucht Bezeichnungen,
Hilfetexte und gebräuchliche Synonyme, sodass etwa «Transparenz» die Deckkraft
findet.

Die Vorschau im Abschnitt **Darstellung des Overlays** zeichnet Farbe,
Deckkraft, Zonenabstand und Zonennamen mit derselben Geometrie und denselben
Füll- und Rahmenregeln wie das echte Overlay. Wertänderungen lassen sich damit
beurteilen, ohne ein Fenster zu ziehen.

### Wertebereiche

| Einstellung | Bereich | Standard |
| --- | --- | --- |
| Aussenabstand des Overlays | 0 – 400 px | 8 px |
| Abstand zwischen Zonen | 0 – 80 px | 8 px |
| Magnetdistanz im Editor | 0 – 40 px | 10 px |
| Deckkraft | 8 – 75 % | 24 % |

Aussenabstand und Zonenabstand betreffen ausschliesslich die Overlay-Vorschau.
Fenster werden weiterhin exakt nach der Layoutdefinition platziert. Auch bei 0
behält das Overlay einen visuellen Mindestabstand von 8 px, damit benachbarte
Flächen unterscheidbar bleiben.

## Sicherheit und Not-Aus

Normale Programmstarts wechseln vor dem Laden der Oberfläche über die Windows-UAC-Abfrage in den Administratormodus. Dadurch kann die Anwendung auch erhöhte Fenster positionieren; der reine Diagnosemodus bleibt absichtlich ohne Elevation. `Ctrl + Alt + Shift + F12` deaktiviert Hook und Overlays bis zum nächsten Programmstart. `Escape` beendet nur den aktuellen Ziehvorgang. Die Anwendung enthält keinen Treiber, keinen Windows-Dienst und keine Code-Injektion; ein Schutzschalter stoppt die Snap-Funktion bei Callback-Fehlern oder ungewöhnlich vielen Hook-Ereignissen.

## Diagnose

```powershell
SaschaZoneManager.exe --diagnostics
```

Die Diagnose liest Konfigurationsstatus, Monitore, DPI und Autostartstatus. Sie registriert keinen Fenster-Hook und verändert weder Einstellungen noch Registry.

## Vergleich mit anderen Programmen

Die folgenden Punkte fehlen gegenüber vergleichbaren Werkzeugen
(PowerToys FancyZones, AquaSnap, DisplayFusion) und sind nach erwartetem Nutzen
geordnet. Sie sind bewusst noch nicht umgesetzt, nicht übersehen.

1. **Fenster per Tastatur platzieren.** `Win` + Pfeiltasten in ein Zielfeld
   verschieben, wahlweise nach Zonenindex oder nach geometrischer Lage. Die
   sinnvolle Ergänzung dazu ist, die entstehende Tastenkombination direkt unter
   der jeweiligen Auswahl anzuzeigen, weil genau diese Einstellung bei
   FancyZones am häufigsten missverstanden wird.
2. **Fenster nach Auflösungs- oder Monitorwechsel zurücksetzen.** Nach einem
   Wechsel der Arbeitsfläche die Fenster erneut in ihre Zonen einpassen. Dazu
   gehört eine definierte und dokumentierte Regel für den Fall, dass das neue
   Layout weniger Zonen hat als das alte.
3. **Zuletzt benutzte Zone je Anwendung merken**, damit ein neu geöffnetes
   Fenster dort erscheint, wo es zuletzt lag.
4. **Regeln pro Anwendung**, die über eine Ausschlussliste hinausgehen: Treffer
   auf Prozessname, Fensterklasse oder Titel, wahlweise exakt, mit Präfix,
   Suffix oder Ausdruck, und sowohl «nie einrasten» als auch «immer in Zone N».
5. **Standardlayout je Monitorausrichtung**, damit ein neu angeschlossener
   Bildschirm sofort etwas Sinnvolles zeigt.
6. **Benannte Fensteranordnungen**, die die Position aller offenen Fenster
   sichern und per Tastenkombination wiederherstellen.

Ausdrücklich nicht geplant sind Eingriffe, die einen Treiber, einen
Windows-Dienst oder Code-Injektion erfordern, etwa zusätzliche Schaltflächen in
fremden Titelleisten.

## Einschränkungen

- Nur Windows 11 x64.
- Wird die Windows-UAC-Abfrage abgebrochen, startet die Anwendung nicht.
- Erhöht laufende Fenster lassen sich nur positionieren, wenn auch Sascha’s Zone Manager erhöht läuft.
- Nicht rechteckige Zonen, virtuelle Desktops, Fensterregeln und automatische Updates sind noch nicht enthalten.
- Eigene Layouts können nicht über eine dokumentierte API in das native Windows-Snap-Popup eingefügt werden; die Anwendung verwendet ein eigenes Overlay.
- Der Prototyp ist nicht digital signiert und kann beim ersten Start eine Windows-Sicherheitswarnung auslösen.

## Entwicklung und Prüfung

Voraussetzung ist das .NET 8 SDK. Der vollständige Prüf- und Publish-Lauf lautet:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify.ps1
```

Das Skript erzeugt das Mehrgrössen-Icon, stellt Pakete wieder her, führt alle Tests aus, baut Release, veröffentlicht eine selbständige Einzeldatei für `win-x64`, kopiert `SaschaZoneManager.exe` ins Rootverzeichnis und prüft Diagnose sowie Per-Monitor-DPI ohne aktivierten Hook.

Auch ein normaler `dotnet build` oder Build in Visual Studio veröffentlicht nach erfolgreicher Kompilierung automatisch eine selbständige `win-x64`-Einzeldatei als `SaschaZoneManager.exe` direkt ins Rootverzeichnis. Eine dort noch laufende Vorgängerversion wird atomar ersetzt und bis zu ihrem Prozessende als ignorierte Sicherungsdatei beibehalten.
