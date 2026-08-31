# Auftrag: Konsolidierung Sascha's Zone Manager

Erstellt: 31.08.2026
Auftraggeber: Sascha
Adressat: nachfolgender AI-Agent
Status: umgesetzt im Repository `klopp1991/zone-manager`, Prüflauf offen

Dieses Dokument ist die vollständige Arbeitsgrundlage. Es setzt keine Kenntnis der vorangegangenen Sitzung voraus. Vor Arbeitsbeginn zusätzlich `AGENTS.md` lesen; die dortigen Regeln zu paralleler Arbeit und zur Dokumentation gelten für jedes Arbeitspaket.

---

## 1. Ausgangslage

Projektwurzel: `T:\PortableApps\ZoneManager`

Sascha's Zone Manager ist ein Windows-11-x64-Programm für frei bearbeitbare Fensterzonen pro Monitor (.NET 8, WPF mit WinForms-Interop, self-contained Single-File-EXE).

### 1.1 Geprüftes Inventar

| Element | Stand |
|---|---|
| Solution | `ZoneManager.sln` |
| Projekte | `src\SnapZones.Core`, `src\SnapZones.Windows`, `src\SnapZones.App`, `tests\SnapZones.Tests` |
| Assembly / Produkt | `AssemblyName` = `ZoneManager`, `Product` = Sascha's Zone Manager |
| Root-Namespace App | `SnapZones.App` — weicht bewusst noch vom Assemblynamen ab, siehe AP2 |
| SDK | `global.json` bindet 8.0.424, `rollForward: latestFeature` |
| Tests | 303, alle grün (Stand 31.08.2026) |
| Compilerstrenge | `TreatWarningsAsErrors` in allen vier Projekten |
| Prüflauf | `scripts\verify.ps1`, Laufzeit ca. 1:37 mit `-SkipDpiCheck` |
| Root-EXE | `ZoneManager.exe` im Projektstamm, ca. 71.9 MB, self-contained win-x64 |
| Versionskontrolle | **kein Git-Repository vorhanden** — siehe AP5 |

### 1.2 Relevante Codestellen

| Thema | Datei |
|---|---|
| Einstiegspunkt, Elevation-Entscheidung | `src\SnapZones.App\Program.cs` |
| Elevation-Logik, testbar ohne Prozessstart | `src\SnapZones.App\Services\ElevationStartupService.cs` |
| Konfigurations- und Logverzeichnis | `src\SnapZones.App\App.xaml.cs`, Zeilen 27-29 |
| Persistenz, Pfad wird injiziert | `src\SnapZones.Core\Persistence\JsonConfigurationRepository.cs` |
| Einzelinstanz-Mutex | `src\SnapZones.App\Services\SingleInstanceService.cs`, Schlüssel aus `ProductInfo.InstanceKey` |
| Autostart-Registry | `src\SnapZones.Windows\Startup\WindowsStartupService.cs`, `ValueName = "SnapZones"` |
| Manifest | `src\SnapZones.App\app.manifest`, `requestedExecutionLevel level="asInvoker"` |

### 1.3 Laufzeitpfade, aktueller Stand

