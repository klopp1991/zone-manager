# Fensterplatzierungs-Engine Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Sascha Window Zones lernt die letzte sichtbare Platzierung jedes geeigneten Fenstertyps und stellt Zone, Position, Grösse sowie Maximierung beim nächsten Öffnen genau einmal wieder her; optionale Regeln können eine feste Zone oder einen Ausschluss definieren.

**Architecture:** Eine reine Core-Schicht enthält Identität, Regelauflösung, Zonenklassifikation und monitorfeste Geometrie. Eine getrennte Windows-Schicht beobachtet dokumentierte WinEvents und liest oder setzt native Fensterplatzierungen; `WindowPlacementEngine` in der App-Schicht koordiniert Wiederherstellung, Lernen, Deduplizierung und gebündelte Speicherung. Benutzerregeln bleiben in `settings.json`, häufig veränderte gelernte Zustände liegen atomar in `placements.json`.

**Tech Stack:** .NET 8, C# 12, WPF, xUnit 2.9, dokumentierte Win32-/Shell-APIs, PowerShell-Verifikation für Windows 11 x64

**Spec:** `docs/superpowers/specs/2026-08-30-window-placement-engine-design.md`

## Global Constraints

- Zielplattform bleibt Windows 11 x64 mit .NET 8 und WPF.
- Keine Code-Injection, kein Treiber, kein Windows-Dienst, keine Administratoranforderung und keine undokumentierten Windows-Strukturen.
- Die Automatik ist nach Migration und bei neuen Konfigurationen aktiv, kann aber über einen Hauptschalter vollständig deaktiviert werden.
- Ein Fenster wird pro Handle-Lebensdauer höchstens einmal automatisch platziert und danach nicht festgehalten.
- Maximierung wird gespeichert und wiederhergestellt; Minimierung wird weder gespeichert noch wiederhergestellt.
- Standardidentität ist AppUserModelId oder kanonischer Prozesspfad plus Fensterklasse und Fensterart; wechselnde Titel und Dokumentpfade werden nicht persistiert.
- Gleichartige Fenster desselben Programms teilen einen Eintrag; Hauptfenster und Dialoge bleiben getrennt.
- Not-Aus `Ctrl + Alt + Shift + F12` deaktiviert Snap-Funktion und Fensterautomatik dauerhaft.
- Fremde Benutzerfenster dürfen in automatisierten Tests nicht verschoben werden; native Integrationstests verwenden ausschliesslich eigene kontrollierte Testfenster.
- Vor jeder Aufgabe `git status --short` prüfen und vorhandene, nicht zur Aufgabe gehörende Änderungen unverändert lassen.
- Für Git im aktuellen Netzlaufwerk `git -c safe.directory='//192.168.1.32/temp/PortableApps/SaschaWindowZones'` verwenden.

---

## File Structure

### Core-Domäne

- `src/SnapZones.Core/Placement/WindowIdentity.cs`: stabile Anwendungs- und Fenstertypidentität.
- `src/SnapZones.Core/Placement/WindowPlacementEntry.cs`: gelernter Platzierungszustand und Katalog.
- `src/SnapZones.Core/Placement/WindowPlacementRule.cs`: Regelmodell, Aktion und Konfliktergebnis.
- `src/SnapZones.Core/Placement/PlacementGeometry.cs`: Normalisierung, Monitorrückfall, Begrenzung und Zonenklassifikation.
- `src/SnapZones.Core/Placement/PlacementRuleResolver.cs`: deterministische spezifischste Regel und Titel-Wildcards.
- `src/SnapZones.Core/Persistence/IWindowPlacementRepository.cs`: Lade-/Speichervertrag für dynamische Zustände.
- `src/SnapZones.Core/Persistence/JsonWindowPlacementRepository.cs`: atomare `placements.json` mit Sicherung und 500-Einträge-Grenze.

### Windows-Schicht

- `src/SnapZones.Windows/Hooks/IWindowLifecycleHook.cs`: normalisierte Fenster-Lebenszyklusereignisse.
- `src/SnapZones.Windows/Hooks/WindowLifecycleHook.cs`: getrennte `SetWinEventHook`-Registrierungen und Schutzschalter.
- `src/SnapZones.Windows/Windows/IPlacementWindowService.cs`: Fenster lesen, auflisten und ohne Aktivierung platzieren.
- `src/SnapZones.Windows/Windows/PlacementWindowSnapshot.cs`: native Momentaufnahme für Core und App.
- `src/SnapZones.Windows/Windows/WindowsPlacementWindowService.cs`: Eignungsprüfung, AppUserModelId/Prozesspfad, Normalposition und Maximierung.
- `src/SnapZones.Windows/Windows/WindowsIntegrityLevelReader.cs`: dokumentierte Integritätsprüfung vor Schreibzugriffen.
- `src/SnapZones.Windows/Windows/WindowSelectionService.cs`: einmalige Auswahl des nächsten fremden Vordergrundfensters für die UI.
- `src/SnapZones.Windows/Native/User32.cs`, `NativeTypes.cs` und `Shell32.cs`: ausschliesslich zusätzlich benötigte dokumentierte Interop-Verträge.

### App-Schicht und UI

- `src/SnapZones.App/Services/WindowPlacementSaveCoordinator.cs`: 750-ms-Bündelung und Flush.
- `src/SnapZones.App/Services/ApplicationDataPaths.cs`: gemeinsamer normaler oder portabler Datenpfad für `settings.json` und `placements.json`.
- `src/SnapZones.App/Services/ApplicationControllerDependencies.cs`: injizierbare produktive Windows- und Placement-Abhängigkeiten.
- `src/SnapZones.App/Services/IWindowPlacementEngine.cs`: vom Controller testbar verwendeter Engine-Vertrag.
- `src/SnapZones.App/Services/WindowPlacementEngine.cs`: Ereignisfluss, Wiederholungen, Lernen und Regelanwendung.
- `src/SnapZones.App/ViewModels/WindowPlacementViewModel.cs`: gelernte Einträge, Auswahl und Regelaktionen.
- `src/SnapZones.App/ViewModels/WindowPlacementItemViewModel.cs`: einzelne lesbare Listenzeile.
- Bestehende App-, Controller-, ViewModel- und MainWindow-Dateien verbinden Start, Not-Aus, AutoSave und UI.

---

### Task 1: Platzierungsdomäne und monitorfeste Geometrie

**Files:**
- Create: `src/SnapZones.Core/Placement/WindowIdentity.cs`
- Create: `src/SnapZones.Core/Placement/WindowPlacementEntry.cs`
- Create: `src/SnapZones.Core/Placement/PlacementGeometry.cs`
- Test: `tests/SnapZones.Tests/Placement/PlacementGeometryTests.cs`

**Interfaces:**
- Consumes: `PixelRect`, `MonitorWorkArea`, `NormalizedRect`.
- Produces: `WindowIdentity`, `WindowKind`, `WindowPlacementEntry`, `WindowPlacementCatalog`, `PlacementMonitorTarget`, `PlacementZoneTarget` und `PlacementGeometry.Resolve/Normalize/ClassifyZone`.

- [ ] **Step 1: Schreib die fehlschlagenden Geometrietests**

```csharp
using SnapZones.Core.Geometry;
using SnapZones.Core.Models;
using SnapZones.Core.Placement;
using Xunit;

namespace SnapZones.Tests.Placement;

public sealed class PlacementGeometryTests
{
    private static readonly WindowIdentity Identity = new("C:\\Apps\\excel.exe", "XLMAIN", WindowKind.MainWindow);

    [Fact]
    public void Resolve_uses_exact_pixels_when_the_saved_work_area_is_unchanged()
    {
        var entry = Entry("DISPLAY-A", new PixelRect(120, 80, 1200, 800));
        var actual = PlacementGeometry.Resolve(
            entry,
            [new PlacementMonitorTarget("DISPLAY-A", new MonitorWorkArea(0, 0, 1920, 1080), true)],
            []);
        Assert.Equal(new PixelRect(120, 80, 1200, 800), actual);
    }

    [Fact]
    public void Resolve_maps_to_the_primary_monitor_and_keeps_the_window_visible_when_saved_monitor_is_missing()
    {
        var entry = Entry("MISSING", new PixelRect(1500, 700, 900, 700));
        var actual = PlacementGeometry.Resolve(
            entry,
            [new PlacementMonitorTarget("DISPLAY-B", new MonitorWorkArea(100, 50, 1280, 720), true)],
            []);
        Assert.True(actual.X >= 100 && actual.Y >= 50);
        Assert.True(actual.Right <= 1380 && actual.Bottom <= 770);
        Assert.True(actual.Width >= 160 && actual.Height >= 120);
    }

    [Fact]
    public void ClassifyZone_returns_the_unique_zone_with_at_least_twenty_five_percent_overlap()
    {
        var profile = Guid.NewGuid();
        var zones = new[]
        {
            new PlacementZoneTarget(profile, Guid.NewGuid(), "DISPLAY-A", new PixelRect(0, 0, 960, 1080)),
            new PlacementZoneTarget(profile, Guid.NewGuid(), "DISPLAY-A", new PixelRect(960, 0, 960, 1080))
        };
        Assert.Equal(zones[1].ZoneId, PlacementGeometry.ClassifyZone(new PixelRect(1000, 100, 800, 800), zones));
    }

    private static WindowPlacementEntry Entry(string monitorId, PixelRect bounds) => new(
        Identity, monitorId, null, new MonitorWorkArea(0, 0, 1920, 1080), bounds,
        PlacementGeometry.Normalize(bounds, new MonitorWorkArea(0, 0, 1920, 1080)),
        false, DateTimeOffset.Parse("2026-08-30T12:00:00Z"));
}
```

