# Monitor Layouts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ersetze globale Profile durch unabhängig aktivierbare, gespeicherte Layouts pro Monitor.

**Architecture:** `SnapConfiguration` speichert Schema 2 als flache `MonitorLayout`-Liste; `LayoutService` erzwingt monitorbezogene Invarianten und `MainViewModel` projiziert den gewählten Monitor und dessen Layouts in den Editor. Repository und Import migrieren Schema 1 verlustarm, während Controller und Tray stets die pro Monitor aktiven Layouts verwenden.

**Tech Stack:** .NET 8, C# 12, WPF, Windows Forms NotifyIcon, System.Text.Json, xUnit, PowerShell.

**Spec:** `docs/superpowers/specs/2026-08-30-monitor-layouts-design.md`

## Global Constraints

- DE-CH und neutrale UI-Texte.
- Keine grafische Abnahme; nur automatisierte Struktur-, Funktions-, Build-, Diagnose- und DPI-Prüfungen.
- Bestehende Schema-1-Konfigurationen und Archive werden automatisch migriert.
- Änderungen eines Monitors dürfen die aktive Layoutwahl anderer Monitore nicht verändern.
- Bestehende, nicht zu dieser Aufgabe gehörende Änderungen bleiben erhalten.

---

### Task 1: Layoutkatalog und Migration

**Files:**
- Modify: `src/SnapZones.Core/Models/MonitorLayout.cs`
- Modify: `src/SnapZones.Core/Models/SnapConfiguration.cs`
- Create: `src/SnapZones.Core/Layouts/LayoutService.cs`
- Modify: `src/SnapZones.Core/Persistence/JsonConfigurationRepository.cs`
- Test: `tests/SnapZones.Tests/Layouts/LayoutServiceTests.cs`
- Test: `tests/SnapZones.Tests/Persistence/JsonConfigurationRepositoryTests.cs`

**Interfaces:**
- Produces: `LayoutService.ActiveLayoutFor`, `EnsureMonitor`, `AddLayout`, `RenameLayout`, `DeleteLayout`, `ActivateLayout`, `UpdateLayout`, `UpdateSettings`.
- Produces: `JsonConfigurationRepository.Upgrade(SnapConfiguration)` für Repository und Import.

- [ ] **Step 1: Schreib fehlschlagende Service- und Migrationstests**

Prüfe mit festen IDs, dass `ActivateLayout(secondId)` nur die Layoutgruppe desselben Monitors umschaltet und dass Schema 1 pro Profil-Monitor-Paar einen Schema-2-Eintrag mit erhaltenem Namen und erhaltenen Zonen erzeugt.

- [ ] **Step 2: Führe die fokussierten Tests aus und bestätige RED**

```powershell
dotnet test tests/SnapZones.Tests/SnapZones.Tests.csproj --no-restore --filter "FullyQualifiedName~LayoutServiceTests|FullyQualifiedName~JsonConfigurationRepositoryTests"
```

Erwartet: Kompilierung oder Assertions schlagen ausschliesslich wegen der fehlenden Layoutkatalog- beziehungsweise Migrationsfunktion fehl.

- [ ] **Step 3: Implementiere Schema 2, Kataloginvarianten und Migration**

`MonitorLayout` erhält init-Eigenschaften `Guid Id`, `string Name` und `bool IsActive`. `SnapConfiguration` enthält `IReadOnlyList<MonitorLayout> Layouts` und eine nur zum Einlesen verwendete, beim Schreiben ausgelassene Schema-1-Profilsammlung. `LayoutService` gruppiert per stabiler ID, ersatzweise Gerätename, und aktiviert beim Löschen eines aktiven Layouts den ersten verbleibenden Eintrag derselben Gruppe.

- [ ] **Step 4: Führe die fokussierten Tests bis GREEN aus**

Verwende denselben Befehl und erwarte keine Fehler oder Warnungen.

### Task 2: ViewModel und Editorfluss

**Files:**
- Modify: `src/SnapZones.App/ViewModels/MainViewModel.cs`
- Modify: `src/SnapZones.App/ViewModels/MonitorChoice.cs`
- Modify: `src/SnapZones.App/ViewModels/SettingsViewModel.cs`
- Modify: `tests/SnapZones.Tests/Support/ConfigurationSamples.cs`
- Modify: `tests/SnapZones.Tests/ViewModels/MainViewModelPersistenceTests.cs`

**Interfaces:**
- Consumes: `LayoutService` aus Task 1.
- Produces: `ObservableCollection<MonitorLayout> Layouts`, `MonitorLayout? SelectedLayout`, `AddLayout`, `RenameSelectedLayout`, `DeleteSelectedLayout`, `ActivateLayout` und `CanDeleteSelectedLayout`.

- [ ] **Step 1: Schreib fehlschlagende ViewModel-Tests**

Prüfe, dass Layoutwechsel, neue Layouts und Löschungen sofort persistieren, den Editor auf das aktive Layout setzen und die aktive Wahl eines zweiten Monitors unverändert lassen.

- [ ] **Step 2: Bestätige RED mit fokussiertem Testlauf**

```powershell
dotnet test tests/SnapZones.Tests/SnapZones.Tests.csproj --no-restore --filter FullyQualifiedName~MainViewModelPersistenceTests
```

- [ ] **Step 3: Implementiere die monitorbezogene ViewModel-Projektion**

Speichere einen gültigen Editorentwurf vor Monitor- oder Layoutwechseln, aktualisiere Layout- und Monitorlisten ohne rekursive Persistenz und erzeuge neue Layouts als Zonen-Kopie des aktiven Layouts mit neuen Zonen-IDs.