- Konfiguration: `%APPDATA%\SnapZones\settings.json` sowie `settings.backup-1.json` bis `settings.backup-5.json`
- Protokolle: `%LOCALAPPDATA%\SnapZones\logs\snapzones.log`
- Einzelinstanz: `Local\SaschaWindowZones.Mutex` und `Local\SaschaWindowZones.Activate`
- Autostart: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`, Wertname `SnapZones`

### 1.4 Elevationsverhalten, Ist-Zustand

Das Manifest fordert `asInvoker`. `Program.Main` ruft `ElevationStartupService.EnsureElevation` auf. Ist der Prozess nicht erhöht und wurde nicht `--diagnostics` übergeben, startet die Anwendung sich selbst per `ProcessStartInfo` mit `Verb = "runas"` neu und hängt den Marker `--elevation-attempted` an. Mögliche Ergebnisse: `Continue`, `Relaunched`, `Cancelled` bei Win32-Fehler 1223, oder `Failed`. In den letzten drei Fällen beendet sich der Erstprozess, bei `Cancelled` und `Failed` mit einer MessageBox.

Konsequenz: **ohne bestätigte UAC-Abfrage startet die Anwendung überhaupt nicht.** In einer nicht interaktiven Sitzung blockiert bereits `Process.Start` mit `runas`. Genau daran ist der Prüflauf am 31.08.2026 hängen geblieben, weil `scripts\verify-dpi-awareness.ps1` die Oberfläche startet.

---

## 2. Bereits erledigt — nicht wiederholen

Die folgenden Punkte wurden am 31.08.2026 gefunden und behoben. Sie sind Ausgangszustand, nicht Auftrag.

1. `scripts\verify.ps1` zeigte auf `SnapZones.sln`; die Solution heisst `ZoneManager.sln`. Dadurch brach der Lauf bereits bei `dotnet restore` ab und es entstand nie eine EXE. Pfad korrigiert.
2. `.gitignore` ignorierte nur historische Artefaktnamen. `/ZoneManager.exe` mit 72 MB, `outputs/ZoneManager-prototype/` und `outputs/zonemanager-diagnostics.json` waren nicht ignoriert. Datei auf die aktuellen Namen umgestellt, tote Einträge entfernt.
3. Das MSBuild-Target `PublishRootExecutableAfterBuild` in `SnapZones.App.csproj` löst bei jedem Build des App-Projekts einen vollständigen Self-contained-Publish aus. Da das Testprojekt das App-Projekt referenziert, erzeugte ein Prüflauf die 72-MB-EXE viermal. `verify.ps1` übergibt für Test, Build und Publish jetzt `-p:SkipRootExecutablePublish=true`; die Root-EXE entsteht einmal aus dem Publish-Artefakt.
4. In `verify.ps1` standen `if ($LASTEXITCODE -ne 0)`-Prüfungen hinter Aufrufen von PowerShell-Skripten. PowerShell-Skripte setzen `$LASTEXITCODE` nicht; geprüft wurde der Wert des letzten nativen Befehls. Entfernt, die Skripte melden Fehler über terminierende Ausnahmen.
5. `scripts\verify-root-build.ps1` existierte, wurde aber von nichts aufgerufen. Jetzt in `verify.ps1` eingehängt, Statusfeld `rootBuild=passed` im Abschlussbericht.
6. Assets `SaschaWindowZones.ico`, `.Header.png` und `.svg` umbenannt in `ZoneManager.*`; Referenzen in `SnapZones.App.csproj`, `Views\MainWindow.xaml` und `scripts\build-icon.ps1` nachgezogen.
7. `ProductInfo.ProcessName` hiess irreführend so, ist aber der Schlüssel der Einzelinstanz und nicht der Prozessname. Umbenannt in `ProductInfo.InstanceKey`; **der Wert blieb unverändert** auf `SaschaWindowZones`.
8. `outputs\SnapZones-Kurzanleitung.md` und `outputs\SnapZones-Pruefbericht.md` umbenannt in `ZoneManager-*` und inhaltlich auf den geprüften Stand gebracht. Der alte Prüfbericht nannte 44 Tests statt 303 und einen Blocker, der im Code adressiert ist.
9. `scripts\test-new-task-worktree.ps1`: Temp-Präfix auf `ZoneManager-WorktreeTest-` umgestellt.
10. `README.md`, `docs\README.md` und der Prüfbericht dokumentieren `-p:SkipRootExecutablePublish=true` sowie die Interaktivitätsanforderung der DPI-Prüfung.
11. `AGENTS.md` um den Abschnitt Dokumentation erweitert — Dauerregel, siehe AP4.

Letzter grüner Lauf:

```
VERIFY_OK tests=passed rootBuild=passed dpi=skipped monitors=1 startupLayouts=1 files=3
bytes=71928832 maximumExecutableBytes=100000000
rootExe=T:\PortableApps\ZoneManager\ZoneManager.exe hookRegistered=false settingsChanged=false
```

---

## 3. Arbeitsregeln für diesen Auftrag

1. `AGENTS.md` ist bindend. Nach AP5 existiert Git; ab dann gilt die Worktree-Pflicht: pro Arbeitspaket ein eigener Branch in einem eigenen Worktree über `scripts\new-task-worktree.ps1 -TaskName "<name>"`.
2. Nach jedem Arbeitspaket muss `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify.ps1` grün sein. In nicht interaktiven Sitzungen `-SkipDpiCheck` verwenden und im Bericht als übersprungen ausweisen; die DPI-Prüfung nach AP1 einmal interaktiv nachholen.
3. Keine Aussage ungeprüft dokumentieren. Zahlen aus dem tatsächlichen Lauf übernehmen.
4. Umbenennungen in kleinen, jeweils grünen Schritten. Nach jedem Schritt Build und Tests, nicht erst am Ende.
5. Am Ende jedes Arbeitspakets Abschnitt 5 dieses Dokuments nachführen: Status, Datum, Ergebnis.

---

## 4. Arbeitspakete

Reihenfolge ist bindend: AP5, dann AP1, dann AP3, dann AP2. AP4 gilt durchgehend.

### AP5 — Git einführen

**Entscheidung Sascha:** "Git soll verwendet werden."

**Warum:** `AGENTS.md` und `scripts\new-task-worktree.ps1` setzen Git voraus, es existiert aber kein Repository. Ohne Versionskontrolle sind die folgenden Umbenennungen nicht sicher rückholbar.

**Umfang**

1. `git init` in `T:\PortableApps\ZoneManager`, Standardbranch `main`.
2. Vor dem ersten Commit `git status` gegen `.gitignore` prüfen. `ZoneManager.exe`, `bin/`, `obj/`, `outputs/ZoneManager-prototype/`, `outputs/zonemanager-diagnostics.json` und `work/` dürfen **nicht** in der Liste stehen. Erst wenn das stimmt, committen.
3. Erster Commit als Bestandsaufnahme des jetzigen, grünen Standes.
4. `scripts\test-new-task-worktree.ps1` ausführen und das Ergebnis im Auftrag festhalten.
5. Klären und dokumentieren, ob ein Remote gewünscht ist. Das README nennt `https://github.com/klopp1991/zone-manager.git`; ob dieses Remote existiert und benutzt werden soll, ist **ungeprüft**. Ohne ausdrückliche Freigabe von Sascha nichts pushen und kein Remote anlegen.