- [ ] **Step 2: Führe den Test aus und bestätige das erwartete Scheitern**

```powershell
dotnet test tests\SnapZones.Tests\SnapZones.Tests.csproj -c Release --filter FullyQualifiedName~PlacementGeometryTests
```

Expected: FAIL mit fehlendem Namespace oder fehlenden Typen aus `SnapZones.Core.Placement`.

- [ ] **Step 3: Implementiere die Domänentypen und den Algorithmus**

```csharp
public enum WindowKind { MainWindow, Dialog }
public sealed record WindowIdentity(string ApplicationKey, string WindowClass, WindowKind Kind);
public sealed record WindowPlacementEntry(
    WindowIdentity Identity, string MonitorStableId, Guid? ZoneId,
    MonitorWorkArea SourceWorkArea, PixelRect NormalBoundsPixels,
    NormalizedRect NormalBoundsNormalized, bool WasMaximized, DateTimeOffset LastUpdatedUtc);
public sealed record WindowPlacementCatalog(int SchemaVersion, IReadOnlyList<WindowPlacementEntry> Entries)
{
    public const int CurrentSchemaVersion = 1;
    public static WindowPlacementCatalog Empty { get; } = new(CurrentSchemaVersion, []);
}
public sealed record PlacementMonitorTarget(string StableId, MonitorWorkArea WorkArea, bool IsPrimary);
public sealed record PlacementZoneTarget(Guid ProfileId, Guid ZoneId, string MonitorStableId, PixelRect Bounds);
```

`PlacementGeometry` erhält diese Signaturen:

```csharp
public static NormalizedRect Normalize(PixelRect bounds, MonitorWorkArea workArea);
public static PixelRect Resolve(WindowPlacementEntry entry, IReadOnlyList<PlacementMonitorTarget> monitors, IReadOnlyList<PlacementZoneTarget> zones);
public static Guid? ClassifyZone(PixelRect bounds, IReadOnlyList<PlacementZoneTarget> zones);
```

`Resolve` sucht gespeicherten Monitor, sonst Monitor der gespeicherten Zone, sonst primären, sonst ersten Monitor. Bei identischer Arbeitsfläche nutzt es exakte Pixel; sonst bildet es `NormalBoundsNormalized` ab. Danach begrenzt es Breite auf `[160, workArea.Width]`, Höhe auf `[120, workArea.Height]` sowie X/Y vollständig auf die Arbeitsfläche. `ClassifyZone` verwendet Schnittfläche geteilt durch Fensterfläche, verwirft Werte unter `0.25` und liefert bei gleichem Höchstwert `null`.

- [ ] **Step 4: Führe die fokussierten Tests aus**

```powershell
dotnet test tests\SnapZones.Tests\SnapZones.Tests.csproj -c Release --filter FullyQualifiedName~PlacementGeometryTests
```

Expected: PASS.

- [ ] **Step 5: Committe nur die Platzierungsdomäne**

```powershell
git -c safe.directory='//192.168.1.32/temp/PortableApps/SaschaWindowZones' add -- src/SnapZones.Core/Placement tests/SnapZones.Tests/Placement/PlacementGeometryTests.cs
git -c safe.directory='//192.168.1.32/temp/PortableApps/SaschaWindowZones' commit -m "feat: add window placement geometry"
```

---

### Task 2: Spezifische Regeln, Wildcards und Konflikte

**Files:**
- Create: `src/SnapZones.Core/Placement/WindowPlacementRule.cs`
- Create: `src/SnapZones.Core/Placement/PlacementRuleResolver.cs`
- Test: `tests/SnapZones.Tests/Placement/PlacementRuleResolverTests.cs`

**Interfaces:**
- Consumes: `WindowIdentity` und `WindowKind` aus Task 1.
- Produces: `WindowPlacementMode`, `WindowPlacementRule`, `RuleResolution` und `PlacementRuleResolver.Resolve`.

- [ ] **Step 1: Schreib Tests für spezifische Regeln und Konflikte**

```csharp
[Fact]
public void Resolve_prefers_a_class_rule_over_an_application_only_exclusion()
{
    var identity = new WindowIdentity("C:\\Apps\\excel.exe", "XLMAIN", WindowKind.MainWindow);
    var general = Rule(identity, WindowPlacementMode.Exclude);
    var specific = Rule(identity, WindowPlacementMode.FixedZone) with { WindowClass = "XLMAIN", ZoneId = Guid.NewGuid() };
    var result = PlacementRuleResolver.Resolve(identity, "Budget.xlsx - Excel", [general, specific]);
    Assert.False(result.HasConflict);
    Assert.Equal(specific.Id, result.Rule!.Id);
}

[Fact]
public void Resolve_reports_a_conflict_for_two_equally_specific_rules()
{
    var identity = new WindowIdentity("C:\\Apps\\excel.exe", "XLMAIN", WindowKind.MainWindow);
    var first = Rule(identity, WindowPlacementMode.Exclude) with { WindowClass = "XLMAIN" };
    var second = Rule(identity, WindowPlacementMode.RememberLast) with { WindowClass = "XLMAIN" };
    var result = PlacementRuleResolver.Resolve(identity, "Excel", [first, second]);
    Assert.True(result.HasConflict);
    Assert.Null(result.Rule);
}
```

Der lokale Testhelfer erzeugt `WindowPlacementRule` mit eindeutiger ID, aktivem Zustand, ApplicationKey und allen optionalen Feldern `null`.

```csharp
private static WindowPlacementRule Rule(WindowIdentity identity, WindowPlacementMode mode) => new(
    Guid.NewGuid(), true, identity.ApplicationKey,
    null, null, null, mode, null, null, null);
```

- [ ] **Step 2: Bestätige das Scheitern**

```powershell
dotnet test tests\SnapZones.Tests\SnapZones.Tests.csproj -c Release --filter FullyQualifiedName~PlacementRuleResolverTests
```

Expected: FAIL wegen fehlendem Regelmodell und Resolver.

- [ ] **Step 3: Implementiere Regelmodell und Auflösung**

```csharp
public enum WindowPlacementMode { RememberLast, FixedZone, Exclude }
public sealed record WindowPlacementRule(
    Guid Id, bool IsEnabled, string ApplicationKey, string? WindowClass,
    WindowKind? WindowKind, string? TitlePattern, WindowPlacementMode Action,
    Guid? ProfileId, string? MonitorStableId, Guid? ZoneId);
public sealed record RuleResolution(WindowPlacementRule? Rule, bool HasConflict);
```

`Resolve` filtert `IsEnabled` und `ApplicationKey` ordinal-ignore-case. Optionale Klasse und Art müssen exakt passen. `TitlePattern` wird mit `Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", ".")`, `RegexOptions.IgnoreCase | RegexOptions.CultureInvariant` und 100-ms-Timeout geprüft. Spezifität: Titel `4`, Klasse `2`, Art `1`; eine Maximalregel gewinnt, mehrere Maximalregeln ergeben Konflikt.

- [ ] **Step 4: Führe Regeln und Geometrie aus**

```powershell
dotnet test tests\SnapZones.Tests\SnapZones.Tests.csproj -c Release --filter "FullyQualifiedName~PlacementRuleResolverTests|FullyQualifiedName~PlacementGeometryTests"
```

Expected: PASS.

- [ ] **Step 5: Committe den Regelkern**

```powershell
git -c safe.directory='//192.168.1.32/temp/PortableApps/SaschaWindowZones' add -- src/SnapZones.Core/Placement/WindowPlacementRule.cs src/SnapZones.Core/Placement/PlacementRuleResolver.cs tests/SnapZones.Tests/Placement/PlacementRuleResolverTests.cs
git -c safe.directory='//192.168.1.32/temp/PortableApps/SaschaWindowZones' commit -m "feat: resolve window placement rules"
```

---

### Task 3: Atomarer dynamischer Platzierungsspeicher

**Files:**
- Create: `src/SnapZones.Core/Persistence/IWindowPlacementRepository.cs`
- Create: `src/SnapZones.Core/Persistence/WindowPlacementLoadResult.cs`
- Create: `src/SnapZones.Core/Persistence/JsonWindowPlacementRepository.cs`
- Test: `tests/SnapZones.Tests/Persistence/JsonWindowPlacementRepositoryTests.cs`

**Interfaces:**
- Consumes: `WindowPlacementCatalog`, `WindowPlacementEntry`, `JsonConfigurationRepository.CreateSerializerOptions()`.
- Produces: `IWindowPlacementRepository.LoadAsync/SaveAsync` und belastbare `placements.json`.

- [ ] **Step 1: Schreib Speicher-, Sicherungs- und Begrenzungstests**

```csharp
[Fact]
public async Task Save_then_load_is_atomic_and_keeps_only_the_500_newest_entries()
{
    using var directory = new TemporaryDirectory();
    var repository = new JsonWindowPlacementRepository(directory.Path);
    var entries = Enumerable.Range(0, 501).Select(CreateEntry).ToArray();
    await repository.SaveAsync(new WindowPlacementCatalog(1, entries), CancellationToken.None);
    var loaded = await repository.LoadAsync(CancellationToken.None);
    Assert.Equal(500, loaded.Catalog.Entries.Count);
    Assert.DoesNotContain(loaded.Catalog.Entries, item => item.Identity.WindowClass == "Class-0");
    Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
}

[Fact]
public async Task Load_renames_corrupt_primary_and_recovers_the_last_valid_backup()
{
    using var directory = new TemporaryDirectory();
    var repository = new JsonWindowPlacementRepository(directory.Path);
    var expected = new WindowPlacementCatalog(1, [CreateEntry(1)]);
    await repository.SaveAsync(expected, CancellationToken.None);
    await repository.SaveAsync(new WindowPlacementCatalog(1, [CreateEntry(2)]), CancellationToken.None);
    await File.WriteAllTextAsync(Path.Combine(directory.Path, "placements.json"), "{");
    var loaded = await repository.LoadAsync(CancellationToken.None);
    Assert.True(loaded.RecoveredFromError);
    Assert.Equal(expected.Entries, loaded.Catalog.Entries);
    Assert.Single(Directory.GetFiles(directory.Path, "placements.invalid-*.json"));
}
```

