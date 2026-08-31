# App-Regeln Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fenster anhand von Prozess, optionalem Fenstertitel und Fensterklasse bei Erstellung, Fokus oder Layoutaktivierung verzögert und begrenzt wiederholt in eine konfigurierte Zone verschieben.

**Architecture:** Ein reiner Core-Matcher löst Regeln deterministisch nach Aktivstatus, Ereignis, Priorität und Spezifität auf. Ein eigener WinEvent-Hook liefert nur Top-Level-Fensterereignisse; der Anwendungskordinator prüft Identität und Ziel unmittelbar vor jeder Positionierung erneut und verwendet den bestehenden Fensterdienst für den einzigen `SetWindowPos`-Aufruf.

**Tech Stack:** .NET 8, C# 12, WPF, dokumentierte Win32-WinEvent- und Fenster-APIs, xUnit.

**Spec:** `docs/superpowers/specs/2026-08-30-sascha-window-zones-reliability-ux-design.md`

## Global Constraints

- Windows 11 x64; kein Treiber, Dienst, Code-Injection oder undokumentierte Shell-Schnittstelle.
- Regeln starten keine Programme und verwenden keinen stillen Fallback bei fehlendem Layout, Monitor oder Zone.
- Wiederholungen liegen zwischen 0 und 3; vor jedem Versuch werden Handle, Prozess, Fensterklasse und Ziel erneut geprüft.
- Vorhandene benutzerseitige Änderungen im Arbeitsverzeichnis bleiben unverändert erhalten.

---

### Task 1: Regelmodell, Matching und Konfigurationsmigration

**Files:**
- Create: `src/SnapZones.Core/AppRules/AppRule.cs`
- Create: `src/SnapZones.Core/AppRules/AppRuleMatcher.cs`
- Modify: `src/SnapZones.Core/Models/SnapConfiguration.cs`
- Modify: `src/SnapZones.Core/Persistence/JsonConfigurationRepository.cs`
- Modify: `src/SnapZones.Core/Layouts/LayoutService.cs`
- Test: `tests/SnapZones.Tests/AppRules/AppRuleMatcherTests.cs`
- Test: `tests/SnapZones.Tests/Persistence/JsonConfigurationRepositoryTests.cs`

**Interfaces:**
- Produces: `AppRuleEvent`, `AppRule`, `AppWindowIdentity`, `AppRuleMatcher.Resolve`, `SnapConfiguration.AppRules`, `LayoutService.UpdateAppRules`.

- [ ] **Step 1:** Tests schreiben, die Prozesspfad-/Dateinamen-Matching, Titel-Wildcards, exakte Klassen, Ereignisse, Priorität und Spezifität sowie Schema-2-Migration auf leere Regeln erwarten.
- [ ] **Step 2:** Fokussierte Tests ausführen und das Fehlen der Typen beziehungsweise Migration als erwarteten Fehler bestätigen.
- [ ] **Step 3:** Unveränderliche Regeltypen, deterministischen Matcher, Schema 3 und Validierung der Bereiche `DelayMilliseconds 0..30000`, `RetryCount 0..3`, `Priority 0..100` implementieren.
- [ ] **Step 4:** Fokussierte Tests erneut ausführen und grün bestätigen.

### Task 2: Windows-Ereignisse und sichere Fensteridentität

**Files:**
- Create: `src/SnapZones.Windows/Hooks/IWindowRuleHook.cs`
- Create: `src/SnapZones.Windows/Hooks/WindowRuleHook.cs`
- Create: `src/SnapZones.Windows/Windows/WindowRuleCandidate.cs`
- Modify: `src/SnapZones.Windows/Windows/IWindowService.cs`
- Modify: `src/SnapZones.Windows/Windows/WindowsWindowService.cs`
- Modify: `src/SnapZones.Windows/Native/User32.cs`
- Create: `src/SnapZones.Windows/Native/Kernel32.cs`
- Test: `tests/SnapZones.Tests/Drag/WindowsSafetyBoundaryTests.cs`

**Interfaces:**
- Produces: `IWindowRuleHook.RuleEvent`, `IWindowService.InspectRuleCandidate`, `IWindowService.EnumerateTopLevelWindows`.

