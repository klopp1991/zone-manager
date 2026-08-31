# Sascha’s Zone Manager — Prüfbericht

Stand: 01.09.2026 · Lauf: `dotnet test ZoneManager.sln -c Release` · Abschluss: bestanden

Geprüfte Version: Arbeitsstand nach **2026.0831.01**; die Korrekturen am Beenden-Ablauf, am Zonenspalt, an der Regelidentität und an verwaisten Monitoren sowie die neuen Ausschlüsse sind noch nicht als Release veröffentlicht.

## Bestanden

- Release-Build für Windows 11 x64: 0 Fehler, 0 Warnungen (`TreatWarningsAsErrors` in allen Projekten aktiv).
- 413 automatisierte Tests: 413 bestanden, 0 fehlgeschlagen, 0 übersprungen.
- Ausgleich des unsichtbaren Fensterrahmens: `WindowFrameCompensationTests` prüft, dass zwei Fenster in aneinandergrenzenden Zonen bündig sitzen, und dass unplausible Messwerte das Ziel unverändert lassen.
- Regelidentität: `AppRuleDisplayNameTests` hält fest, dass die Auswahl laufender Programme den Dateinamen übernimmt und dieser vor wie nach einem Versionswechsel trifft, der vollständige Pfad dagegen nicht.
- Nicht verbundene Monitore: `DisconnectedMonitorTests` deckt Anzeige, Löschen des letzten Layouts samt Verschwinden des Monitors und den Schutz des letzten Layouts verbundener Monitore ab.
- Beenden über den Infobereich: Der Ablauf legt Hooks und Platzierungs-Engine still, bevor er speichert, wartet höchstens fünf Sekunden und meldet einen Fehlschlag sichtbar. Abgedeckt durch `ShutdownSaveTests` (Erfolg, Zeitüberschreitung, Speicherfehler, begrenzter Zusatz-Flush) und `TrayIconServiceTests` (das Kontextmenü wird nie ersetzt oder verworfen, «Beenden» bleibt klickbar).
- Versionsschema `YYYY.MMDD.NN`: `Directory.Build.props` trägt Dateiversion `2026.831.1` und Produktversion `2026.0831.01`. `scripts\test-set-version.ps1` läuft ausserhalb dieses Laufs und ist zuletzt zu `2026.0831.01` bestanden.
- Ausschlüsse: `AppExclusionTests` deckt den Vergleich nach Dateiname, vollständigem Pfad, Titelmuster und Fensterklasse ab, ebenso den abgeschalteten Ausschluss, den Ausschluss ohne Merkmal und das Fenster ohne lesbaren Programmpfad. Im Ziehpfad ist festgehalten, dass ein ausgeschlossenes Fenster weder Overlay noch Ziehzustand erhält, ein nicht ausgeschlossenes dagegen weiterhin einrastet. `AppRuleCoordinatorTests` hält fest, dass ein Ausschluss jede Regel schlägt und ein abgeschalteter Ausschluss die Regel unberührt lässt. `ConfigurationExclusionPersistenceTests` deckt den Schemawechsel 4 auf 5, den Speicher-Lade-Umlauf und die Abweisung ungültiger Einträge ab. `AppExclusionsPresentationTests` prüft Bindungen, Hilfetexte, gleiche Schaltflächenhöhe und die Position der Seite hinter den Regeln.
- Zonen verbinden: `ZoneSpanningTests` deckt die Hüllbox-Rechnung samt leerem Rechteck, das Aufsammeln der überstrichenen Zonen, die gemeinsame Hervorhebung, das wiederholte Überfahren einer bereits gewählten Zone, den Rückfall auf eine einzelne Zone beim Loslassen der Taste, die Beschränkung auf einen Monitor, den Abbruch und das Übergehen inzwischen gelöschter Zonen ab.
- Autostart: `ScheduledTaskStartupServiceTests` deckt die Aufgabendefinition ab — erhöhter Start ohne Abfrage, Anmeldeauslöser, aufgehobene Akku- und Laufzeitgrenzen, XML-Maskierung —, ausserdem Anlegen und Entfernen der Aufgabe, die gemeldete Ursache eines Fehlschlags, die Abweisung einer Aufgabe mit fremdem Programmpfad sowie den Vorrang der Aufgabe vor dem Registry-Eintrag samt Rückfall.
- Gemerkte Fensterpositionen: `RememberedWindowPositionsTests` hält fest, dass die Funktion voreingestellt eingeschaltet bleibt, den Umlauf durch die Einstellungen übersteht, die Anzahl in Einzahl und Mehrzahl richtig benennt, das Verwerfen nur als Anfrage stellt und dass Schalter, Anzahl und Schaltfläche in den Einstellungen gebunden sind.
- Oberflächenüberarbeitung nach `docs\ui-richtlinien.md` automatisiert abgedeckt: einheitliche Steuerelementhöhe, gleiche gemessene Höhe benachbarter Schaltflächen auf den Seiten Monitore und Regeln, Texthierarchie, Kontrast von `SubtleInkBrush` in hellem und dunklem Theme, gemeinsamer Einheitenumschalter im Layouteditor, ganze Prozentschritte der Regler sowie die neue Auswahl laufender Programme.

