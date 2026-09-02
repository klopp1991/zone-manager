# Sascha’s Zone Manager

Sascha’s Zone Manager ist ein Windows-11-Programm für frei bearbeitbare Fensterzonen pro Monitor. Beim Ziehen eines geeigneten Fensters zeigt ein eigenes Overlay die aktiven Zonen; beim Loslassen wird das Fenster in der gewählten Zone platziert.

## Schnellstart

Fertige Programmdatei: `ZoneManager.exe` liegt beim jeweils neuesten [Release](https://github.com/klopp1991/zone-manager/releases/latest) und nicht im Repository. Herunterladen und starten genügt; das Programm läuft aus dem Verzeichnis, in dem die Datei liegt. Wer es dauerhaft einrichten will, wählt **Einstellungen → Installation** oder ruft `ZoneManager.exe --install` auf — das kopiert die Datei nach `%ProgramFiles%`, verknüpft sie im Startmenü und trägt sie in «Apps und Features» ein.

Selbst bauen - Voraussetzungen: Windows 11 x64 und das [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
git clone https://github.com/klopp1991/zone-manager.git
Set-Location zone-manager
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify.ps1
 .\ZoneManager.exe
```

Der Prüf- und Publish-Lauf stellt Pakete wieder her, führt die Tests aus, baut eine selbständige `win-x64`-Einzeldatei und prüft Diagnose sowie Per-Monitor-DPI. Der normale Start kommt ohne UAC-Bestätigung aus; Administratorrechte fordert das Programm erst an, wenn es auf ein Fenster trifft, das es sonst nicht bewegen dürfte. `--diagnostics` läuft immer ohne Elevation.

Der Lauf schliesst eine Per-Monitor-DPI-Prüfung ein, die die Oberfläche startet und deshalb eine interaktive Sitzung mit bestätigter UAC-Abfrage braucht. In nicht interaktiven Umgebungen bleibt dieser Schritt sonst an der unbeantworteten Abfrage stehen; `-SkipDpiCheck` überspringt ihn.

## Funktionen

- Layouts und Zonen je Monitor, wahlweise in Prozent oder Pixel; die Einheit wird pro Karte an einer Stelle umgeschaltet
- Zonen per Maus, acht Griffen oder exakten Abständen bearbeiten
- Magnetismus an Monitor- und Zonenkanten; `Alt` deaktiviert ihn temporär
- Mehrere Zonen verbinden: mit gedrückter `Strg`-Taste über sie ziehen, das Fenster belegt ihre gemeinsame Fläche
- Hauptzone: eine Zone als Arbeitszone markieren, in der neue Fenster ohne eigene Regel und ohne gemerkte Position landen
- Overlay auf allen Monitoren, nur auf dem Monitor des Ziehbeginns, oder mitwandernd auf dem Monitor unter dem Mauszeiger
- Sofortige Layoutspeicherung, fünf automatische Backups sowie JSON-Export und -Import
- System-, helles und dunkles Theme, Autostart ohne UAC-Abfrage über eine Anmeldeaufgabe und Not-Aus über `Ctrl + Alt + Shift + F12`, der das Einrasten auch wieder einschaltet
- Statuszeile mit dem Zustand der Snap-Funktion und der letzten Meldung; pausiertes Einrasten lässt sich dort und im Infobereich wieder aktivieren
- Administratorrechte nur auf Bedarf: Start ohne Abfrage, Nachfrage höchstens einmal je Sitzung und nur, wenn ein Fenster sie wirklich verlangt
- Wahlweise ein signierter Fensterhelfer mit `uiAccess`: auch Fenster höher berechtigter Programme rasten ein, ohne dass das Programm selbst je Administratorrechte bekommt
- Monitorerkennung über Displaypfad und EDID-Daten; umgesteckte Monitore werden an Modell und Seriennummer wiedererkannt, Monitorwechsel zur Laufzeit werden ohne Neustart übernommen, und je Monitorkombination bleibt die zuletzt aktive Layoutauswahl gemerkt
- Update aus dem Programm heraus: Suche auf Anstoss oder beim Start, Download nur aus der Release-Ablage über HTTPS mit Prüfung der SHA-256-Prüfsumme (`ZoneManager.exe.sha256`, `ZoneManager.Helper.exe.sha256`), Austausch von Programmdatei und Fensterhelfer als ein Vorgang mit Rückfall auf den alten Stand
- Tastenkürzel für das Vordergrundfenster (`Ctrl + Alt + Links/Rechts`, `Ctrl + Alt + 1..9`, `Ctrl + Alt + Rücktaste`, Zusatztasten wählbar), Rückgängig und Wiederholen im Layouteditor, Zonennummern in Editor und Overlay, Vorschau des Entwurfs auf dem echten Monitor
- Erweiterte Einstellungen für erfahrene Anwender: Toleranzen, Verzögerungen, Overlay-Stil, Schutzgrenzen und Katalogumfang, alle mit sicherem Standard und Zurücksetzen
- Regeln nach Prozess, optionalem Fenstertitel und Fensterklasse mit Ereignis, Verzögerung, Wiederholungen und Zielzone; das Programm wird über den Dateidialog oder aus der Liste der laufenden Programme gewählt
- Ausschlüsse nach denselben Merkmalen: ein ausgeschlossenes Fenster bekommt kein Overlay, rastet nicht ein, wird von keiner Regel bewegt und behält dauerhaft eigene Grösse und Position

## Konfiguration

Die Einstellungen liegen unter `%APPDATA%\SnapZones\settings.json`; Sicherungen liegen als `settings.backup-1.json` bis `settings.backup-5.json` daneben. Importdateien werden vollständig validiert und ersetzen die bestehende Konfiguration erst nach einer Bestätigung.

## Entwicklung

```powershell
dotnet test ZoneManager.sln -c Release
dotnet build ZoneManager.sln -c Release
```

Jeder Build des App-Projekts veröffentlicht anschliessend automatisch die selbständige Root-`ZoneManager.exe`. Für schnelle Zwischenbuilds lässt sich dieser Schritt mit `-p:SkipRootExecutablePublish=true` überspringen.

`scriptserify.ps1` prüft neben Build und Tests auch zwei Dinge am echten System: `verify-dpi-awareness.ps1` startet das Programm und belegt, dass es pro Monitor DPI-bewusst ist; `measure-window-frame.ps1` misst an bereits offenen Fenstern den unsichtbaren Fensterrand, von dem der Ausgleich beim Einrasten abhängt, und bricht ab, wenn er die angenommene Obergrenze überschreitet.

Die Lösung ist in `SnapZones.Core`, `SnapZones.Windows` und `SnapZones.App` geteilt. Tests liegen unter `tests\SnapZones.Tests`; der reproduzierbare Gesamtcheck ist `scripts\verify.ps1`. Die Skripte `scripts\test-new-task-worktree.ps1` und `scripts\test-set-version.ps1` prüfen die Hilfsskripte und laufen ausserhalb von `verify.ps1`.

Die gebaute `ZoneManager.exe` und der daneben liegende `ZoneManager.Helper.exe` im Wurzelverzeichnis sind Build-Artefakte und werden nicht versioniert; sie entstehen bei jedem Build neu und werden nur an Releases angehängt. `scripts\verify.ps1` erneuert beide — sonst bliebe neben einer frischen Programmdatei ein Helfer aus einem früheren Lauf liegen, und genau dieser Stand ginge ins Release.

## Versionen und Releases

Die Version folgt dem Schema `YYYY.MMDD.NN`; `NN` beginnt an jedem Tag bei `01` und zählt je Veröffentlichung des Tages um eins hoch. Das Hauptfenster zeigt sie oben rechts, die Dateieigenschaften der EXE tragen sie als Datei- und Produktversion.

Ein Release entsteht auf `main` mit sauberem Arbeitsbaum:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\publish-release.ps1
```

Das Skript schreibt die nächste Version des Tages nach `Directory.Build.props`, führt `scripts\verify.ps1` aus (`-SkipDpiCheck` reicht den Schalter durch, wenn keine interaktive Sitzung für die UAC-Abfrage der DPI-Prüfung bereitsteht), committet die Versionsdatei, setzt den Tag `v<Version>`, pusht beides und erstellt das GitHub-Release mit vier Anhängen: `ZoneManager.exe`, `ZoneManager.Helper.exe` und je einer Prüfsummendatei `*.sha256`; ohne die Prüfsummendatei lädt das Programm kein Update. Der Fensterhelfer hängt mit am Release, weil das Programm ihn beim Update mit ersetzt — läge nur die Programmdatei bei, liefe nach einem Update eine neue Anwendung gegen einen alten Helfer. Dafür braucht es ein angemeldetes [GitHub CLI](https://cli.github.com/) (`gh auth login`) oder ein Token in `GH_TOKEN`; fehlt beides, bleiben Commit und Tag bestehen und das Skript nennt den Befehl zum Nachholen.

Solange das Repository privat ist, findet die Updatesuche nichts: sie fragt bewusst ohne Anmeldung und ohne Token an, und `releases/latest` antwortet einem anonymen Aufruf bei einem privaten Repository mit `404`. Das Update greift erst, wenn die Releases öffentlich erreichbar sind.

Nur die Version setzen, ohne zu veröffentlichen: `scripts\set-version.ps1` (mit `-WhatIfOnly` als reine Vorschau). Die erzeugte `Directory.Build.props` wird nicht von Hand bearbeitet. Assemblys können keine führenden Nullen speichern; `AssemblyVersion` und `FileVersion` tragen deshalb `2026.831.1`, während die angezeigte Version `2026.0831.01` lautet.

## Sicherheit und Einschränkungen

Die Anwendung verwendet keinen Treiber, keinen Windows-Dienst und keine Code-Injektion. Sie unterstützt nur Windows 11 x64, ist nicht digital signiert und kann deshalb beim ersten Start eine Sicherheitswarnung auslösen. Die Updatefunktion prüft die geladene Datei an Herkunft, Grösse und der SHA-256-Prüfsumme der Veröffentlichung; eine Veröffentlichung ohne `ZoneManager.exe.sha256` wird nicht geladen. Das native Windows-Snap-Popup kann nicht über eine dokumentierte API um eigene Zonen erweitert werden; Sascha’s Zone Manager verwendet dafür ein eigenes Overlay.

Weitere Bedienungs- und Architekturdetails stehen in [docs/README.md](docs/README.md), der [Kurzanleitung](outputs/ZoneManager-Kurzanleitung.md) und dem [Prüfbericht](outputs/ZoneManager-Pruefbericht.md). Für Änderungen an der Oberfläche gelten verbindlich die [UI-Richtlinien](docs/ui-richtlinien.md).

## Mitwirken

Änderungen bitte mit passenden Tests ergänzen und vor einem Commit den vollständigen Lauf ausführen:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify.ps1
```