**Akzeptanzkriterien**

- `git log` zeigt genau einen Commit; `git status` ist danach sauber.
- Keine Build-Artefakte und keine EXE im Index. Prüfung: `git ls-files` enthält keinen Pfad mit `/bin/`, `/obj/` oder Endung `.exe`.
- `scripts\test-new-task-worktree.ps1` läuft fehlerfrei.
- Das README beschreibt den tatsächlichen Repository-Zustand.

---

### AP1 — Elevation vorher prüfen statt daran scheitern

**Entscheidung Sascha:** "Am besten prüft die App, bevor sie sich die Rechte hochstuft, ob sie sich selber hochstufen kann. Falls möglich, macht sie das; falls nicht, läuft sie halt mit den entsprechenden Rechten."

**Warum:** Heute ist Elevation eine Alles-oder-nichts-Entscheidung. Schlägt sie fehl oder wird sie nicht beantwortet, startet die Anwendung gar nicht. Ein grosser Teil der Funktion braucht keine Administratorrechte; nur das Positionieren erhöhter Fremdfenster braucht sie.

**Umfang**

1. `ElevationStartupService` um eine Vorprüfung erweitern, die **ohne Prozessstart** ermittelt, ob eine Erhöhung überhaupt möglich ist. Zu berücksichtigen: läuft der Benutzer bereits erhöht; ist er Mitglied der lokalen Administratoren beziehungsweise besitzt sein gefiltertes Token die Administrator-Zugehörigkeit; ist UAC aktiviert (`EnableLUA`); liegt eine Sitzung vor, in der eine Abfrage überhaupt angezeigt werden kann.
2. Neues Ergebnis `ElevationStartupStatus.ContinueUnelevated` einführen: die Anwendung startet mit den vorhandenen Rechten weiter, statt sich zu beenden.
3. `Program.Main` entsprechend anpassen. `Cancelled` führt nicht mehr zum Programmende, sondern ebenfalls in den nicht erhöhten Betrieb.
4. Eingeschränkten Betrieb sichtbar machen: Hinweis in der Oberfläche und im Tray-Tooltip, dass Fenster erhöhter Programme nicht positioniert werden können, samt Schaltfläche für einen erneuten Versuch mit Erhöhung. Kein stiller Funktionsverlust.
5. Fehler beim Positionieren erhöhter Fremdfenster müssen im nicht erhöhten Betrieb als erwarteter, erklärter Fall behandelt werden, nicht als Absturz oder stiller Fehlschlag.
6. `--diagnostics` bleibt unverändert ohne Erhöhung. Die Diagnoseausgabe erhält zusätzlich die Felder `isElevated` und `canElevate`.
7. Tests in `tests\SnapZones.Tests` für jeden Zweig der Vorprüfung. `ElevationStartupService` ist bereits über `isAdministrator` und `startElevated` injizierbar; die neue Prüfung ebenso injizierbar halten, damit sie ohne echte Registry und ohne echten Prozessstart testbar bleibt.
8. `scripts\verify-dpi-awareness.ps1` härten: begrenzte Wartezeit statt unbegrenzten Blockierens, mit klarer Fehlermeldung bei nicht beantworteter UAC-Abfrage. Der Prüflauf darf nicht mehr unbegrenzt hängen.
9. `README.md`, `docs\README.md` und `outputs\ZoneManager-Kurzanleitung.md` auf das neue Startverhalten anpassen. Die heutige Formulierung "Wird die Windows-UAC-Abfrage abgebrochen, startet die Anwendung nicht" wird damit falsch und muss ersetzt werden.