`CreateEntry(index)` verwendet eindeutige `WindowIdentity`, steigendes `LastUpdatedUtc` und gültige 800×600-Geometrie.

```csharp
private static WindowPlacementEntry CreateEntry(int index) => new(
    new WindowIdentity($"C:\\Apps\\App-{index}.exe", $"Class-{index}", WindowKind.MainWindow),
    "DISPLAY-A", null, new MonitorWorkArea(0, 0, 1920, 1080),
    new PixelRect(10, 10, 800, 600), new NormalizedRect(0, 0, 0.5, 0.5),
    false, DateTimeOffset.UnixEpoch.AddMinutes(index));
```

- [ ] **Step 2: Bestätige das Scheitern**

```powershell
dotnet test tests\SnapZones.Tests\SnapZones.Tests.csproj -c Release --filter FullyQualifiedName~JsonWindowPlacementRepositoryTests
```

Expected: FAIL wegen fehlendem Repository.

- [ ] **Step 3: Implementiere Vertrag und JSON-Ablage**

```csharp
public interface IWindowPlacementRepository
{
    Task<WindowPlacementLoadResult> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(WindowPlacementCatalog catalog, CancellationToken cancellationToken);
}
public sealed record WindowPlacementLoadResult(WindowPlacementCatalog Catalog, bool RecoveredFromError, string? ErrorMessage = null);
```

Das Repository verwendet `placements.json`, `placements.backup-1.json` und `placements.<guid>.tmp`. Vor dem Schreiben sortiert es absteigend nach `LastUpdatedUtc` und nimmt 500 Einträge. Besteht die Primärdatei, nutzt es `File.Replace(temp, primary, backup, true)`, sonst `File.Move`. Ungültige Primärdaten werden zeitgestempelt als `placements.invalid-*.json` verschoben; danach wird die Sicherung geladen und wieder als Primärdatei gespeichert, sonst `WindowPlacementCatalog.Empty` geliefert.

- [ ] **Step 4: Führe Platzierungs- und Konfigurationsrepositorytests aus**

```powershell
dotnet test tests\SnapZones.Tests\SnapZones.Tests.csproj -c Release --filter "FullyQualifiedName~JsonWindowPlacementRepositoryTests|FullyQualifiedName~JsonConfigurationRepositoryTests"
```

Expected: PASS.

- [ ] **Step 5: Committe den getrennten Speicher**

```powershell
git -c safe.directory='//192.168.1.32/temp/PortableApps/SaschaWindowZones' add -- src/SnapZones.Core/Persistence/IWindowPlacementRepository.cs src/SnapZones.Core/Persistence/WindowPlacementLoadResult.cs src/SnapZones.Core/Persistence/JsonWindowPlacementRepository.cs tests/SnapZones.Tests/Persistence/JsonWindowPlacementRepositoryTests.cs
git -c safe.directory='//192.168.1.32/temp/PortableApps/SaschaWindowZones' commit -m "feat: persist learned window placements"
```

---

### Task 4: Konfigurationsschema 2 und verlustfreie Migration

**Files:**
- Modify: `src/SnapZones.Core/Models/AppSettings.cs:22-56`
- Modify: `src/SnapZones.Core/Models/SnapConfiguration.cs:3-18`
- Modify: `src/SnapZones.Core/Persistence/JsonConfigurationRepository.cs:20-212`
- Modify: `tests/SnapZones.Tests/Models/ConfigurationDefaultsTests.cs`
- Modify: `tests/SnapZones.Tests/Persistence/JsonConfigurationRepositoryTests.cs:80-121`
- Create: `tests/SnapZones.Tests/Persistence/WindowPlacementMigrationTests.cs`

**Interfaces:**
- Consumes: `WindowPlacementRule`.
- Produces: `RestoreWindowPlacementEnabled`, `WindowPlacementRules`, `EffectiveWindowPlacementRules`, Schema 2.

- [ ] **Step 1: Schreib den Migrationstest für reale Schema-1-Daten**

```csharp
[Fact]
public async Task Load_migrates_schema_one_to_two_without_treating_it_as_corrupt()
{
    using var directory = new TemporaryDirectory();
    var profileId = "11111111-1111-1111-1111-111111111111";
    var json = $$"""
    { "SchemaVersion": 1, "Settings": {
      "ActiveProfileId": "{{profileId}}", "SnappingEnabled": false,
      "StartWithWindows": false, "OverlayScope": "AllMonitors",
      "TriggerMode": "Immediate", "OuterMargin": 8, "ZoneGap": 8,
      "OverlayColor": "#707070", "OverlayOpacity": 0.24 },
      "Profiles": [{ "Id": "{{profileId}}", "Name": "Standard", "QuickSlot": 1, "Monitors": [] }] }
    """;
    await File.WriteAllTextAsync(Path.Combine(directory.Path, "settings.json"), json);
    var result = await new JsonConfigurationRepository(directory.Path).LoadAsync(CancellationToken.None);
    Assert.False(result.RecoveredFromError);
    Assert.Equal(2, result.Configuration.SchemaVersion);
    Assert.True(result.Configuration.Settings.RestoreWindowPlacementEnabled);
    Assert.Empty(result.Configuration.Settings.EffectiveWindowPlacementRules);
    Assert.Empty(Directory.GetFiles(directory.Path, "settings.invalid-*.json"));
}
```

- [ ] **Step 2: Bestätige das Scheitern**

```powershell
dotnet test tests\SnapZones.Tests\SnapZones.Tests.csproj -c Release --filter FullyQualifiedName~WindowPlacementMigrationTests
```

Expected: FAIL, weil Schema 1 aktuell nicht nach Schema 2 migriert wird.

- [ ] **Step 3: Ergänze Felder, Migration und Validierung**

Erweitere `AppSettings` nach `OuterMargins`:

```csharp
bool RestoreWindowPlacementEnabled = true,
IReadOnlyList<WindowPlacementRule>? WindowPlacementRules = null)
{
    public IReadOnlyList<WindowPlacementRule> EffectiveWindowPlacementRules => WindowPlacementRules ?? [];
```

Setze `CurrentSchemaVersion = 2` und rufe vor jeder Validierung folgenden Migrator auf:

```csharp
private static SnapConfiguration Migrate(SnapConfiguration configuration) => configuration.SchemaVersion switch
{
    1 => configuration with
    {
        SchemaVersion = SnapConfiguration.CurrentSchemaVersion,
        Settings = configuration.Settings with
        {
            RestoreWindowPlacementEnabled = true,
            WindowPlacementRules = configuration.Settings.EffectiveWindowPlacementRules
        }
    },
    SnapConfiguration.CurrentSchemaVersion => configuration,
    _ => throw new InvalidDataException("Die Konfigurationsversion wird nicht unterstützt.")
};
```

`Validate` prüft eindeutige Regel-IDs und nicht leere ApplicationKeys, akzeptiert aber fehlende Zielmonitore oder Zielzonen als pausierbare Regeln. Die neuen AppSettings-Parameter bleiben optional, damit bestehende Konstruktoraufrufe bauen.

- [ ] **Step 4: Führe Modell- und Migrationstests aus**

```powershell
dotnet test tests\SnapZones.Tests\SnapZones.Tests.csproj -c Release --filter "FullyQualifiedName~ConfigurationDefaultsTests|FullyQualifiedName~JsonConfigurationRepositoryTests|FullyQualifiedName~WindowPlacementMigrationTests"
```

Expected: PASS; der bestehende Schema-1-Kompatibilitätstest erwartet zusätzlich Schema 2 und aktivierte Fensterautomatik.

- [ ] **Step 5: Committe die Schema-Migration**

```powershell
git -c safe.directory='//192.168.1.32/temp/PortableApps/SaschaWindowZones' add -- src/SnapZones.Core/Models/AppSettings.cs src/SnapZones.Core/Models/SnapConfiguration.cs src/SnapZones.Core/Persistence/JsonConfigurationRepository.cs tests/SnapZones.Tests/Models/ConfigurationDefaultsTests.cs tests/SnapZones.Tests/Persistence/JsonConfigurationRepositoryTests.cs tests/SnapZones.Tests/Persistence/WindowPlacementMigrationTests.cs
git -c safe.directory='//192.168.1.32/temp/PortableApps/SaschaWindowZones' commit -m "feat: migrate configuration for window placement"
```

---

### Task 5: Eng begrenzter Fenster-Lebenszyklus-Hook

**Files:**
- Create: `src/SnapZones.Windows/Hooks/IWindowLifecycleHook.cs`
- Create: `src/SnapZones.Windows/Hooks/WindowLifecycleHook.cs`
- Modify: `src/SnapZones.Windows/Native/User32.cs:11-124`
- Test: `tests/SnapZones.Tests/Windows/WindowLifecycleHookTests.cs`