- [ ] **Step 4: Führe die ViewModel-Tests bis GREEN aus**

Erwarte vollständigen Erfolg ohne Warnungen.

### Task 3: Tray, Laufzeitziele und Import

**Files:**
- Create: `src/SnapZones.App/Services/TrayLayoutMenuPlan.cs`
- Modify: `src/SnapZones.App/Services/TrayIconService.cs`
- Modify: `src/SnapZones.App/Services/ApplicationController.cs`
- Modify: `src/SnapZones.Core/Persistence/ConfigurationTransferService.cs`
- Modify: `src/SnapZones.Windows/Hotkeys/IGlobalHotkeyService.cs`
- Modify: `src/SnapZones.Windows/Hotkeys/GlobalHotkeyService.cs`
- Test: `tests/SnapZones.Tests/Services/TrayLayoutMenuPlanTests.cs`

**Interfaces:**
- Produces: `TrayLayoutMenuPlan.Build(SnapConfiguration)` mit Monitorgruppen und markiertem Layout.
- Consumes: `MainViewModel.ActivateLayout(Guid)`.

- [ ] **Step 1: Schreib fehlschlagende Menü- und Zielauswahltests**

Prüfe zwei Monitore mit je zwei Layouts, exakte Gruppierung, aktive Markierung und unabhängige Auswahl.

- [ ] **Step 2: Bestätige RED mit fokussiertem Testlauf**

```powershell
dotnet test tests/SnapZones.Tests/SnapZones.Tests.csproj --no-restore --filter "FullyQualifiedName~TrayLayoutMenuPlanTests"
```

- [ ] **Step 3: Implementiere Monitor-Untermenüs und aktive Laufzeitlayouts**

Erzeuge pro Monitor einen `ToolStripMenuItem` mit Layout-Unterpunkten. Controller, Overlays und Drag-Koordinator verwenden pro Live-Monitor genau das aktive Layout; Profil-Hotkeys und Profil-Callbacks entfallen, der Sicherheits-Hotkey bleibt.

- [ ] **Step 4: Führe die fokussierten Tests bis GREEN aus**

Erwarte vollständigen Erfolg ohne Warnungen.

### Task 4: WPF-Oberfläche und Texte

**Files:**
- Modify: `src/SnapZones.App/Views/MainWindow.xaml`
- Modify: `src/SnapZones.App/Views/MainWindow.xaml.cs`
- Modify: `src/SnapZones.App/Services/ProfileChangedToast.xaml`
- Modify: `src/SnapZones.App/Services/ProfileChangedToast.xaml.cs`
- Modify: `tests/SnapZones.Tests/Theme/ThemeResourceTests.cs`
- Modify: `tests/SnapZones.Tests/Theme/LayoutSuggestionPresentationTests.cs`
- Modify: `docs/README.md`

**Interfaces:**
- Consumes: Layoutauswahl und Aktionen aus Task 2.
- Produces: eine Seite **Layouts** mit Monitorwahl, Layoutwahl, Layoutname, Erstellen/Löschen und bestehendem Zonen-Editor.

- [ ] **Step 1: Schreib fehlschlagende WPF-Strukturtests**

Prüfe ohne Screenshot, dass keine Profilseite und kein Profilwähler existieren, die Layoutseite Monitor- und Layoutauswahl besitzt und die Löschaktion an `CanDeleteSelectedLayout` gebunden ist.

- [ ] **Step 2: Bestätige RED mit fokussiertem Testlauf**

```powershell
dotnet test tests/SnapZones.Tests/SnapZones.Tests.csproj --no-restore --filter "FullyQualifiedName~ThemeResourceTests|FullyQualifiedName~LayoutSuggestionPresentationTests"
```

- [ ] **Step 3: Implementiere die kompakte Layout-Hierarchie und neutrale Texte**

Entferne Profilseite und Profilwähler. Zeige auf der Layoutseite zuerst Monitor und Layout, daneben Layoutname, Erstellen, Löschen und neue Zone; verwende bestehende Farben, Typografie, Fokuszustände und Responsive-Mindestgrössen.

- [ ] **Step 4: Führe die WPF-Strukturtests bis GREEN aus**

Erwarte vollständigen Erfolg ohne grafische Abnahme.

### Task 5: Gesamtprüfung und Release

**Files:**
- Modify: `tests/SnapZones.Tests/*` nur soweit alte Profilannahmen durch das neue Verhalten ersetzt werden müssen.
- Modify: `outputs/SnapZones-Kurzanleitung.md`

**Interfaces:**
- Produces: getestete Release-Einzeldatei `SaschaZoneManager.exe` im Projektroot und identisches Publish-Artefakt.

- [ ] **Step 1: Führe die vollständige Testsuite aus**

```powershell
dotnet test SnapZones.sln -c Release --no-restore
```

- [ ] **Step 2: Behebe ausschliesslich aufgabenbezogene Fehler und wiederhole den vollständigen Testlauf**

Erwartet: alle Tests bestanden, keine Warnungen.

- [ ] **Step 3: Aktualisiere Kurzanleitung und führe den vollständigen Prüf-/Publish-Lauf aus**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1
```

Erwartet: `VERIFY_OK`, Diagnose meldet mindestens einen Monitor, `hookRegistered=false`, `settingsChanged=false`, DPI-Prüfung bestanden und Root-EXE hashgleich mit dem Publish-Artefakt.