- [ ] **Step 1:** Tests für ungültige Handles, eigene/unsichtbare/Kind-/Tool-/Cloaked-Fenster und stabile Kandidatendaten ergänzen.
- [ ] **Step 2:** Tests ausführen und den fehlenden Regel-Kandidatenpfad als erwarteten Fehler bestätigen.
- [ ] **Step 3:** Getrennte Hooks für `EVENT_OBJECT_SHOW` und `EVENT_SYSTEM_FOREGROUND`, dokumentierte Prozesspfadauflösung und erneute Eignungsprüfung implementieren.
- [ ] **Step 4:** Sicherheits- und Hook-Tests ausführen.

### Task 3: Verzögerte und serialisierte Regelausführung

**Files:**
- Create: `src/SnapZones.App/Services/AppRuleCoordinator.cs`
- Test: `tests/SnapZones.Tests/Services/AppRuleCoordinatorTests.cs`

**Interfaces:**
- Consumes: Regelmatcher, Fensterkandidaten, Layouts, Live-Monitore und `IWindowService.TrySnap`.
- Produces: `HandleAsync(AppRuleEvent, nint)`, `HandleLayoutActivatedAsync(Guid)`, `CancelPending()` und strukturierte Statusmeldungen.

- [ ] **Step 1:** Tests für Verzögerung, erneute Identitätsprüfung, Zielauflösung, serielle Ausführung, höchstens drei Wiederholungen und pausierte fehlende Ziele schreiben.
- [ ] **Step 2:** Tests ausführen und das Fehlen des Koordinators als erwarteten Fehler bestätigen.
- [ ] **Step 3:** Koordinator mit injizierbarer Verzögerung und Fenster-Gateway implementieren; Pixelziel über `ZoneGeometry.ToPixels` berechnen.
- [ ] **Step 4:** Fokussierte Tests ausführen und grün bestätigen.

### Task 4: Regelverwaltung und Laufzeitintegration

**Files:**
- Create: `src/SnapZones.App/ViewModels/AppRuleEditorViewModel.cs`
- Modify: `src/SnapZones.App/ViewModels/MainViewModel.cs`
- Modify: `src/SnapZones.App/Views/MainWindow.xaml`
- Modify: `src/SnapZones.App/Views/MainWindow.xaml.cs`
- Modify: `src/SnapZones.App/Services/ApplicationController.cs`
- Test: `tests/SnapZones.Tests/ViewModels/AppRuleEditorViewModelTests.cs`
- Test: `tests/SnapZones.Tests/Theme/AppRulesPresentationTests.cs`

**Interfaces:**
- Produces: sichtbare App-Regel-Liste mit Hinzufügen, Löschen, Aktivstatus, Prozessauswahl, Titelmuster, Klasse, Ereignis, Priorität, Verzögerung, Wiederholungen, Ziellayout, Zielzone und Pausenstatus.

- [ ] **Step 1:** ViewModel- und UI-Vertragstests für gültige Speicherung, fehlende Ziele und neutrale deutsche Beschriftungen schreiben.
- [ ] **Step 2:** Tests ausführen und die fehlende App-Regel-Oberfläche als erwarteten Fehler bestätigen.
- [ ] **Step 3:** ViewModel, Master-Detail-Tab und Dateiauswahl implementieren; jede gültige Änderung automatisch über `SaveRequested` persistieren.
- [ ] **Step 4:** Hook und Koordinator im `ApplicationController` aktivieren, bei Layoutwechsel auslösen und bei Not-Aus/Dispose vollständig stoppen.
- [ ] **Step 5:** ViewModel-, UI- und Controller-nahe Tests ausführen.

### Task 5: Gesamtverifikation

**Files:**
- Modify: `README.md`
- Modify: `docs/README.md`

- [ ] **Step 1:** Funktionsbeschreibung, Grenzen und Regelbeispiele dokumentieren.
- [ ] **Step 2:** `dotnet test SnapZones.sln -c Release` ausführen.
- [ ] **Step 3:** `dotnet build SnapZones.sln -c Release` ausführen.
- [ ] **Step 4:** `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify.ps1` ausführen und Diagnose-/Publish-Ergebnis prüfen.