**Interfaces:**
- Consumes: `User32.WinEventProc`, `HookCircuitBreaker`, `SynchronizationContext`.
- Produces: `WindowLifecycleEventKind`, `WindowLifecycleEvent`, `IWindowLifecycleHook`, `WindowLifecycleHook`.

- [ ] **Step 1: Schreib Mapping-, Startzustands- und echte Hook-Tests**

```csharp
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SnapZones.Windows.Hooks;
using Xunit;

namespace SnapZones.Tests.Windows;

public sealed class WindowLifecycleHookTests
{
    [Fact]
    public void Hook_is_disabled_until_enabled()
    {
        using var hook = new WindowLifecycleHook(new SynchronizationContext());
        Assert.False(hook.IsEnabled);
    }

    [Theory]
    [InlineData(0x8002, WindowLifecycleEventKind.Shown)]
    [InlineData(0x8003, WindowLifecycleEventKind.Hidden)]
    [InlineData(0x8001, WindowLifecycleEventKind.Destroyed)]
    [InlineData(0x800B, WindowLifecycleEventKind.LocationChanged)]
    [InlineData(0x000B, WindowLifecycleEventKind.MoveSizeEnded)]
    [InlineData(0x0017, WindowLifecycleEventKind.MinimizeEnded)]
    public void Map_translates_required_events(uint nativeEvent, WindowLifecycleEventKind expected) =>
        Assert.Equal(expected, WindowLifecycleHook.Map(nativeEvent));

    [Fact]
    public void Hook_receives_a_show_event_from_an_owned_test_window()
    {
        using var hook = new WindowLifecycleHook(new SynchronizationContext());
        using var window = new Form();
        using var received = new ManualResetEventSlim();
        hook.EventReceived += item =>
        {
            if (item.WindowHandle == window.Handle && item.Kind == WindowLifecycleEventKind.Shown) received.Set();
        };
        hook.Enable();
        NotifyWinEvent(0x8002, window.Handle, 0, 0);
        Assert.True(received.Wait(TimeSpan.FromSeconds(2)));
    }

    [DllImport("user32.dll")]
    private static extern void NotifyWinEvent(uint eventType, nint window, int objectId, int childId);
}
```

- [ ] **Step 2: Bestätige das Scheitern**

```powershell
dotnet test tests\SnapZones.Tests\SnapZones.Tests.csproj -c Release --filter FullyQualifiedName~WindowLifecycleHookTests
```

Expected: FAIL wegen fehlendem Hook.

- [ ] **Step 3: Implementiere Interface und sechs exakte Registrierungen**

```csharp
public enum WindowLifecycleEventKind { Shown, Hidden, Destroyed, LocationChanged, MoveSizeEnded, MinimizeEnded }
public sealed record WindowLifecycleEvent(nint WindowHandle, WindowLifecycleEventKind Kind);
public interface IWindowLifecycleHook : IDisposable
{
    event Action<WindowLifecycleEvent>? EventReceived;
    event Action<string>? EmergencyStopped;
    bool IsEnabled { get; }
    void Enable();
    void Disable();
}
```

`Enable` registriert `SetWinEventHook(eventType, eventType, ...)` einzeln für `0x8002`, `0x8003`, `0x8001`, `0x800B`, `0x000B`, `0x0017` und speichert alle Handles. Schlägt eine Registrierung fehl, löst es bereits registrierte Handles und wirft `Win32Exception`. Der Callback akzeptiert nur `window != 0`, `objectId == 0`, `childId == 0`, verwendet `HookCircuitBreaker(2000, TimeSpan.FromSeconds(10))` und postet auf den Synchronisationskontext. `Disable` löst alle Handles idempotent.

- [ ] **Step 4: Führe neue und bestehende Hook-Sicherheitstests aus**

```powershell
dotnet test tests\SnapZones.Tests\SnapZones.Tests.csproj -c Release --filter "FullyQualifiedName~WindowLifecycleHookTests|FullyQualifiedName~WindowsSafetyBoundaryTests"
```

Expected: PASS.

- [ ] **Step 5: Committe den Hook**

```powershell
git -c safe.directory='//192.168.1.32/temp/PortableApps/SaschaWindowZones' add -- src/SnapZones.Windows/Hooks/IWindowLifecycleHook.cs src/SnapZones.Windows/Hooks/WindowLifecycleHook.cs src/SnapZones.Windows/Native/User32.cs tests/SnapZones.Tests/Windows/WindowLifecycleHookTests.cs
git -c safe.directory='//192.168.1.32/temp/PortableApps/SaschaWindowZones' commit -m "feat: observe window lifecycle events"
```

---

### Task 6: Native Fensteridentität, Eignung und Platzierung

**Files:**
- Create: `src/SnapZones.Windows/Windows/PlacementWindowSnapshot.cs`
- Create: `src/SnapZones.Windows/Windows/IPlacementWindowService.cs`
- Create: `src/SnapZones.Windows/Windows/WindowsPlacementWindowService.cs`
- Create: `src/SnapZones.Windows/Windows/WindowsIntegrityLevelReader.cs`
- Create: `src/SnapZones.Windows/Native/Shell32.cs`
- Modify: `src/SnapZones.Windows/Native/User32.cs:73-124`
- Modify: `src/SnapZones.Windows/Native/NativeTypes.cs:1-20`
- Test: `tests/SnapZones.Tests/Windows/WindowsPlacementWindowServiceTests.cs`

**Interfaces:**
- Consumes: `WindowIdentity`, `WindowKind`, `PixelRect`.
- Produces: `PlacementWindowSnapshot`, `IPlacementWindowService.Inspect/TryPlace/EnumerateEligibleWindows/GetForegroundWindow`.

- [ ] **Step 1: Schreib kontrollierte Fenster-Service-Tests**

```csharp
[Fact]
public void Inspect_reads_class_normal_bounds_and_main_kind_from_a_controlled_form()
{
    using var form = new Form { Bounds = new System.Drawing.Rectangle(120, 90, 900, 600), Text = "Placement test" };
    form.Show();
    var snapshot = new WindowsPlacementWindowService().Inspect(form.Handle, excludedProcessId: -1);
    Assert.NotNull(snapshot);
    Assert.Equal(WindowKind.MainWindow, snapshot.Identity.Kind);
    Assert.Equal(new PixelRect(120, 90, 900, 600), snapshot.NormalBounds);
    Assert.False(snapshot.IsMinimized);
}

[Fact]
public void Inspect_classifies_an_owned_normal_window_as_dialog()
{
    using var owner = new Form();
    using var dialog = new Form();
    owner.Show();
    dialog.Show(owner);
    var snapshot = new WindowsPlacementWindowService().Inspect(dialog.Handle, excludedProcessId: -1);
    Assert.NotNull(snapshot);
    Assert.Equal(WindowKind.Dialog, snapshot.Identity.Kind);
}

[Fact]
public void TryPlace_rejects_an_invalid_handle_without_side_effects() =>
    Assert.False(new WindowsPlacementWindowService().TryPlace(0, new PixelRect(10, 10, 800, 600), false));

[Fact]
public void Inspect_rejects_the_excluded_process_and_owned_tool_windows()
{
    using var main = new Form();
    using var tool = new Form { ShowInTaskbar = false, FormBorderStyle = FormBorderStyle.FixedToolWindow };
    main.Show();
    tool.Show(main);
    var service = new WindowsPlacementWindowService();
    Assert.Null(service.Inspect(main.Handle, Environment.ProcessId));
    Assert.Null(service.Inspect(tool.Handle, excludedProcessId: -1));
}
```

- [ ] **Step 2: Bestätige das Scheitern**

```powershell
dotnet test tests\SnapZones.Tests\SnapZones.Tests.csproj -c Release --filter FullyQualifiedName~WindowsPlacementWindowServiceTests
```

Expected: FAIL wegen fehlendem Service.

- [ ] **Step 3: Ergänze die öffentlichen Service-Typen**

```csharp
public sealed record PlacementWindowSnapshot(
    nint WindowHandle, WindowIdentity Identity, string Title,
    PixelRect CurrentBounds, PixelRect NormalBounds,
    bool IsMaximized, bool IsMinimized);
public interface IPlacementWindowService
{
    PlacementWindowSnapshot? Inspect(nint windowHandle, int excludedProcessId);
    bool TryPlace(nint windowHandle, PixelRect normalBounds, bool maximize);
    IReadOnlyList<nint> EnumerateEligibleWindows(int excludedProcessId);
    nint GetForegroundWindow();
}
```

- [ ] **Step 4: Implementiere die dokumentierten nativen Pfade**

Ergänze `WINDOWPLACEMENT`, `POINT`, `GetWindowPlacement`, `SetWindowPlacement`, `GetClassNameW`, `GetWindowTextW`, `GetWindow`, `EnumWindows`, `GetForegroundWindow`, `OpenProcess`, `CloseHandle`, `OpenProcessToken`, `GetTokenInformation` und SID-Unterfunktionen. `Inspect` liefert `null` bei ungültig, unsichtbar, cloaked, Kindfenster, Toolfenster, Shell-Klassen `Progman`, `WorkerW`, `Shell_TrayWnd`, höherer Integrität oder Zielprozess gleich `excludedProcessId`; dadurch werden sämtliche eigenen Verwaltungsfenster ausgeschlossen.

Die Identität wird exakt so priorisiert:

```csharp
var applicationKey = Shell32.TryReadAppUserModelId(windowHandle)
    ?? Path.GetFullPath(process.MainModule!.FileName!);
var windowClass = ReadWindowClass(windowHandle);
var kind = User32.GetWindow(windowHandle, 4) != 0 || windowClass == "#32770"
    ? WindowKind.Dialog
    : WindowKind.MainWindow;
```

