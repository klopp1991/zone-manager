# Sascha’s Zone Manager — Prüfbericht

Stand: 31.08.2026

## Was in diesem Durchgang geändert wurde

- Vorarbeiten der Konsolidierung: Solutionpfad in `scripts\verify.ps1`, `-p:SkipRootExecutablePublish=true`, Einhängen von `scripts\verify-root-build.ps1`, Entfernen der wirkungslosen `$LASTEXITCODE`-Prüfungen hinter PowerShell-Skripten, Umbenennung der Assets und Ausgabedokumente, `ProductInfo.InstanceKey`, `.gitignore`.
- AP5: Die Root-EXE ist nicht mehr versioniert; der Repositoryzustand steht im README.
- AP1: Vorprüfung der Erhöhung ohne Prozessstart, neuer Status `ContinueUnelevated`, Hinweisbanner mit erneutem Versuch, Tray-Tooltip, erklärte Platzierungsfehler, `isElevated`/`canElevate`/`elevationReason` in der Diagnose, begrenzte Wartezeit in `scripts\verify-dpi-awareness.ps1`.
- AP3: Laufzeitpfade `%APPDATA%\ZoneManager` und `%LOCALAPPDATA%\ZoneManager\logs\zonemanager.log`, einmalige idempotente Übernahme des alten Ordners, Autostart-Wertname `ZoneManager`, Einzelinstanzschlüssel `ZoneManager`.
- AP2: Projekte, Namespaces, XAML-Verweise und Skriptpfade auf `ZoneManager.*` umbenannt.

## Was geprüft wurde

Der Durchgang lief in einer Linux-Sitzung. Das .NET-8-SDK liess sich aus der Ubuntu-Paketquelle nachinstallieren (8.0.130), jedoch ohne das Windows-Desktop-SDK: `Sdks/Microsoft.NET.Sdk.WindowsDesktop` fehlt darin, und die Bezugsquellen von Microsoft (`builds.dotnet.microsoft.com`, `download.visualstudio.microsoft.com`, `dotnetcli.azureedge.net`) sind von der Netzwerkrichtlinie der Sitzung gesperrt. `ZoneManager.Windows`, `ZoneManager.App` und `ZoneManager.Tests` zielen auf `net8.0-windows` mit WPF und WinForms und sind dort deshalb nicht baubar; ein Versuch endet mit `MSB4019`. **Eine neue `ZoneManager.exe` konnte in dieser Sitzung nicht erzeugt werden.**

Tatsächlich ausgeführt wurde:

- `dotnet build src\ZoneManager.Core\ZoneManager.Core.csproj -c Release`: erfolgreich, 0 Warnungen, 0 Fehler.
- Ersatzprojekt für `net8.0` mit den neuen, oberflächenfreien Quellen (`ElevationCapability`, `ElevationNotice`, `ElevationRuntimeState`, `ElevationStartupService`, `TrayTooltip`, `ConfigurationDirectoryMigration`) und den zugehörigen Testdateien: übersetzt mit `TreatWarningsAsErrors`, 30 Tests, 29 bestanden, 1 fehlgeschlagen. Der Fehlschlag ist umgebungsbedingt: `EnsureElevation_relaunches_a_normal_non_elevated_start_with_all_arguments` erwartet `Path.GetDirectoryName(@"C:\Program Files\ZoneManager.exe")`, was unter Linux leer bleibt. Diese Prüfung bestand vor diesem Durchgang unverändert und ist unter Windows zu bestätigen.
- Ersatzprojekt für `WindowsElevationProbe`, `Advapi32`, `Kernel32` und `WindowsStartupService`: übersetzt mit `TreatWarningsAsErrors`, 0 Warnungen, 0 Fehler.

Nicht übersetzt und damit ungeprüft sind alle Dateien mit WPF- oder WinForms-Bezug, insbesondere `App.xaml.cs`, `Program.cs`, `MainWindow.xaml` samt Code-Behind, `TrayIconService`, `ApplicationController` und `AppRuleCoordinator`.

Zusätzlich statisch geprüft:

- Vollständige Textsuche nach `SnapZones` über `*.cs`, `*.xaml`, `*.csproj`, `*.sln` und `*.ps1`: ausserhalb von `docs\superpowers\**` verbleiben nur die vier bewusst stehengelassenen Altnamen (`ProductInfo.LegacyDataFolderName`, `WindowsStartupService.LegacyValueName`, der Legacy-Pfad im Migrationstest und ein Pfadliteral in `WindowsStartupServiceTests`).
- Abgleich der Projektreferenzen, Solutioneinträge, XAML-`x:Class`- und `clr-namespace`-Verweise sowie der Skriptpfade nach der Umbenennung.
- `git ls-files` enthält keinen Pfad mit `/bin/`, `/obj/` oder der Endung `.exe`.

## Was offen ist

Auf einem Windows-11-x64-Rechner mit .NET 8 SDK ist nachzuholen:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify.ps1
```

Erwartet wird ein Abschluss mit `VERIFY_OK ... rootBuild=passed dpi=passed`. Vor dem Lauf `bin\` und `obj\` aller vier Projekte löschen, weil dort noch generierte Dateien mit den alten Namen liegen können. Die Testanzahl war vor diesem Durchgang 303; vier neue Testklassen und erweiterte Elevationstests kommen hinzu. Die tatsächliche Zahl ist erst nach dem Lauf zu dokumentieren; keine der Zahlen stammt aus einem Lauf dieses Durchgangs.

Ebenfalls offen und nur auf dem Zielsystem prüfbar:

- Start ohne Administratorrechte mit abgebrochener UAC-Abfrage: laufende Anwendung mit sichtbarem Banner statt Programmende.
- Start als Administrator: unverändertes Verhalten.
- `ZoneManager.exe --diagnostics`: läuft ohne Erhöhung und meldet `isElevated`, `canElevate` und `elevationReason`.
- Übernahme einer vorhandenen `%APPDATA%\SnapZones`-Konfiguration und zweiter Start ohne weitere Änderung. Vor dem ersten Test von Hand eine Kopie von `%APPDATA%\SnapZones` sichern.
- Autostart aus- und wieder einschalten: genau ein Registry-Wert `ZoneManager`, kein verbliebener Wert `SnapZones`.

## Letzter bestätigter Lauf

Der letzte tatsächlich bestätigte Prüflauf stammt vom 31.08.2026 vor diesen Änderungen, aus dem Arbeitsverzeichnis von Sascha:

```
VERIFY_OK tests=passed rootBuild=passed dpi=skipped monitors=1 startupLayouts=1 files=3
bytes=71928832 maximumExecutableBytes=100000000
rootExe=T:\PortableApps\ZoneManager\ZoneManager.exe hookRegistered=false settingsChanged=false
```

Die DPI-Prüfung war dort übersprungen. Sie braucht eine interaktive Sitzung und ist noch nachzuholen.