**Akzeptanzkriterien**

- Start ohne Administratorrechte und mit abgebrochener UAC-Abfrage führt zu einer laufenden Anwendung mit sichtbarem Hinweis, nicht zum Programmende.
- Start als Administrator verhält sich unverändert.
- `--diagnostics` läuft weiterhin ohne Erhöhung und meldet `isElevated` und `canElevate`.
- `scripts\verify.ps1` ohne `-SkipDpiCheck` läuft interaktiv durch und endet mit `dpi=passed`.
- `verify-dpi-awareness.ps1` bricht in einer nicht interaktiven Sitzung innerhalb der Zeitgrenze mit verständlicher Meldung ab, statt zu hängen.

**Risiko:** Die Vorprüfung darf nicht selbst eine UAC-Abfrage auslösen. Nur Token- und Registry-Abfragen verwenden.

---

### AP3 — Konfigurations- und Protokollordner umbenennen, mit Migration

**Entscheidung Sascha:** "Versuch, das lokal bei mir umzubenennen. Also du änderst den Namen des Folders und entsprechend logischerweise auch im Code."

**Warum:** Die Laufzeitpfade tragen noch den alten Produktnamen `SnapZones`, während das Produkt Sascha's Zone Manager und die EXE `ZoneManager.exe` heisst.

**Umfang**