## Nicht abgedeckt in diesem Lauf

- **Der vollständige Prüflauf `scripts\verify.ps1` wurde für diesen Stand nicht ausgeführt.** Geprüft sind Release-Build ohne Warnungen und die Testsuite. Publish, Root-EXE samt SHA-256-Vergleich, `scripts\verify-root-build.ps1`, Icon-Erzeugung und Diagnoselauf stehen aus; die zuletzt dafür festgehaltenen Werte stammen aus dem Lauf zu `2026.0831.01` und gelten nicht für diesen Stand.
- **Die Wirkung der Ausschlüsse in der Platzierungs-Engine ist nicht automatisiert abgedeckt.** `WindowPlacementEngine` besitzt keine Unittests; dass ein ausgeschlossenes Fenster weder platziert noch in den Positionskatalog aufgenommen wird, ist nur im Code umgesetzt und nicht am laufenden Programm nachgestellt.
- **Die neue Seite «Ausschlüsse» wurde nicht am laufenden Programm in Augenschein genommen.** Alle Aussagen zur Darstellung stammen aus den automatisierten WPF-Tests.
- **Das Verbinden mehrerer Zonen wurde nicht am laufenden Programm erprobt.** Koordinator, Hüllbox und Befehl sind durch Tests abgedeckt; die Auswertung der Strg-Taste über `GetAsyncKeyState` und die gemeinsame Hervorhebung im Overlay sind nur im Code umgesetzt.
- **Der Autostart wurde nicht am echten System eingerichtet.** Definition und Ablauf sind durch Tests abgedeckt; dass `schtasks.exe` die Aufgabe annimmt und dass die Anmeldung anschliessend ohne UAC-Abfrage startet, ist nicht nachgestellt. Ebenso ungeprüft ist die Umstellung eines bestehenden Registry-Autostarts auf die Aufgabe.
- **Das Abschalten und Verwerfen des Positionskatalogs ist nicht am laufenden Programm erprobt.** Die Prüfungen im Platzierungs-Modul selbst — dass ein abgeschalteter Katalog weder anwendet noch aufnimmt und dass `ForgetAll` die Datei leert — sind mangels Unittests für `WindowPlacementEngine` nur im Code umgesetzt.
- **Die tote Regelwelt `WindowPlacementRule` besteht weiter.** `PlacementRuleResolver` wird im Platzierungs-Engine an drei Stellen mit einer stets leeren Regelliste aufgerufen; die Zweige für `FixedZone` und `Exclude` sind dadurch unerreichbar. Die Ausschlüsse laufen bewusst über einen eigenen, aktiven Pfad. Der Rückbau steht aus.
- **Per-Monitor-DPI-Prüfung übersprungen** (`dpi=skipped`). `scripts\verify-dpi-awareness.ps1` startet die Oberfläche, die sich selbst über die Windows-UAC-Abfrage erhöht. In einer nicht interaktiven Sitzung bleibt der Start an der unbeantworteten Abfrage stehen. Für die vollständige Prüfung `scripts\verify.ps1` ohne `-SkipDpiCheck` in einer interaktiven Sitzung ausführen und die UAC-Abfrage bestätigen.
- **Die überarbeitete Oberfläche wurde nicht am laufenden Programm in Augenschein genommen.** Alle Aussagen zur Darstellung stammen aus den automatisierten WPF-Tests, nicht aus einem Sichttest. Insbesondere die Auswahl **Laufendes Programm wählen …** wurde nur mit einer eingespeisten Prozessliste geprüft; die tatsächliche Systemabfrage `RunningProcessCatalog.FromSystem` ist ungeprüft.
- **Der gemessene Fensterrahmen ist ungeprüft.** Die Rechnung ist durch Tests abgedeckt, der tatsächliche Randwert kommt jedoch erst zur Laufzeit vom Desktop Window Manager und wurde nicht am echten Fenster nachgemessen.
- **Titelleisten-Erkennung nicht manuell nachgestellt.** Der frühere Blocker — `IsTitleBarDrag = false` bei einem leeren Windows-11-Notepad — ist im Code adressiert: `WindowsWindowService.IsTitleBarDrag` fragt zunächst `WM_NCHITTEST` mit Timeout ab und fällt nur bei ausbleibender Antwort auf eine geometrische Prüfung zurück. Ein erneuter manueller Hook-Test mit Notepad steht aus.
- **Das GitHub-Release zu `v2026.0831.01` ist noch nicht erstellt.** Commit und Tag sind gepusht, die EXE liegt gebaut im Rootverzeichnis; das Anhängen als Release-Asset braucht ein angemeldetes GitHub CLI (`gh auth login`).
- **Die Versionsanzeige in der Kopfzeile wurde nicht am laufenden Programm in Augenschein genommen**, nur der ausgelesene Wert der gebauten EXE ist geprüft.
- Der Prototyp ist nicht digital signiert und kann beim ersten Start eine Windows-Sicherheitswarnung auslösen.

## Reproduktion

Dieser Lauf:

```powershell
dotnet test ZoneManager.sln -c Release
```

Der vollständige Release-Lauf:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify.ps1
```
