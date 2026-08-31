# Sascha’s Zone Manager

Sascha’s Zone Manager erstellt frei bearbeitbare Fensterbereiche pro Monitor. Sobald mindestens ein aktives Layout vorhanden ist, zeigt die Snap-Funktion beim Ziehen eines geeigneten Fensters an der Titelleiste die Bereiche als Overlay; beim Loslassen füllt das Fenster die gewählte Zone.

## Schnellstart

1. `ZoneManager.exe` starten und die Windows-UAC-Abfrage bestätigen. Die Datei kommt entweder aus dem neuesten [Release](https://github.com/klopp1991/zone-manager/releases/latest) oder entsteht im Rootverzeichnis, sobald das Projekt gebaut wird.
2. Unter **Layouts** einen Monitor und eines seiner Layouts wählen oder ein neues Layout erstellen.
3. Die vorhandenen Zonen anpassen und mit **+ Zone** die grösste freie Fläche belegen.
4. Zonen ziehen, über acht Griffe skalieren oder rechts als Zahlen eingeben – wahlweise über Position und Grösse oder über die vier Randabstände. Die **Masseinheit** wird einmal pro Karte auf Prozent oder Pixel gestellt und gilt für alle acht Felder.
5. Die Snap-Funktion läuft mit den aktiven Layouts automatisch; jede gültige Änderung wird sofort gespeichert und angewendet.

Konfiguration und bestehende Installationen bleiben unter `%APPDATA%\SnapZones\settings.json` kompatibel. Die fünf letzten Stände liegen daneben als `settings.backup-1.json` bis `settings.backup-5.json`; bei einer beschädigten Hauptdatei wird die neueste gültige Sicherung automatisch wiederhergestellt. Autostart ist beim ersten Start ausgeschaltet.

**Export** schreibt jederzeit ein vollständiges JSON-Backup mit sämtlichen Einstellungen, Monitorlayouts, Zonen, IDs und Parametern. **Import** validiert die komplette Datei, zeigt den exakten Ersetzungsumfang und sichert den bisherigen Zustand unmittelbar vor der bestätigten Übernahme. Bestehende Profilkonfigurationen aus Schema 1 werden beim Laden in unabhängige Layouts pro Monitor migriert.

## Regeln

Auf der Seite **Regeln** verbindet eine Regel ein Programm mit einer Zielzone. Der Editor führt in vier nummerierten Gruppen durch die Eingabe:

1. **Programm** — Prozesspfad oder Programmname. Zwei Wege führen dorthin: **Programmdatei wählen …** öffnet den Dateidialog und eignet sich auch für Programme, die gerade nicht laufen; **Laufendes Programm wählen …** listet die aktuell laufenden Programme mit sichtbarem Fenster samt Fenstertitel und Pfad und ist durchsuchbar. Läuft ein Programm mit erhöhten Rechten, gibt Windows den Pfad nicht preis; dann wird der Programmname übernommen, was für die Regel genügt.
2. **Fenster eingrenzen (optional)** — Titelmuster vergleicht einen Teil des Fenstertitels ohne Rücksicht auf Gross- und Kleinschreibung; Fensterklasse vergleicht den internen Windows-Fenstertyp wie `CabinetWClass`. Leer bedeutet jeweils: die Regel gilt für jedes Fenster des Programms. `*` und `?` dienen als Platzhalter.
3. **Auslöser** — Ereignis, Verzögerung von 0 bis 30000 Millisekunden, 0 bis 3 Wiederholungen, Priorität von 0 bis 100. Unter der Zeile erklärt ein Hinweisfeld das gewählte Ereignis im Klartext: **Fenster wird geöffnet** greift einmalig beim Erscheinen eines neuen Fensters, **Fenster erhält den Fokus** jedes Mal beim Wechsel zu einem passenden Fenster, **Layout wird aktiviert** ordnet beim Layoutwechsel alle bereits offenen passenden Fenster neu an.
4. **Ziel** — Ziellayout und Zielzone; das Layout bestimmt zugleich den Monitor.

Vor jedem Platzierungsversuch werden Fensteridentität, Regel und Ziel erneut geprüft. Fehlende Layouts, Monitore oder Zonen pausieren die Regel sichtbar; es gibt keinen stillen Fallback und Regeln starten keine Programme.

## Layouteditor

- **+ Zone** belegt die grösste freie achsenparallele Fläche; ohne ausreichenden freien Bereich wird nichts verändert.
- Zonen docken innerhalb der eingestellten Magnetdistanz an Monitor- und Zonenkanten an; `Alt` deaktiviert den Magnetismus während des Ziehens.
- Die Karte **Ausgewählte Zone** schaltet die **Masseinheit** an einer einzigen Stelle um; die Umschaltung gilt gemeinsam für alle acht Zahlenfelder. **Prozent** bleibt bei Auflösungsänderungen proportional; **Pixel** bezieht sich auf die aktuelle Windows-Arbeitsfläche des Monitors.
- **Position und Grösse** bearbeitet X, Y, Breite und Höhe; **Abstände zum Rand** beschreibt dieselbe Zone von den vier Rändern aus.
- Überlappende, zu kleine oder ausserhalb liegende Zonen werden markiert und können nicht gespeichert werden.

## Monitore

Auf der Seite **Monitore** wählt die Liste links den Monitor, der im ganzen Programm als aktiver Monitor gilt. **Nach oben** und **Nach unten** ändern die Reihenfolge, **Monitore identifizieren** blendet den verwendeten Namen drei Sekunden lang auf jedem Bildschirm ein. Rechts wird der Monitor umbenannt; ein leerer Name stellt die automatische Bezeichnung wieder her. Monitornamen werden bevorzugt aus dem aktiven Displaypfad und den EDID-Daten gelesen.

## Skalierung

Die Seite **Skalierung** liest die erkannten Werte des gewählten Monitors aus — Anzeigeskalierung, Auflösung, Arbeitsfläche und, sofern Windows die EDID liefert, die Bildschirmdiagonale — und öffnet die zuständige Windows-Seite.

Ändern lassen sich diese Werte nur in Windows selbst. Windows 11 stellt normalen Desktopanwendungen keine unterstützte Schnittstelle bereit, um Anzeigeskalierung, Textskalierung oder monitorweise Taskleisten- und Icongrössen zu setzen. Benutzerdefinierte Windows-Skalierung von 100 bis 500 % und Textskalierung von 100 bis 225 % sind zudem globale Windows-Einstellungen. Sascha’s Zone Manager verwendet dafür bewusst keine Explorer-Injektion, keine privaten DPI-Pakete und keine undokumentierten Registry-Werte; die Seite bleibt deshalb lesend.

## Einstellungen

- System-, helles oder dunkles Theme; Systemänderungen werden ohne Neustart übernommen.
- Overlay auf allen Monitoren oder nur auf dem aktiven Monitor.
- Sofortige Aktivierung oder Aktivierung mit Umschalttaste.
- **Overlay-Abstände**: Aussenabstände links, oben, rechts und unten in Pixel, Zonenabstand und Magnetdistanz in ganzen Prozent. Diese Werte betreffen ausschliesslich die Vorschau beim Ziehen; wo ein Fenster tatsächlich landet, legt das Layout unter **Abstände zum Rand** fest. Neben jedem Prozentregler steht der abgeleitete Pixelwert als `≙ n px`.
- Overlayfarbe, Deckkraft und ein-/ausblendbare Zonennamen.
- Autostart pro Benutzer; die Windows-UAC-Abfrage muss auch beim Login bestätigt werden.

Jede Einstellung erklärt direkt in der Oberfläche Wirkung, Gültigkeitsbereich und Einschränkungen. Wie Titel, Beschriftungen und Hilfetexte dabei aufgebaut sind, steht verbindlich in [ui-richtlinien.md](ui-richtlinien.md).

## Sicherheit und Not-Aus

Normale Programmstarts wechseln vor dem Laden der Oberfläche über die Windows-UAC-Abfrage in den Administratormodus. Dadurch kann die Anwendung auch erhöhte Fenster positionieren; der reine Diagnosemodus bleibt absichtlich ohne Elevation. `Ctrl + Alt + Shift + F12` deaktiviert Hook und Overlays bis zum nächsten Programmstart. `Escape` beendet nur den aktuellen Ziehvorgang. Die Anwendung enthält keinen Treiber, keinen Windows-Dienst und keine Code-Injektion; ein Schutzschalter stoppt die Snap-Funktion bei Callback-Fehlern oder ungewöhnlich vielen Hook-Ereignissen.

## Beenden

Das Schliessen des Fensters blendet die Anwendung nur in den Infobereich aus. Beendet wird sie über **Rechtsklick auf das Infobereichssymbol → Beenden**.

Beim Beenden werden zuerst Hooks, Zeitgeber und die Platzierungs-Engine stillgelegt, damit keine neue Arbeit mehr anfällt; anschliessend werden Einstellungen und Fensterplatzierungen gespeichert. Für diesen Abschluss gilt eine Zeitgrenze von fünf Sekunden. Lässt sich in dieser Zeit nicht vollständig speichern, meldet ein Hinweisfenster die Ursache und fragt, ob trotzdem beendet werden soll — die Anwendung bleibt nie ohne sichtbare Begründung geöffnet.

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

## Version und Releases

Die Version folgt dem Schema `YYYY.MMDD.NN`. `NN` beginnt an jedem Tag bei `01` und zählt je Veröffentlichung des Tages um eins hoch, sodass jede Auslieferung an ihrem Namen erkennbar datiert ist. Die Kopfzeile des Hauptfensters zeigt die Version rechts neben dem Programmnamen; dieselbe Angabe steht in den Dateieigenschaften der EXE.

`Directory.Build.props` hält die Werte für alle Projekte und wird ausschliesslich von `scripts\set-version.ps1` geschrieben. Die Anzeigeform mit führender Null (`2026.0831.01`) steht in `ZoneManagerVersion` und `InformationalVersion`; `AssemblyVersion` und `FileVersion` tragen die numerische Form `2026.831.1`, weil Assemblyversionen keine führenden Nullen speichern können. Die Anwendung liest ausschliesslich die `InformationalVersion` und schneidet ein etwaiges Metadatensuffix ab.

`scripts\publish-release.ps1` führt den vollständigen Weg aus: Version schreiben, `scripts\verify.ps1` ausführen, `Directory.Build.props` committen, Tag `v<Version>` setzen, Commit und Tag pushen und das GitHub-Release mit `ZoneManager.exe` als Anhang erstellen. Das Skript arbeitet nur auf `main` und nur bei sauberem Arbeitsbaum und reicht `-SkipDpiCheck` an den Prüflauf durch; ohne angemeldetes GitHub CLI oder `GH_TOKEN` endet es nach dem Push und nennt den Befehl für das Release.

Die EXE wird bewusst nicht versioniert, sondern nur an Releases angehängt: Sie ist ein reproduzierbares Build-Artefakt von rund 72 MB, das die Repository-Historie sonst mit jeder Auslieferung dauerhaft vergrössern würde.

## Entwicklung und Prüfung

Voraussetzung ist das .NET 8 SDK. Der vollständige Prüf- und Publish-Lauf lautet:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify.ps1
```

Das Skript erzeugt das Mehrgrössen-Icon, stellt Pakete wieder her, führt alle Tests aus, baut Release, veröffentlicht eine selbständige Einzeldatei für `win-x64`, kopiert `ZoneManager.exe` ins Rootverzeichnis und prüft Diagnose sowie Per-Monitor-DPI ohne aktivierten Hook.

Der Lauf schliesst eine Per-Monitor-DPI-Prüfung ein, die die Oberfläche startet und deshalb eine interaktive Sitzung mit bestätigter UAC-Abfrage braucht. In nicht interaktiven Umgebungen bleibt dieser Schritt sonst an der unbeantworteten Abfrage stehen; `-SkipDpiCheck` überspringt ihn.

Auch ein normaler `dotnet build` oder Build in Visual Studio veröffentlicht nach erfolgreicher Kompilierung automatisch eine selbständige `win-x64`-Einzeldatei als `ZoneManager.exe` direkt ins Rootverzeichnis. Eine dort noch laufende Vorgängerversion wird atomar ersetzt und bis zu ihrem Prozessende als ignorierte Sicherungsdatei beibehalten.

Dieser Schritt kostet bei jedem Build einen vollständigen Self-contained-Publish. Für schnelle Zwischenbuilds und in Prüfläufen, die die Root-EXE separat erzeugen, lässt er sich mit `-p:SkipRootExecutablePublish=true` überspringen; `scripts\verify-root-build.ps1` prüft den impliziten Weg gezielt in einem Wegwerfverzeichnis unter `work\`.

Die Skripttests laufen ausserhalb von `verify.ps1` und legen dafür je ein temporäres Repository an: `scripts\test-new-task-worktree.ps1` prüft die Worktree-Erstellung, `scripts\test-set-version.ps1` das Versionsschema samt Tageswechsel und Tag-Erkennung.