1. Zielpfade: `%APPDATA%\ZoneManager\settings.json` inklusive der fünf Sicherungen, `%LOCALAPPDATA%\ZoneManager\logs\zonemanager.log`.
2. Einmalige Migration beim Start: existiert der alte Ordner und der neue noch nicht, wird der Inhalt übernommen. Der alte Ordner wird **nicht gelöscht**, sondern bleibt unverändert als Rückfallebene liegen. Die Migration ist idempotent und wird protokolliert.
3. Ist bereits eine Konfiguration am neuen Ort vorhanden, hat diese Vorrang; es wird nichts überschrieben.
4. Autostart-Wertname in `WindowsStartupService` von `SnapZones` auf `ZoneManager` umstellen. Ein vorhandener alter Wert wird beim Aktivieren entfernt, damit kein doppelter Autostart entsteht.
5. Einzelinstanz-Schlüssel `ProductInfo.InstanceKey` auf `ZoneManager` umstellen — **erst nach AP1 und zusammen mit dieser Umbenennung**, damit nicht zwei Instanzen mit unterschiedlichen Schlüsseln nebeneinander laufen. Im Code vermerken, dass eine gleichzeitig laufende Altversion dadurch nicht mehr erkannt wird.
6. `scripts\verify-dpi-awareness.ps1` liest `%APPDATA%\SnapZones\settings.json`; Pfad mitziehen.
7. Migration mit Tests abdecken: kein alter Ordner; nur alter Ordner; beide Ordner; beschädigte alte Datei.
8. Doku: `docs\README.md`, `outputs\ZoneManager-Kurzanleitung.md` und `README.md` auf die neuen Pfade umstellen und die Migration einmal erklären.

**Akzeptanzkriterien**

- Ein Start mit vorhandener alter Konfiguration übernimmt Layouts, Zonen, App-Regeln und Einstellungen vollständig; der alte Ordner bleibt erhalten.
- Ein Start ohne jede Konfiguration erzeugt sauber die Standardkonfiguration am neuen Ort.
- Zweimaliger Start hintereinander verändert nach der ersten Migration nichts mehr.
- Autostart lässt sich aus- und wieder einschalten und hinterlässt genau einen Registry-Wert.
- Keine Fundstelle von `SnapZones` mehr in Laufzeitpfaden; verbleibende Treffer betreffen nur Namespace- und Projektnamen, die AP2 auflöst.

**Risiko:** Sascha hat eine aktive lokale Konfiguration. Vor dem ersten Test von Hand eine Kopie von `%APPDATA%\SnapZones` sichern und das im Auftrag vermerken.

---

### AP2 — Projekte und Namespaces auf ZoneManager umbenennen

**Entscheidung Sascha:** "Sauber umbenennen, sauber regelmässig prüfen, sauber arbeiten, dann geht das."

**Warum:** Assembly und Produkt heissen ZoneManager, Projekte und Namespaces noch SnapZones. Die Abweichung ist bei jeder Datei erklärungsbedürftig und war bereits Ursache eines Build-Defekts, nämlich des falschen Solutionnamens in `verify.ps1`.

**Zielbenennung**

| Alt | Neu |
|---|---|
| `src\SnapZones.Core\SnapZones.Core.csproj` | `src\ZoneManager.Core\ZoneManager.Core.csproj` |
| `src\SnapZones.Windows\SnapZones.Windows.csproj` | `src\ZoneManager.Windows\ZoneManager.Windows.csproj` |
| `src\SnapZones.App\SnapZones.App.csproj` | `src\ZoneManager.App\ZoneManager.App.csproj` |
| `tests\SnapZones.Tests\SnapZones.Tests.csproj` | `tests\ZoneManager.Tests\ZoneManager.Tests.csproj` |
| Namespaces `SnapZones.*` | `ZoneManager.*` |

**Vorgehen — je Schritt ein Commit, je Schritt grün**

