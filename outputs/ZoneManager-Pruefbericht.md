# Sascha’s Zone Manager — Prüfbericht

Stand: 31.08.2026

## Was in diesem Durchgang geändert wurde

- Vorarbeiten der Konsolidierung: Solutionpfad in `scripts\verify.ps1`, `-p:SkipRootExecutablePublish=true`, Einhängen von `scripts\verify-root-build.ps1`, Entfernen der wirkungslosen `$LASTEXITCODE`-Prüfungen hinter PowerShell-Skripten, Umbenennung der Assets und Ausgabedokumente, `ProductInfo.InstanceKey`, `.gitignore`.
- AP5: Die Root-EXE ist nicht mehr versioniert; der Repositoryzustand steht im README.
- AP1: Vorprüfung der Erhöhung ohne Prozessstart, neuer Status `ContinueUnelevated`, Hinweisbanner mit erneutem Versuch, Tray-Tooltip, erklärte Platzierungsfehler, `isElevated`/`canElevate`/`elevationReason` in der Diagnose, begrenzte Wartezeit in `scripts\verify-dpi-awareness.ps1`.
- AP3: Laufzeitpfade `%APPDATA%\ZoneManager` und `%LOCALAPPDATA%\ZoneManager\logs\zonemanager.log`, einmalige idempotente Übernahme des alten Ordners, Autostart-Wertname `ZoneManager`, Einzelinstanzschlüssel `ZoneManager`.
- AP2: Projekte, Namespaces, XAML-Verweise und Skriptpfade auf `ZoneManager.*` umbenannt.

## Was geprüft wurde

Nichts davon wurde gebaut oder ausgeführt. Der Durchgang lief in einer Linux-Sitzung ohne .NET SDK und ohne Windows; `SnapZones.Windows`, `SnapZones.App` und das Testprojekt zielen auf `net8.0-windows` mit WPF und WinForms und sind dort grundsätzlich nicht baubar. Der Versuch, das SDK nachzuinstallieren, scheiterte an der Netzwerkrichtlinie der Sitzung (`builds.dotnet.microsoft.com` per CONNECT abgelehnt).

Geprüft wurde ausschliesslich statisch:

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