`Shell32.TryReadAppUserModelId` verwendet `SHGetPropertyStoreForWindow`, Format-ID `9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3`, Property-ID `5`, liest den `PROPVARIANT`-String und gibt bei COM-/Property-Fehler `null` zurück. `TryPlace` schreibt `WINDOWPLACEMENT.rcNormalPosition`, setzt `showCmd` auf `SW_SHOWMAXIMIZED` oder `SW_SHOWNORMAL` und ruft nie `SetForegroundWindow` auf. `WindowsIntegrityLevelReader.CanControl` vergleicht den letzten RID des Token-Integrity-SID des eigenen und des Zielprozesses; ein nicht lesbares Zieltoken ergibt `false`.

- [ ] **Step 5: Führe Fenster-Service- und Sicherheitsgrenztests aus**

```powershell
dotnet test tests\SnapZones.Tests\SnapZones.Tests.csproj -c Release --filter "FullyQualifiedName~WindowsPlacementWindowServiceTests|FullyQualifiedName~WindowsSafetyBoundaryTests"
```

Expected: PASS; kein Test verschiebt ein fremdes Fenster.

- [ ] **Step 6: Committe den nativen Platzierungsservice**

```powershell
git -c safe.directory='//192.168.1.32/temp/PortableApps/SaschaWindowZones' add -- src/SnapZones.Windows/Windows/PlacementWindowSnapshot.cs src/SnapZones.Windows/Windows/IPlacementWindowService.cs src/SnapZones.Windows/Windows/WindowsPlacementWindowService.cs src/SnapZones.Windows/Windows/WindowsIntegrityLevelReader.cs src/SnapZones.Windows/Native/Shell32.cs src/SnapZones.Windows/Native/User32.cs src/SnapZones.Windows/Native/NativeTypes.cs tests/SnapZones.Tests/Windows/WindowsPlacementWindowServiceTests.cs
git -c safe.directory='//192.168.1.32/temp/PortableApps/SaschaWindowZones' commit -m "feat: inspect and place eligible windows"
```

---

### Task 7: Gebündeltes Speichern mit sicherem Flush

**Files:**
- Create: `src/SnapZones.App/Services/WindowPlacementSaveCoordinator.cs`
- Test: `tests/SnapZones.Tests/Services/WindowPlacementSaveCoordinatorTests.cs`

**Interfaces:**
- Consumes: `IWindowPlacementRepository`, `WindowPlacementCatalog`.
- Produces: `RequestSave`, `FlushAsync`, `SaveFinished`.

- [ ] **Step 1: Schreib Tests für Bündelung und Flush**

```csharp
[Fact]
public async Task Multiple_requests_inside_the_debounce_window_write_only_the_latest_catalog()
{
    var repository = new RecordingPlacementRepository();
    var coordinator = new WindowPlacementSaveCoordinator(repository, TimeSpan.FromMilliseconds(20));
    coordinator.RequestSave(new WindowPlacementCatalog(1, []));
    coordinator.RequestSave(new WindowPlacementCatalog(1, [CreateEntry("latest")]));
    await coordinator.FlushAsync(CancellationToken.None);
    Assert.Single(repository.Saved);
    Assert.Equal("latest", repository.Saved[0].Entries[0].Identity.WindowClass);
}

[Fact]
public async Task Flush_surfaces_the_last_repository_error()
{
    var coordinator = new WindowPlacementSaveCoordinator(new ThrowingPlacementRepository(), TimeSpan.Zero);
    coordinator.RequestSave(WindowPlacementCatalog.Empty);
    await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.FlushAsync(CancellationToken.None));
}
```

Die Testdatei enthält lokale Fakes für `IWindowPlacementRepository`; `RecordingPlacementRepository.Saved` ist eine `List<WindowPlacementCatalog>`.

- [ ] **Step 2: Bestätige das Scheitern**

```powershell
dotnet test tests\SnapZones.Tests\SnapZones.Tests.csproj -c Release --filter FullyQualifiedName~WindowPlacementSaveCoordinatorTests
```

Expected: FAIL wegen fehlendem Coordinator.

- [ ] **Step 3: Implementiere eine threadsichere Latest-Wins-Schleife**

Übernimm Sperr- und Flush-Struktur von `ConfigurationSaveCoordinator`, ergänze vor dem Schreiben `await Task.Delay(debounceDelay)` und lies danach unter Sperre nochmals den neuesten Katalog. Während eines Schreibens eintreffende Anfragen erzeugen genau einen weiteren Zyklus. `FlushAsync` wartet, bis weder Worker noch ausstehender Katalog vorhanden ist, und wirft `InvalidOperationException("Die Fensterplatzierungen konnten nicht gespeichert werden.", lastException)`.

- [ ] **Step 4: Führe beide Save-Coordinator-Gruppen aus**

```powershell
dotnet test tests\SnapZones.Tests\SnapZones.Tests.csproj -c Release --filter "FullyQualifiedName~WindowPlacementSaveCoordinatorTests|FullyQualifiedName~ConfigurationSaveCoordinatorTests"
```

Expected: PASS.

- [ ] **Step 5: Committe die Schreibbündelung**

```powershell
git -c safe.directory='//192.168.1.32/temp/PortableApps/SaschaWindowZones' add -- src/SnapZones.App/Services/WindowPlacementSaveCoordinator.cs tests/SnapZones.Tests/Services/WindowPlacementSaveCoordinatorTests.cs
git -c safe.directory='//192.168.1.32/temp/PortableApps/SaschaWindowZones' commit -m "feat: debounce window placement saves"
```

---

### Task 8: Ereignisgesteuerte WindowPlacementEngine

**Files:**
- Create: `src/SnapZones.App/Services/PlacementEnvironment.cs`
- Create: `src/SnapZones.App/Services/IWindowPlacementEngine.cs`
- Create: `src/SnapZones.App/Services/WindowPlacementEngine.cs`
- Test: `tests/SnapZones.Tests/Services/WindowPlacementEngineTests.cs`

**Interfaces:**
- Consumes: Lifecycle-Hook, Window-Service, Core-Regeln/Geometrie, Save-Coordinator.
- Produces: `Start`, `Stop`, `EmergencyStop`, `ApplyNowAsync`, `Forget`, `RememberExplicitZone`, `FlushAsync`, `CatalogChanged`.

- [ ] **Step 1: Schreib Engine-Tests mit deterministischen Fakes**

```csharp
[Fact]
public async Task Shown_restores_a_remembered_window_once_and_does_not_hold_it_afterwards()
{
    var fixture = EngineFixture.WithRememberedWindow(maximized: true);
    fixture.Engine.Start();
    fixture.Hook.Raise(42, WindowLifecycleEventKind.Shown);
    fixture.Hook.Raise(42, WindowLifecycleEventKind.Shown);
    await fixture.DrainAsync();
    var placement = Assert.Single(fixture.Windows.Placements);
    Assert.Equal(42, placement.WindowHandle);
    Assert.True(placement.Maximize);
}

[Fact]
public async Task Exclusion_neither_places_nor_learns_the_window()
{
    var fixture = EngineFixture.WithRule(WindowPlacementMode.Exclude);
    fixture.Engine.Start();
    fixture.Hook.Raise(42, WindowLifecycleEventKind.Shown);
    fixture.Hook.Raise(42, WindowLifecycleEventKind.MoveSizeEnded);
    await fixture.DrainAsync();
    Assert.Empty(fixture.Windows.Placements);
    Assert.Empty(fixture.Engine.Catalog.Entries);
}

[Fact]
public async Task Not_ready_window_is_attempted_at_most_three_times()
{
    var fixture = EngineFixture.WithUnreadableWindow();
    fixture.Engine.Start();
    fixture.Hook.Raise(42, WindowLifecycleEventKind.Shown);
    await fixture.DrainAsync();
    Assert.Equal(3, fixture.Windows.InspectCalls);
}

[Fact]
public async Task Minimized_window_is_not_learned()
{
    var fixture = EngineFixture.WithCurrentWindow(isMinimized: true);
    fixture.Engine.Start();
    fixture.Hook.Raise(42, WindowLifecycleEventKind.MoveSizeEnded);
    await fixture.DrainAsync();
    Assert.Empty(fixture.Engine.Catalog.Entries);
}

[Fact]
public async Task Minimize_ended_never_reapplies_a_remembered_placement()
{
    var fixture = EngineFixture.WithRememberedWindow(maximized: false);
    fixture.Engine.Start();
    fixture.Hook.Raise(42, WindowLifecycleEventKind.MinimizeEnded);
    await fixture.DrainAsync();
    Assert.Empty(fixture.Windows.Placements);
}

[Fact]
public async Task Manual_change_after_restore_becomes_the_next_remembered_normal_size_and_maximized_state()
{
    var fixture = EngineFixture.WithRememberedWindow(maximized: false);
    fixture.Engine.Start();
    fixture.Hook.Raise(42, WindowLifecycleEventKind.Shown);
    await fixture.DrainAsync();
    fixture.Advance(TimeSpan.FromMilliseconds(751));
    fixture.Windows.CurrentSnapshot = fixture.Windows.CurrentSnapshot with
    {
        NormalBounds = new PixelRect(300, 200, 1200, 800),
        IsMaximized = true
    };
    fixture.Hook.Raise(42, WindowLifecycleEventKind.MoveSizeEnded);
    await fixture.DrainAsync();
    var learned = Assert.Single(fixture.Engine.Catalog.Entries);
    Assert.Equal(new PixelRect(300, 200, 1200, 800), learned.NormalBounds);
    Assert.True(learned.IsMaximized);
}

[Fact]
public async Task Missing_fixed_zone_does_not_move_or_fallback_to_a_remembered_placement()
{
    var fixture = EngineFixture.WithMissingFixedZoneAndRememberedWindow();
    fixture.Engine.Start();
    fixture.Hook.Raise(42, WindowLifecycleEventKind.Shown);
    await fixture.DrainAsync();
    Assert.Empty(fixture.Windows.Placements);
    Assert.Contains(fixture.Log, message => message.Contains("Zielzone", StringComparison.Ordinal));
}
```