1. `ZoneManager.Core` umbenennen: Ordner, csproj, `namespace`, `using`, Projektreferenzen in `.Windows`, `.App` und `.Tests` sowie in `ZoneManager.sln` einschliesslich der Projektzeilen. Build und Tests.
2. `ZoneManager.Windows` analog. Build und Tests.
3. `ZoneManager.App` analog. Hier zusätzlich beachten:
   - `AssemblyName` bleibt `ZoneManager`, `RootNamespace` wird `ZoneManager.App`.
   - XAML: `x:Class`, `xmlns:local`, `clr-namespace`-Verweise und die Pack-URIs `/ZoneManager;component/Assets/...` in `Views\MainWindow.xaml` und den Overlay-XAML-Dateien.
   - Pfade in `scripts\build-icon.ps1` (`src\SnapZones.App\Assets\...`) sowie in `scripts\verify.ps1` und `scripts\verify-root-build.ps1` (`src\SnapZones.App\SnapZones.App.csproj`).
   - Die MSBuild-Eigenschaften `RootExecutablePath`, `RootPublishDirectory` und `RootPublishScript` im csproj arbeiten mit relativen Pfaden auf die Projektwurzel; nach dem Ordnerwechsel prüfen.

   Build und Tests.
4. `ZoneManager.Tests` analog. Build und Tests.
5. `Add-Type -TypeDefinition` in `scripts\verify-dpi-awareness.ps1` verwendet den Namespace `SnapZones` für `ProcessDpiProbe`. Umbenennen und die Typexistenzprüfung `'SnapZones.ProcessDpiProbe' -as [type]` mitziehen.
6. Vollständiger `scripts\verify.ps1`-Lauf.
7. Doku: `README.md`, Abschnitt zur Projektaufteilung, und `docs\README.md`. `docs\superpowers\**` bleibt unverändert, siehe Regel 6 im Abschnitt Dokumentation von `AGENTS.md`.

**Akzeptanzkriterien**

- Eine Suche nach `SnapZones` über `*.cs`, `*.xaml`, `*.csproj`, `*.ps1`, `*.sln` und `*.md` liefert ausserhalb von `docs\superpowers\**` und Buildartefakten keine Treffer.
- 303 Tests weiterhin grün, 0 Warnungen, 0 Fehler.
- `scripts\verify.ps1` endet mit `VERIFY_OK`.
- Die erzeugte EXE heisst unverändert `ZoneManager.exe` und startet.
- Reihenfolge: **nach AP3**, damit die Laufzeitpfade nicht mitten in der Umbenennung wechseln.

**Risiko:** `bin/` und `obj/` enthalten alte generierte Dateien mit den alten Namen. Vor dem Abschlusslauf beide Ordner in allen Projekten löschen, sonst täuschen zwischengespeicherte BAML- und Resources-Artefakte Erfolg oder Fehler vor.

---

### AP4 — Dokumentation dauerhaft gepflegt halten

**Entscheidung Sascha:** "Ganz generell und dauerhaft soll die Doku nicht einfach immer nur grösser und grösser werden, sondern laufend sauber dokumentiert sein."

Das ist keine einmalige Aufgabe, sondern eine Dauerregel. Sie steht als Abschnitt Dokumentation in `AGENTS.md` und gilt für jedes Arbeitspaket dieses Auftrags und alle späteren.

**Akzeptanzkriterien je Arbeitspaket**

- Kein Arbeitspaket gilt als fertig, solange ein Dokument eine Aussage enthält, die durch das Arbeitspaket falsch geworden ist.
- Neue Erkenntnisse werden in das zuständige bestehende Dokument eingearbeitet, nicht als neuer Anhang danebengestellt.
- `outputs\ZoneManager-Pruefbericht.md` gibt nach jedem Arbeitspaket den Stand des letzten tatsächlichen Laufs wieder, einschliesslich dessen, was nicht geprüft wurde.

---

## 5. Fortschritt

