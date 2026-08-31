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

Der Prüf- und Publish-Lauf stellt Pakete wieder her, führt die Tests aus, baut eine selbständige `win-x64`-Einzeldatei und prüft Diagnose sowie Per-Monitor-DPI. Beim normalen Start ist eine Windows-UAC-Bestätigung erforderlich; `--diagnostics` läuft absichtlich ohne Elevation.

Der Lauf schliesst eine Per-Monitor-DPI-Prüfung ein, die die Oberfläche startet und deshalb eine interaktive Sitzung mit bestätigter UAC-Abfrage braucht. In nicht interaktiven Umgebungen bleibt dieser Schritt sonst an der unbeantworteten Abfrage stehen; `-SkipDpiCheck` überspringt ihn.

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

Die Einstellungen liegen unter `%APPDATA%\SnapZones\settings.json`; Sicherungen liegen als `settings.backup-1.json` bis `settings.backup-5.json` daneben. Importdateien werden vollständig validiert und ersetzen die bestehende Konfiguration erst nach einer Bestätigung.

## Entwicklung

```powershell
dotnet test ZoneManager.sln -c Release
dotnet build ZoneManager.sln -c Release
```

Jeder Build des App-Projekts veröffentlicht anschliessend automatisch die selbständige Root-`ZoneManager.exe`. Für schnelle Zwischenbuilds lässt sich dieser Schritt mit `-p:SkipRootExecutablePublish=true` überspringen.

Die Lösung ist in `SnapZones.Core`, `SnapZones.Windows` und `SnapZones.App` geteilt. Tests liegen unter `tests\SnapZones.Tests`; der reproduzierbare Gesamtcheck ist `scripts\verify.ps1`.

## Sicherheit und Einschränkungen

Die Anwendung verwendet keinen Treiber, keinen Windows-Dienst und keine Code-Injektion. Sie unterstützt nur Windows 11 x64, ist nicht digital signiert und kann deshalb beim ersten Start eine Sicherheitswarnung auslösen. Das native Windows-Snap-Popup kann nicht über eine dokumentierte API um eigene Zonen erweitert werden; Sascha’s Zone Manager verwendet dafür ein eigenes Overlay.

Weitere Bedienungs- und Architekturdetails stehen in [docs/README.md](docs/README.md), der [Kurzanleitung](outputs/ZoneManager-Kurzanleitung.md) und dem [Prüfbericht](outputs/ZoneManager-Pruefbericht.md).

## Mitwirken

Änderungen bitte mit passenden Tests ergänzen und vor einem Commit den vollständigen Lauf ausführen:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify.ps1
```