`EngineFixture` enthält Fake-Hook, Fake-Window-Service, Recording-Repository, Coordinator mit Null-Verzögerung, sofortige Delay-Funktion und Umgebung mit einem Profil, einem Monitor und zwei Zonen.

- [ ] **Step 2: Bestätige das Scheitern**

```powershell
dotnet test tests\SnapZones.Tests\SnapZones.Tests.csproj -c Release --filter FullyQualifiedName~WindowPlacementEngineTests
```

Expected: FAIL wegen fehlender Engine.

- [ ] **Step 3: Definiere Umgebung und Engine-Vertrag**

```csharp
public sealed record PlacementEnvironment(
    SnapConfiguration Configuration,
    IReadOnlyList<PlacementMonitorTarget> Monitors,
    IReadOnlyList<PlacementZoneTarget> Zones);
```

Der Engine-Konstruktor erhält Hook, Window-Service, Save-Coordinator, initialen Katalog, `Func<PlacementEnvironment>`, eigene Prozess-ID, `Action<string>` und optional `Func<TimeSpan, CancellationToken, Task>`.

```csharp
public WindowPlacementCatalog Catalog { get; }
public event Action<WindowPlacementCatalog>? CatalogChanged;
public void Start();
public void Stop();
public void EmergencyStop();
public void ReplaceCatalog(WindowPlacementCatalog catalog);
public Task ApplyNowAsync(WindowIdentity identity, CancellationToken cancellationToken);
public void Forget(WindowIdentity identity);
public void RememberExplicitZone(nint windowHandle, Guid profileId, string monitorStableId, Guid zoneId);
public Task FlushAsync(CancellationToken cancellationToken);
```

- [ ] **Step 4: Implementiere den definierten Ereignisfluss**

Nur `Shown` darf eine Wiederherstellung auslösen und erhält genau drei Inspektionsversuche nach `100 ms`, `300 ms`, `700 ms`; ein `HashSet<nint>` verhindert doppelte erfolgreiche Verarbeitung. `MinimizeEnded` darf höchstens eine verzögerte Erfassung des wieder sichtbaren Zustands anstossen, aber nie eine Regel oder Platzierung anwenden. Regelkonflikt protokolliert und tut nichts, `Exclude` platziert und lernt nicht, `FixedZone` sucht Profil/Monitor/Zone, sonst gewinnt der Katalogeintrag nach `WindowIdentity`.

`LocationChanged` ersetzt ein 400-ms-Capture-Delay pro Handle. `MoveSizeEnded`, `Hidden`, `Destroyed` erfassen sofort den letzten lesbaren oder gecachten Zustand. Eigene Bewegungen werden pro Handle 750 ms unterdrückt. Minimierte Snapshots werden ignoriert. Katalogwechsel ersetzen dieselbe Identität, sortieren nach Zeit, begrenzen auf 500, lösen `CatalogChanged` aus und rufen `RequestSave` auf. `Stop` löst Ereignisse und CancellationTokens, behält aber Daten. `ReplaceCatalog` ist nur im gestoppten Zustand zulässig, ersetzt den Startkatalog ohne ihn erneut zu speichern und löst `CatalogChanged` aus. `ApplyNowAsync` bewegt höchstens das erste aktuell passende Fenster.

- [ ] **Step 5: Führe Engine und Abhängigkeiten aus**

```powershell
dotnet test tests\SnapZones.Tests\SnapZones.Tests.csproj -c Release --filter "FullyQualifiedName~WindowPlacementEngineTests|FullyQualifiedName~PlacementRuleResolverTests|FullyQualifiedName~PlacementGeometryTests|FullyQualifiedName~WindowPlacementSaveCoordinatorTests"
```

Expected: PASS.

- [ ] **Step 6: Committe die Engine**

```powershell
git -c safe.directory='//192.168.1.32/temp/PortableApps/SaschaWindowZones' add -- src/SnapZones.App/Services/PlacementEnvironment.cs src/SnapZones.App/Services/IWindowPlacementEngine.cs src/SnapZones.App/Services/WindowPlacementEngine.cs tests/SnapZones.Tests/Services/WindowPlacementEngineTests.cs
git -c safe.directory='//192.168.1.32/temp/PortableApps/SaschaWindowZones' commit -m "feat: restore and learn window placements"
```

---

### Task 9: App-Start, Controller, manueller Snap und Not-Aus integrieren

**Files:**
- Modify: `src/SnapZones.App/App.xaml.cs:23-91`
- Modify: `src/SnapZones.App/Services/ApplicationController.cs:18-321`
- Create: `src/SnapZones.App/Services/ApplicationControllerDependencies.cs`
- Create: `src/SnapZones.App/Services/ApplicationDataPaths.cs`
- Modify: `src/SnapZones.Core/Drag/DragAction.cs`
- Modify: `src/SnapZones.Core/Drag/WindowDragCoordinator.cs:45-91`
- Modify: `tests/SnapZones.Tests/Drag/WindowDragCoordinatorTests.cs`
- Modify: `tests/SnapZones.Tests/Drag/WindowsSafetyBoundaryTests.cs`
- Create: `tests/SnapZones.Tests/Services/ApplicationDataPathsTests.cs`
- Create: `tests/SnapZones.Tests/Services/ApplicationControllerPlacementTests.cs`

**Interfaces:**
- Consumes: Repository, Engine und Windows-Dienste aus Tasks 3, 5–8.
- Produces: gestartete und sicher gestoppte Fensterautomatik sowie direkte Zonen-ID-Übernahme nach manuellem Snap.

- [ ] **Step 1: Erweiter den Drag-Test um Monitor- und Zonenidentität**

```csharp
var snap = Assert.IsType<SnapWindowAction>(actions.Last());
Assert.Equal(target.Monitor.Identity.StableId, snap.MonitorStableId);
Assert.Equal(target.Zones[0].Id, snap.ZoneId);
```

Ändere den Vertrag zu:

```csharp
public sealed record SnapWindowAction(
    nint WindowHandle, PixelRect Bounds,
    string MonitorStableId, Guid ZoneId) : DragAction;
```

- [ ] **Step 2: Führe den fokussierten Drag-Test aus und bestätige das Scheitern**

```powershell
dotnet test tests\SnapZones.Tests\SnapZones.Tests.csproj -c Release --filter FullyQualifiedName~WindowDragCoordinatorTests
```

Expected: FAIL, weil die Aktion Identitäten noch nicht enthält.

- [ ] **Step 3: Übergib Identitäten aus `WindowDragCoordinator.End`**

```csharp
ActionRequested?.Invoke(new SnapWindowAction(
    windowHandle, bounds,
    hoverTarget.Monitor.Identity.StableId,
    hoverZone.Id));
```

Aktualisiere alle bestehenden Konstruktorverwendungen und Assertions auf die neue Signatur.

- [ ] **Step 4: Schreib Controller-Tests für Reconfigure, Snap und Not-Aus**

`ApplicationControllerPlacementTests` verwendet injizierte Fakes über einen neuen `ApplicationControllerDependencies`-Record, sodass kein globaler Hook registriert wird. `ControllerFixture` stellt Controller, ViewModel, Recording-Engine, gespeicherte Konfiguration und eine Methode zum Auslösen einer `DragAction` bereit. Implementiere exakt diese Tests:

```csharp
[Fact]
public void Reconfigure_starts_placement_engine_when_automation_is_enabled()
{
    var fixture = ControllerFixture.Create(restoreWindowPlacementEnabled: true);
    fixture.Controller.Reconfigure();
    Assert.Equal(1, fixture.PlacementEngine.StartCalls);
}

[Fact]
public void Successful_manual_snap_records_profile_monitor_and_zone()
{
    var fixture = ControllerFixture.Create(restoreWindowPlacementEnabled: true);
    var zoneId = Guid.NewGuid();
    fixture.RaiseDragAction(new SnapWindowAction(42, new PixelRect(0, 0, 800, 600), "DISPLAY-1", zoneId));
    Assert.Equal((42, fixture.ActiveProfileId, "DISPLAY-1", zoneId), fixture.PlacementEngine.LastExplicitZone);
}

[Fact]
public void Emergency_stop_disables_snapping_and_window_placement_in_saved_configuration()
{
    var fixture = ControllerFixture.Create(restoreWindowPlacementEnabled: true, snappingEnabled: true);
    fixture.Controller.EmergencyStop("Test");
    Assert.False(fixture.ViewModel.Settings.RestoreWindowPlacementEnabled);
    Assert.False(fixture.ViewModel.Settings.SnappingEnabled);
    Assert.Equal(1, fixture.PlacementEngine.EmergencyStopCalls);
    Assert.False(fixture.SavedConfiguration.AppSettings.RestoreWindowPlacementEnabled);
    Assert.False(fixture.SavedConfiguration.AppSettings.SnappingEnabled);
}

[Fact]
public async Task Flush_flushes_configuration_and_placement_catalog()
{
    var fixture = ControllerFixture.Create(restoreWindowPlacementEnabled: true);
    await fixture.Controller.FlushAsync(CancellationToken.None);
    Assert.Equal(1, fixture.ConfigurationSaveCoordinator.FlushCalls);
    Assert.Equal(1, fixture.PlacementEngine.FlushCalls);
}
```

