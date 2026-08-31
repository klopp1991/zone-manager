# Sascha’s Zone Manager

Sascha’s Zone Manager ist ein Windows-11-Programm für frei bearbeitbare Fensterzonen pro Monitor. Beim Ziehen eines geeigneten Fensters zeigt ein eigenes Overlay die aktiven Zonen; beim Loslassen wird das Fenster in der gewählten Zone platziert.

## Schnellstart

Voraussetzungen: Windows 11 x64 und das [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
git clone https://github.com/klopp1991/zone-manager.git
Set-Location zone-manager
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify.ps1
.\SaschaZoneManager.exe
```

Der Prüf- und Publish-Lauf stellt Pakete wieder her, führt die Tests aus, baut eine selbständige `win-x64`-Einzeldatei und prüft Diagnose sowie Per-Monitor-DPI. Beim normalen Start ist eine Windows-UAC-Bestätigung erforderlich; `--diagnostics` läuft absichtlich ohne Elevation.

## Funktionen

- Layouts und Zonen je Monitor mit Prozent- oder Pixelmassen
- Zonen per Maus, acht Griffen oder exakten Abständen bearbeiten
- Magnetismus an Monitor- und Zonenkanten; `Alt` deaktiviert ihn temporär
- Overlay auf allen Monitoren oder nur auf dem aktiven Monitor
- `Ctrl` beim Ziehen fasst mehrere Zonen zu einem Fensterbereich zusammen
- Einstellungsseite mit dauerhaft sichtbaren Erklärungen, ausklappbarer
  Detailhilfe, Suche, Zurücksetzen je Einstellung und Live-Vorschau des Overlays
- Sofortige Layoutspeicherung, fünf automatische Backups sowie JSON-Export und -Import
- System-, helles und dunkles Theme, Autostart und Not-Aus über `Ctrl + Alt + Shift + F12`
- Monitorerkennung über Displaypfad und EDID-Daten

## Konfiguration

Die Einstellungen liegen unter `%APPDATA%\SnapZones\settings.json`; Sicherungen liegen als `settings.backup-1.json` bis `settings.backup-5.json` daneben. Importdateien werden vollständig validiert und ersetzen die bestehende Konfiguration erst nach einer Bestätigung.

## Entwicklung

```powershell
dotnet test SnapZones.sln -c Release
dotnet build SnapZones.sln -c Release
```

Die Lösung ist in vier Projekte geteilt:

| Projekt | Zielframework | Inhalt |
| --- | --- | --- |
| `SnapZones.Core` | `net8.0` | Modelle, Geometrie, Layouts, Persistenz, Einstellungskatalog |
| `SnapZones.Presentation` | `net8.0` | ViewModels und darstellende Services, ohne WPF |
| `SnapZones.Windows` | `net8.0-windows` | Win32-Interop: Monitore, Hooks, Hotkeys, Autostart |
| `SnapZones.App` | `net8.0-windows` (WPF) | Fenster, Overlays, Themes, Anwendungssteuerung |

Die Tests sind entsprechend zweigeteilt:

- `tests\SnapZones.Tests` enthält die Logiktests. Sie hängen an keiner
  WPF-Laufzeit und laufen deshalb auch auf einem Linux-Agent:

  ```bash
  dotnet test tests/SnapZones.Tests/SnapZones.Tests.csproj -c Release -p:EnableWindowsTargeting=true
  ```

- `tests\SnapZones.Tests.Windows` enthält die Tests für WPF, Themes und den
  nativen Desktop. Sie benötigen Windows.

Der reproduzierbare Gesamtcheck bleibt `scripts\verify.ps1` und führt beide
Suiten aus. `.github/workflows/ci.yml` fährt die Logiktests unter Linux und die
vollständige Prüfung unter Windows.

## Sicherheit und Einschränkungen

Die Anwendung verwendet keinen Treiber, keinen Windows-Dienst und keine Code-Injektion. Sie unterstützt nur Windows 11 x64, ist nicht digital signiert und kann deshalb beim ersten Start eine Sicherheitswarnung auslösen. Das native Windows-Snap-Popup kann nicht über eine dokumentierte API um eigene Zonen erweitert werden; Sascha’s Zone Manager verwendet dafür ein eigenes Overlay.

Weitere Bedienungs- und Architekturdetails stehen in [docs/README.md](docs/README.md), der [Kurzanleitung](outputs/SnapZones-Kurzanleitung.md) und dem [Prüfbericht](outputs/SnapZones-Pruefbericht.md).

## Mitwirken

Änderungen bitte mit passenden Tests ergänzen und vor einem Commit den vollständigen Lauf ausführen:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify.ps1
```
