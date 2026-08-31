# Sascha’s Zone Manager

Sascha’s Zone Manager ist ein Windows-11-Programm für frei bearbeitbare Fensterzonen pro Monitor. Beim Ziehen eines geeigneten Fensters zeigt ein eigenes Overlay die aktiven Zonen; beim Loslassen wird das Fenster in der gewählten Zone platziert.

## Schnellstart

Voraussetzungen: Windows 11 x64 und das [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
git clone https://github.com/klopp1991/zone-manager.git
Set-Location zone-manager
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify.ps1
 .\ZoneManager.exe
```

Der Prüf- und Publish-Lauf stellt Pakete wieder her, führt die Tests aus, baut eine selbständige `win-x64`-Einzeldatei, legt sie als `ZoneManager.exe` ins Rootverzeichnis und prüft Diagnose, Root-Build sowie Per-Monitor-DPI. In einer nicht interaktiven Sitzung `-SkipDpiCheck` ergänzen; die DPI-Prüfung startet die Oberfläche und braucht eine interaktive Sitzung.

Beim Start prüft die Anwendung zuerst ohne Prozessstart, ob sie sich selbst erhöhen kann, und fordert nur dann Administratorrechte an. Ist das nicht möglich oder wird die UAC-Abfrage abgebrochen, läuft sie unerhöht weiter und weist das in der Oberfläche und im Infobereich aus; Fenster von Programmen mit höheren Rechten lassen sich dann nicht positionieren. `--diagnostics` läuft weiterhin absichtlich ohne Elevation und meldet zusätzlich `isElevated` und `canElevate`.

`ZoneManager.exe` wird nicht im Repository mitgeliefert, sondern beim Bau erzeugt und ist in `.gitignore` ausgeschlossen.

## Repository

Das Projekt liegt unter Git; das Remote ist `https://github.com/klopp1991/zone-manager.git`. Der Standardbranch ist `main`. Nach `AGENTS.md` arbeitet jede Aufgabe auf einem eigenen Branch in einem eigenen Worktree (`scripts\new-task-worktree.ps1 -TaskName "<name>"`); der Haupt-Worktree bleibt für Bestandsaufnahme und Integration reserviert. Nicht versioniert sind Build-Artefakte (`bin/`, `obj/`, `work/`, `outputs/ZoneManager-prototype/`, `outputs/zonemanager-diagnostics.json`) sowie die erzeugte Root-EXE.

## Funktionen

- Layouts und Zonen je Monitor mit Prozent- oder Pixelmassen
- Zonen per Maus, acht Griffen oder exakten Abständen bearbeiten
- Magnetismus an Monitor- und Zonenkanten; `Alt` deaktiviert ihn temporär
- Overlay auf allen Monitoren oder nur auf dem aktiven Monitor
- Sofortige Layoutspeicherung, fünf automatische Backups sowie JSON-Export und -Import
- System-, helles und dunkles Theme, Autostart und Not-Aus über `Ctrl + Alt + Shift + F12`
- Monitorerkennung über Displaypfad und EDID-Daten
- App-Regeln nach Prozess, optionalem Fenstertitel und Fensterklasse mit Ereignis, Verzögerung, Wiederholungen und Zielzone

## Konfiguration

Die Einstellungen liegen unter `%APPDATA%\ZoneManager\settings.json`; Sicherungen liegen als `settings.backup-1.json` bis `settings.backup-5.json` daneben. Protokolle liegen unter `%LOCALAPPDATA%\ZoneManager\logs\zonemanager.log`. Importdateien werden vollständig validiert und ersetzen die bestehende Konfiguration erst nach einer Bestätigung.

Ältere Versionen verwendeten den Ordner `SnapZones`. Beim ersten Start wird der Inhalt von `%APPDATA%\SnapZones` einmalig übernommen, sofern am neuen Ort noch nichts liegt; der alte Ordner bleibt unverändert als Rückfallebene erhalten. Eine bereits vorhandene neue Konfiguration hat Vorrang und wird nicht überschrieben.

## Entwicklung

```powershell
dotnet test ZoneManager.sln -c Release -p:SkipRootExecutablePublish=true
dotnet build ZoneManager.sln -c Release -p:SkipRootExecutablePublish=true
```

Die Lösung ist in `ZoneManager.Core`, `ZoneManager.Windows` und `ZoneManager.App` geteilt. Tests liegen unter `tests\ZoneManager.Tests`; der reproduzierbare Gesamtcheck ist `scripts\verify.ps1`.

`-p:SkipRootExecutablePublish=true` unterdrückt den Self-contained-Publish, den ein Build des App-Projekts sonst bei jedem Durchlauf auslöst. Ohne diesen Schalter entsteht die rund 72 MB grosse Root-EXE mehrfach je Prüflauf.

## Sicherheit und Einschränkungen

Die Anwendung verwendet keinen Treiber, keinen Windows-Dienst und keine Code-Injektion. Sie unterstützt nur Windows 11 x64, ist nicht digital signiert und kann deshalb beim ersten Start eine Sicherheitswarnung auslösen. Das native Windows-Snap-Popup kann nicht über eine dokumentierte API um eigene Zonen erweitert werden; Sascha’s Zone Manager verwendet dafür ein eigenes Overlay.

Weitere Bedienungs- und Architekturdetails stehen in [docs/README.md](docs/README.md), der [Kurzanleitung](outputs/ZoneManager-Kurzanleitung.md) und dem [Prüfbericht](outputs/ZoneManager-Pruefbericht.md).

## Mitwirken

Änderungen bitte mit passenden Tests ergänzen und vor einem Commit den vollständigen Lauf ausführen:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify.ps1
```