- [ ] **Step 5: Teste und implementiere gemeinsame normale und portable Datenpfade**

```csharp
[Fact]
public void Resolve_uses_Data_and_Logs_next_to_executable_when_portable_flag_exists()
{
    using var directory = new TemporaryDirectory();
    var executable = Path.Combine(directory.Path, "SnapZones.App.exe");
    File.WriteAllText(Path.Combine(directory.Path, "portable.flag"), string.Empty);
    var paths = ApplicationDataPaths.Resolve(executable, "R:\\Roaming", "L:\\Local");
    Assert.Equal(Path.Combine(directory.Path, "Data"), paths.ConfigurationDirectory);
    Assert.Equal(Path.Combine(directory.Path, "Logs"), paths.LogDirectory);
}
```

Ergänze einen zweiten Test für den normalen Modus: `ConfigurationDirectory` ist `%APPDATA%\SnapZones`, `LogDirectory` ist `%LOCALAPPDATA%\SnapZones\logs`. Implementiere:

```csharp
public sealed record ApplicationDataPaths(string ConfigurationDirectory, string LogDirectory)
{
    public static ApplicationDataPaths Resolve(
        string executablePath, string roamingRoot, string localRoot)
    {
        var executableDirectory = Path.GetDirectoryName(executablePath)
            ?? throw new ArgumentException("Ausführbarer Pfad hat kein Verzeichnis.", nameof(executablePath));
        return File.Exists(Path.Combine(executableDirectory, "portable.flag"))
            ? new(Path.Combine(executableDirectory, "Data"), Path.Combine(executableDirectory, "Logs"))
            : new(Path.Combine(roamingRoot, "SnapZones"), Path.Combine(localRoot, "SnapZones", "logs"));
    }
}
```

`settings.json` und `placements.json` verwenden beide `ConfigurationDirectory`; nur Logs verwenden `LogDirectory`.

- [ ] **Step 6: Lade `placements.json` nicht blockierend und injiziere die Engine**

In `App.OnStartup` wird direkt nach dem Auflösen der Datenpfade der Ladevorgang gestartet, aber noch nicht abgewartet:

```csharp
var placementRepository = new JsonWindowPlacementRepository(paths.ConfigurationDirectory);
var placementLoadTask = placementRepository.LoadAsync(CancellationToken.None);
```

Erzeuge `WindowLifecycleHook`, `WindowsPlacementWindowService`, `WindowPlacementSaveCoordinator` und `WindowPlacementEngine` in `ApplicationControllerDependencies.CreateDefault(...)` zunächst mit `WindowPlacementCatalog.Empty`. Setze `MainWindow` und zeige es ausser bei `--autostart`, bevor `placementLoadTask` abgewartet wird. Danach ruft `controller.InitializeWindowPlacements(placementLoad)` im noch gestoppten Zustand `ReplaceCatalog` auf und startet die Engine gemäss Hauptschalter. Bei `RecoveredFromError` wird die Meldung in Status und Log geschrieben; vor Abschluss des Ladens registriert die Engine keinen Hook und bewegt kein Fenster.

- [ ] **Step 7: Verbinde Lebenszyklus und Sicherheitszustand**

`ApplicationControllerDependencies` enthält sämtliche produktiven Hook-, Fenster- und Placement-Abhängigkeiten. `Reconfigure` startet die Engine erst nach `InitializeWindowPlacements` und nur bei `RestoreWindowPlacementEnabled`, sonst `Stop`. `EmergencyStop` schaltet in `MainViewModel` beide Automatikflags aus, ruft `placementEngine.EmergencyStop()` und speichert. Nach erfolgreichem `SnapWindowAction` ruft der Controller `RememberExplicitZone` mit aktivem Profil, Monitor und Zone. Die öffentliche `FlushAsync`-Methode wartet auf Konfigurations- und Placement-Flush; `ExitApplication` verwendet sie vor Shutdown, `Dispose` löst alle Engine-/Hook-Ereignisse.

- [ ] **Step 8: Führe Integrations- und Sicherheitsfokustests aus**

```powershell
dotnet test tests\SnapZones.Tests\SnapZones.Tests.csproj -c Release --filter "FullyQualifiedName~ApplicationControllerPlacementTests|FullyQualifiedName~ApplicationDataPathsTests|FullyQualifiedName~WindowDragCoordinatorTests|FullyQualifiedName~WindowsSafetyBoundaryTests"
```

Expected: PASS.

- [ ] **Step 9: Committe die App-Integration**

```powershell
git -c safe.directory='//192.168.1.32/temp/PortableApps/SaschaWindowZones' add -- src/SnapZones.App/App.xaml.cs src/SnapZones.App/Services/ApplicationController.cs src/SnapZones.App/Services/ApplicationControllerDependencies.cs src/SnapZones.App/Services/ApplicationDataPaths.cs src/SnapZones.Core/Drag/DragAction.cs src/SnapZones.Core/Drag/WindowDragCoordinator.cs tests/SnapZones.Tests/Drag/WindowDragCoordinatorTests.cs tests/SnapZones.Tests/Drag/WindowsSafetyBoundaryTests.cs tests/SnapZones.Tests/Services/ApplicationControllerPlacementTests.cs tests/SnapZones.Tests/Services/ApplicationDataPathsTests.cs
git -c safe.directory='//192.168.1.32/temp/PortableApps/SaschaWindowZones' commit -m "feat: integrate window placement lifecycle"
```

---

### Task 10: Fensterplatzierungs-ViewModel und Einfenster-UI

**Files:**
- Create: `src/SnapZones.App/ViewModels/WindowPlacementItemViewModel.cs`
- Create: `src/SnapZones.App/ViewModels/WindowPlacementViewModel.cs`
- Create: `src/SnapZones.Windows/Windows/WindowSelectionService.cs`
- Modify: `src/SnapZones.App/ViewModels/SettingsViewModel.cs:5-225`
- Modify: `src/SnapZones.App/ViewModels/MainViewModel.cs:8-235`
- Modify: `src/SnapZones.App/Views/MainWindow.xaml:50-380`
- Modify: `src/SnapZones.App/Views/MainWindow.xaml.cs:14-480`
- Modify: `src/SnapZones.App/Services/ApplicationController.cs`
- Test: `tests/SnapZones.Tests/ViewModels/SettingsViewModelTests.cs`
- Create: `tests/SnapZones.Tests/ViewModels/WindowPlacementViewModelTests.cs`
- Modify: `tests/SnapZones.Tests/Theme/ThemeResourceTests.cs`

**Interfaces:**
- Consumes: Engine-Katalog, Regeln, Profile, Monitore und Zonen.
- Produces: Seite **Fensterplatzierung** mit Hauptschalter, Auswahl, Anwenden, fester Zone, Ausschluss und Vergessen.

- [ ] **Step 1: Schreib ViewModel-Tests für Hauptschalter und Regelaktionen**

```csharp
[Fact]
public void Exclude_selected_creates_one_enabled_specific_exclusion_rule()
{
    var viewModel = CreateViewModel();
    IReadOnlyList<WindowPlacementRule>? changed = null;
    viewModel.RulesChanged += rules => changed = rules;
    viewModel.SelectedItem = viewModel.Items[0];
    viewModel.ExcludeSelected();
    var rule = Assert.Single(changed!);
    Assert.Equal(WindowPlacementMode.Exclude, rule.Action);
    Assert.Equal(viewModel.SelectedItem.Identity.ApplicationKey, rule.ApplicationKey);
    Assert.Equal(viewModel.SelectedItem.Identity.WindowClass, rule.WindowClass);
}

[Fact]
public void Forget_selected_raises_the_exact_window_identity()
{
    var viewModel = CreateViewModel();
    WindowIdentity? forgotten = null;
    viewModel.ForgetRequested += identity => forgotten = identity;
    viewModel.SelectedItem = viewModel.Items[0];
    viewModel.ForgetSelected();
    Assert.Equal(viewModel.SelectedItem.Identity, forgotten);
}
```

Erweitere `SettingsViewModelTests` um Roundtrip und Standardwert für `RestoreWindowPlacementEnabled` sowie Regeln.

- [ ] **Step 2: Bestätige das Scheitern**

```powershell
dotnet test tests\SnapZones.Tests\SnapZones.Tests.csproj -c Release --filter "FullyQualifiedName~WindowPlacementViewModelTests|FullyQualifiedName~SettingsViewModelTests"
```

Expected: FAIL wegen fehlenden ViewModels und Properties.

- [ ] **Step 3: Implementiere UI-ViewModels und MainViewModel-Verbindung**

`WindowPlacementItemViewModel` exponiert `Identity`, `DisplayName`, `WindowKindText`, `PlacementText`, `LastUpdatedText`, `RuleStatusText` und den zugrunde liegenden Eintrag.

`WindowPlacementViewModel` erhält Katalog, Regeln, Profile und Monitore:

```csharp
public ObservableCollection<WindowPlacementItemViewModel> Items { get; }
public WindowPlacementItemViewModel? SelectedItem { get; set; }
public LayoutProfile? SelectedTargetProfile { get; set; }
public MonitorChoice? SelectedTargetMonitor { get; set; }
public ZoneDefinition? SelectedTargetZone { get; set; }
public string TitlePattern { get; set; }
public event Action<IReadOnlyList<WindowPlacementRule>>? RulesChanged;
public event Action<WindowIdentity>? ForgetRequested;
public event Action<WindowIdentity>? ApplyNowRequested;
public event Action? SelectWindowRequested;
public void ExcludeSelected();
public void RememberSelected();
public void FixSelectedToZone();
public void ForgetSelected();
public void ApplySelectedNow();
public void ReplaceCatalog(WindowPlacementCatalog catalog);
```