| AP | Thema | Status | Datum | Ergebnis |
|---|---|---|---|---|
| AP5 | Git einführen | umgesetzt | 31.08.2026 | Repository bestand bereits inklusive Remote; Root-EXE aus dem Index genommen, `.gitignore` und README auf den tatsächlichen Zustand gebracht |
| AP1 | Elevation vorher prüfen | umgesetzt, Prüflauf offen | 31.08.2026 | Vorprüfung ohne Prozessstart, `ContinueUnelevated`, Banner mit erneutem Versuch, Tray-Hinweis, erklärte Platzierungsfehler, `isElevated`/`canElevate` in der Diagnose, begrenzte Wartezeit im DPI-Skript |
| AP3 | Laufzeitordner umbenennen | umgesetzt, Prüflauf offen | 31.08.2026 | `%APPDATA%\ZoneManager`, `%LOCALAPPDATA%\ZoneManager\logs\zonemanager.log`, idempotente Übernahme mit Tests, Autostart- und Einzelinstanzschlüssel `ZoneManager` |
| AP2 | Projekte und Namespaces umbenennen | umgesetzt, Prüflauf offen | 31.08.2026 | `ZoneManager.Core/.Windows/.App/.Tests`, Namespaces, XAML- und Skriptverweise; `AssemblyName` unverändert `ZoneManager` |
| AP4 | Dokumentationsdisziplin | dauerhaft | 31.08.2026 | Regel in `AGENTS.md` verankert; README, `docs\README.md`, Kurzanleitung und Prüfbericht nachgeführt |

### Abweichungen vom Auftrag

1. Gearbeitet wurde nicht in `T:\PortableApps\ZoneManager`, sondern im GitHub-Repository `klopp1991/zone-manager`. Dieses Repository stand auf einem älteren Stand: die unter Abschnitt 2 als erledigt aufgeführten Punkte fehlten dort und wurden zuerst nachgezogen (erster Commit dieses Durchgangs).
2. Statt eines Worktrees je Arbeitspaket entstand ein Branch mit einem Commit je Arbeitspaket: `claude/datei-lesen-umsetzen-jd2144`. Die Sitzung arbeitete in einem eigenen Klon, ein zweiter Worktree hätte keinen zusätzlichen Schutz gebracht.
3. Kein Arbeitspaket wurde gebaut oder getestet. Die Sitzung lief unter Linux ohne .NET SDK; drei der vier Projekte zielen auf `net8.0-windows` mit WPF. Das Nachinstallieren des SDK scheiterte an der Netzwerkrichtlinie. `scripts\verify.ps1` ist deshalb auf einem Windows-11-Rechner nachzuholen; Einzelheiten in `outputs\ZoneManager-Pruefbericht.md`.
4. `scripts\test-new-task-worktree.ps1` konnte aus demselben Grund nicht ausgeführt werden (PowerShell-Skript, Windows-Pfade); nur das Temp-Präfix wurde umgestellt.
5. Die Migration deckt den Konfigurationsordner ab. Alte Protokolle wandern bewusst nicht mit; sie bleiben unter `%LOCALAPPDATA%\SnapZones\logs` liegen.

---

## 6. Offene Fragen an Sascha

1. **Beantwortet.** Das Remote `https://github.com/klopp1991/zone-manager.git` existiert und wird verwendet; dieser Auftrag wurde direkt darin umgesetzt.
2. **Vorläufig entschieden, Bestätigung offen.** Der nicht erhöhte Betrieb versucht beim nächsten Start wieder von sich aus eine Erhöhung, sofern die Vorprüfung sie für möglich hält; innerhalb einer Sitzung geschieht ein erneuter Versuch nur auf ausdrückliche Bestätigung über die Schaltfläche im Banner. Wenn stattdessen gar kein automatischer Versuch mehr erfolgen soll, ist eine Einstellung dafür nachzurüsten.
3. **Neu:** Die Root-EXE ist nicht mehr versioniert (Akzeptanzkriterium von AP5). Wer das Repository klont, baut sie über `scripts\verify.ps1`. Soll stattdessen weiterhin eine fertige EXE im Repository liegen, ist das rückgängig zu machen.