`SettingsViewModel` erhält `RestoreWindowPlacementEnabled` und eine Regelkopie; `CreateSettings` schreibt, `Apply` liest beide. `MainViewModel` besitzt `WindowPlacement`, übernimmt `RulesChanged` in Settings, ruft `RequestPersistence` und schaltet in `DisableSnappingForSafety` beide Automatikflags aus.

- [ ] **Step 4: Implementiere die einmalige Fensterwahl**

`WindowSelectionService.SelectNextAsync(int ownProcessId, TimeSpan timeout, CancellationToken)` registriert temporär `EVENT_SYSTEM_FOREGROUND`, ignoriert den eigenen Prozess und vervollständigt ein `TaskCompletionSource<nint>` beim ersten durch `IPlacementWindowService.Inspect` bestätigten fremden Fenster. Timeout oder Abbruch liefert `0`; der Hook wird in `finally` immer gelöst.

Der Controller setzt `StatusMessage = "Zielfenster auswählen"`, wartet höchstens zehn Sekunden, ergänzt bei Erfolg den gelesenen Fenstertyp und aktiviert danach das Hauptfenster. Apply und Forget delegieren an die Engine.

- [ ] **Step 5: Ergänze die strukturierte Seite in `MainWindow.xaml`**

Füge zwischen **Layouts** und **Windows-Anzeige** ein `TabItem Header="Fensterplatzierung"` ein. Verwende bestehende Styles und dynamische Farben:

```text
Fensterplatzierung                              Automatik [Ein]
[Fenster auswählen]
┌ Gelernte Programme ─────────────────────────────────────────────┐
│ Programmnamen · Hauptfenster | Monitor · Zone · Zustand · Zeit │
└─────────────────────────────────────────────────────────────────┘
Ausgewähltes Fenster
[Jetzt anwenden] [Letzte Platzierung] [Nicht verwalten] [Vergessen]
Feste Zone: Profil [ ] Monitor [ ] Zone [ ] [Feste Zone speichern]
Erweitert: App-Schlüssel, Klasse, Art, optionales Titelmuster
```

Technische Werte sind auswähl- und kopierbar; Schaltflächen ohne Auswahl deaktiviert. Fehlende Zielzone und Regelkonflikt erscheinen als Text.

- [ ] **Step 6: Verbinde dünne UI-Handler**

Ergänze `PlacementSelect_Click`, `PlacementApply_Click`, `PlacementRemember_Click`, `PlacementExclude_Click`, `PlacementForget_Click`, `PlacementFix_Click`. Jeder Handler ruft nur die entsprechende ViewModel-Methode auf und schreibt Exceptions in `StatusMessage`; es entsteht kein weiteres Dialogfenster.

- [ ] **Step 7: Führe ViewModel-, XAML- und Theme-Regressionstests aus**

```powershell
dotnet test tests\SnapZones.Tests\SnapZones.Tests.csproj -c Release --filter "FullyQualifiedName~WindowPlacementViewModelTests|FullyQualifiedName~SettingsViewModelTests|FullyQualifiedName~MainViewModelPersistenceTests|FullyQualifiedName~ThemeResourceTests"
```

Expected: PASS; der XAML-Test bestätigt Tabtitel, Hauptschalter, Aktionen, dynamische Ressourcen und kopierbare technische Felder.

- [ ] **Step 8: Committe die Fensterplatzierungsseite**

```powershell
git -c safe.directory='//192.168.1.32/temp/PortableApps/SaschaWindowZones' add -- src/SnapZones.App/ViewModels/WindowPlacementItemViewModel.cs src/SnapZones.App/ViewModels/WindowPlacementViewModel.cs src/SnapZones.Windows/Windows/WindowSelectionService.cs src/SnapZones.App/ViewModels/SettingsViewModel.cs src/SnapZones.App/ViewModels/MainViewModel.cs src/SnapZones.App/Views/MainWindow.xaml src/SnapZones.App/Views/MainWindow.xaml.cs src/SnapZones.App/Services/ApplicationController.cs tests/SnapZones.Tests/ViewModels/SettingsViewModelTests.cs tests/SnapZones.Tests/ViewModels/WindowPlacementViewModelTests.cs tests/SnapZones.Tests/Theme/ThemeResourceTests.cs
git -c safe.directory='//192.168.1.32/temp/PortableApps/SaschaWindowZones' commit -m "feat: add window placement management UI"
```

---

### Task 11: Vollständige Verifikation und reale Abnahme

**Files:**
- Modify: `src/SnapZones.App/Services/DiagnosticRunner.cs`
- Create: `tests/SnapZones.Tests/Services/DiagnosticRunnerTests.cs`
- Modify: `docs/README.md`
- Modify: `outputs/SnapZones-Kurzanleitung.md`
- Modify: `outputs/SnapZones-Pruefbericht.md`
- Modify: `scripts/verify.ps1`

**Interfaces:**
- Consumes: vollständige Funktion aus Tasks 1–10.
- Produces: Diagnose, Bedienhinweise, Release-Nachweis und manuelle Abnahme.

- [ ] **Step 1: Ergänze Diagnose-Assertions vor der Implementierung**

Der sichere Diagnosevertrag erhält:

```json
{
  "windowPlacement": {
    "enabled": true,
    "learnedEntryCount": 0,
    "ruleCount": 0,
    "lifecycleHookRegistered": false
  }
}
```

`--diagnostics` liest nur Dateien, verändert nichts und registriert keinen Lebenszyklus-Hook. Ergänze diesen Test; `RunForTestAsync` gibt dasselbe serialisierbare Ergebnisobjekt zurück, das `RunAsync` als JSON ausgibt:

```csharp
[Fact]
public async Task Diagnostics_reports_window_placement_without_registering_a_hook()
{
    using var directory = new TemporaryDirectory();
    var result = await DiagnosticRunner.RunForTestAsync(directory.Path, CancellationToken.None);
    Assert.True(result.WindowPlacement.Enabled);
    Assert.Equal(0, result.WindowPlacement.LearnedEntryCount);
    Assert.Equal(0, result.WindowPlacement.RuleCount);
    Assert.False(result.WindowPlacement.LifecycleHookRegistered);
}
```

- [ ] **Step 2: Führe Test und Build vor der Dokumentation aus**

```powershell
dotnet restore SnapZones.sln
dotnet test SnapZones.sln -c Release --no-restore
dotnet build SnapZones.sln -c Release --no-restore
```

Expected: alle Tests PASS, Build mit 0 Fehlern und 0 Warnungen.

- [ ] **Step 3: Aktualisiere Dokumentation mit tatsächlich vorhandenem Verhalten**

Dokumentiere Hauptschalter, globalen Standard, Maximierung, ausgeschlossene Minimierung, gemeinsame Fenstertypen, feste Zone, Ausschluss, `placements.json`, Not-Aus und Berechtigungsgrenze. Im Prüfbericht nur tatsächlich ausgeführte Tests und reale Resultate nennen.

- [ ] **Step 4: Führe Publish und sichere Diagnose aus**

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\verify.ps1
```

Expected: `VERIFY_OK`, alle Tests bestanden, `hookRegistered=false`, `settingsChanged=false`, `windowPlacement.lifecycleHookRegistered=false`, Root-EXE und Publish-EXE hashgleich.

- [ ] **Step 5: Führe reale Abnahme mit kontrolliert geöffneten Fenstern aus**

1. Windows-Einstellungen normal vergrössern, schliessen und erneut öffnen; Position und Grösse kehren einmalig zurück.
2. Windows-Einstellungen maximiert schliessen und erneut öffnen; sie erscheinen maximiert.
3. Zwei Excel-Hauptfenster teilen den Typ; ein Excel-Dialog erhält einen getrennten Eintrag.
4. Explorer und Notepad frei positionieren, schliessen und erneut öffnen.
5. Feste Zone anwenden, danach manuell verschieben; erst nächstes Öffnen setzt wieder die feste Startzone.
6. Ausschlussregel setzen; das Fenster wird weder bewegt noch weiter gelernt.
7. Zielmonitor trennen; das Fenster erscheint vollständig sichtbar auf einem vorhandenen Monitor.
8. Not-Aus während anstehender Wiederherstellung; beide Automatikflags sind ausgeschaltet und gespeichert.
9. Programm neu starten; `settings.json` und `placements.json` laden ohne Wiederherstellungsmeldung.

- [ ] **Step 6: Trage reale Ergebnisse ein und führe den finalen Regressionstest aus**

```powershell
dotnet test SnapZones.sln -c Release --no-restore
git -c safe.directory='//192.168.1.32/temp/PortableApps/SaschaWindowZones' diff --check
```

Expected: alle Tests PASS und keine Whitespace-Fehler.

- [ ] **Step 7: Committe Diagnose und Dokumentation**

```powershell
git -c safe.directory='//192.168.1.32/temp/PortableApps/SaschaWindowZones' add -- src/SnapZones.App/Services/DiagnosticRunner.cs tests/SnapZones.Tests/Services/DiagnosticRunnerTests.cs docs/README.md outputs/SnapZones-Kurzanleitung.md outputs/SnapZones-Pruefbericht.md scripts/verify.ps1
git -c safe.directory='//192.168.1.32/temp/PortableApps/SaschaWindowZones' commit -m "docs: verify window placement engine"
```
