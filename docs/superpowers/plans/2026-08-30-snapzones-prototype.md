# SnapZones Prototype Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a runnable Windows 11 x64 prototype that edits per-monitor snap zones, shows immediate drag overlays, snaps dropped windows, switches profiles, persists settings, and supports tray/autostart operation.

**Architecture:** A dependency-free core owns models, validation, geometry, editor state, monitor matching, and profile state. A Windows library isolates Win32 display, window, hook, hotkey, and autostart calls; a WPF app composes those services and renders the editor and click-through overlays. Native boundaries are thin so deterministic behaviour is tested in `SnapZones.Tests`, while Windows integration is covered by a diagnostic smoke command and manual target-system checks.

**Tech Stack:** C# 12, .NET 8, WPF, Win32 P/Invoke, System.Text.Json, xUnit

**Spec:** `docs/superpowers/specs/2026-08-30-snapzones-design.md`

## Global Constraints

- Target Windows 11 x64 only; publish `win-x64` as a self-contained single-folder application.
- Keep runtime dependencies inside the .NET Windows Desktop shared framework; do not add an MVVM or tray package.
- Use neutral German UI copy and German code comments; identifiers remain idiomatic English C#.
- Store configuration under `%APPDATA%\\SnapZones` and logs under `%LOCALAPPDATA%\\SnapZones`.
- Request no administrator rights, inject no code into other processes, and ignore higher-integrity windows that cannot be repositioned.
- Start with snapping and autostart disabled; no hook may be registered before an explicit UI action.
- Register `Ctrl+Alt+Shift+F12` as an emergency stop only for an active snap session; callback exceptions or more than 100 hook events in ten seconds trigger the same shutdown path.
- Use per-monitor DPI v2 awareness and physical-pixel rectangles at every Win32 boundary.
- Every behavioural production method starts with a failing test; declarative XAML and direct P/Invoke declarations are verified by build and integration checks.

---

## File map

```text
SnapZones.sln                              solution
global.json                                pins .NET 8 feature band
.gitignore                                 build and local-state exclusions
src/SnapZones.Core/
  SnapZones.Core.csproj                    platform-neutral library
  Models/*.cs                              immutable configuration/runtime records
  Geometry/ZoneGeometry.cs                 validation, hit-testing, pixel conversion
  Profiles/ProfileService.cs               profile selection and hotkey slots
  Persistence/*.cs                         versioned JSON and atomic replacement
  Monitors/MonitorMatcher.cs               saved-to-live monitor assignment
  Editor/LayoutEditorSession.cs            draft editing and save eligibility
  Drag/WindowDragCoordinator.cs            pure drag state machine
src/SnapZones.Windows/
  SnapZones.Windows.csproj                 Windows-only integration library
  Native/*.cs                              focused P/Invoke declarations and structs
  Displays/WindowsMonitorService.cs         live monitor enumeration and identity
  Windows/WindowsWindowService.cs           target-window inspection/positioning
  Hooks/WindowMoveHook.cs                   system move-size event subscription
  Hotkeys/GlobalHotkeyService.cs            Ctrl+Alt+digit registration
  Startup/WindowsStartupService.cs          HKCU autostart state
src/SnapZones.App/
  SnapZones.App.csproj                     WPF executable
  App.xaml(.cs)                             composition, single instance, shutdown
  app.manifest                              per-monitor DPI and Windows 11 support
  Services/*.cs                             config lifecycle, logs, tray orchestration
  ViewModels/*.cs                           application/editor/settings presentation
  Views/MainWindow.xaml(.cs)                navigation and editor host
  Controls/LayoutCanvas.cs                  zone rendering, selection, drag, resize
  Overlays/MonitorOverlayWindow.xaml(.cs)   per-monitor click-through overlay
  Overlays/OverlayManager.cs                overlay lifetime and state projection
tests/SnapZones.Tests/
  SnapZones.Tests.csproj                    xUnit test project
  Geometry/*.cs                             geometry and validation tests
  Persistence/*.cs                          JSON and recovery tests
  Monitors/*.cs                             monitor matching tests
  Profiles/*.cs                             active profile and slot tests
  Editor/*.cs                               draft edit tests
  Drag/*.cs                                 drag state-machine tests
docs/README.md                              German usage and limitations
scripts/verify.ps1                          restore, test, build, publish, artefact check
```

---

### Task 1: Solution foundation and configuration models

**Files:**
- Create: `global.json`, `.gitignore`, `SnapZones.sln`
- Create: `src/SnapZones.Core/SnapZones.Core.csproj`
- Create: `src/SnapZones.Core/Models/{NormalizedRect,ZoneDefinition,MonitorIdentity,MonitorLayout,LayoutProfile,LayoutMetrics,AppSettings,SnapConfiguration}.cs`
- Create: `tests/SnapZones.Tests/SnapZones.Tests.csproj`
- Test: `tests/SnapZones.Tests/Models/ConfigurationDefaultsTests.cs`

**Interfaces:**
- Produces: `NormalizedRect(double X, double Y, double Width, double Height)`, `ZoneDefinition(Guid Id, string Name, NormalizedRect Bounds)`, `MonitorIdentity(string StableId, string DeviceName, string FriendlyName)`, `MonitorLayout(MonitorIdentity Monitor, int SavedWidth, int SavedHeight, IReadOnlyList<ZoneDefinition> Zones)`, `LayoutProfile(Guid Id, string Name, int? QuickSlot, IReadOnlyList<MonitorLayout> Monitors)`, `LayoutMetrics(int OuterMargin, int ZoneGap)`, and `SnapConfiguration.CreateDefault()`.

- [ ] **Step 1: Write the failing defaults test**

```csharp
[Fact]
public void CreateDefault_builds_one_active_profile_with_full_monitor_fallback()
{
    var configuration = SnapConfiguration.CreateDefault();
    Assert.Single(configuration.Profiles);
    Assert.Equal(configuration.Profiles[0].Id, configuration.Settings.ActiveProfileId);
    Assert.Equal("Standard", configuration.Profiles[0].Name);
    Assert.False(configuration.Settings.SnappingEnabled);
    Assert.False(configuration.Settings.StartWithWindows);
    Assert.Equal(0.0, NormalizedRect.Full.X);
    Assert.Equal(1.0, NormalizedRect.Full.Width);
}
```

- [ ] **Step 2: Create the solution/projects and verify RED**

Run: `dotnet test tests/SnapZones.Tests/SnapZones.Tests.csproj --filter CreateDefault_builds_one_active_profile_with_full_monitor_fallback`
Expected: FAIL because `SnapConfiguration` and `NormalizedRect` do not exist.

- [ ] **Step 3: Implement the records and default factory**

```csharp
public sealed record SnapConfiguration(int SchemaVersion, AppSettings Settings, IReadOnlyList<LayoutProfile> Profiles)
{
    public const int CurrentSchemaVersion = 1;
    public static SnapConfiguration CreateDefault()
    {
        var id = Guid.NewGuid();
        return new(CurrentSchemaVersion, AppSettings.Default(id),
            [new LayoutProfile(id, "Standard", 1, [])]);
    }
}
```

- [ ] **Step 4: Verify GREEN and the full solution build**

Run: `dotnet test SnapZones.sln && dotnet build SnapZones.sln -c Release --no-restore`
Expected: PASS with zero warnings and zero errors.

- [ ] **Step 5: Commit**

```powershell
git add -- .gitignore global.json SnapZones.sln src/SnapZones.Core tests/SnapZones.Tests
git commit -m "build: create SnapZones solution and models"
```

---

### Task 2: Zone geometry, validation, and editor draft state

**Files:**
- Create: `src/SnapZones.Core/Geometry/{PixelRect,MonitorWorkArea,ZoneGeometry,ZoneValidationResult}.cs`
- Create: `src/SnapZones.Core/Editor/LayoutEditorSession.cs`
- Test: `tests/SnapZones.Tests/Geometry/ZoneGeometryTests.cs`
- Test: `tests/SnapZones.Tests/Editor/LayoutEditorSessionTests.cs`

**Interfaces:**
- Consumes: `ZoneDefinition`, `NormalizedRect`, `LayoutMetrics`, `MonitorLayout`.
- Produces: `ZoneGeometry.ToPixels(...)`, `ZoneGeometry.HitTest(...)`, `ZoneGeometry.Validate(...)`, and `LayoutEditorSession` methods `AddZone`, `MoveZone`, `ResizeZone`, `DeleteZone`, `Reset`, `CreateSnapshot`.

- [ ] **Step 1: Write failing geometry tests**

```csharp
[Theory]
[InlineData(-1920, 0, 1920, 1080, 8, 8, -1912, 8, 948, 1064)]
[InlineData(0, 0, 3440, 1400, 10, 12, 10, 10, 1704, 1380)]
public void ToPixels_applies_margin_gap_and_negative_origins(
    int x, int y, int w, int h, int margin, int gap,
    int expectedX, int expectedY, int expectedW, int expectedH)
{
    var area = new MonitorWorkArea(x, y, w, h);
    var zone = new NormalizedRect(0, 0, .5, 1);
    Assert.Equal(new PixelRect(expectedX, expectedY, expectedW, expectedH),
        ZoneGeometry.ToPixels(zone, area, new LayoutMetrics(margin, gap)));
}

[Fact]
public void Validate_rejects_overlapping_zones()
{
    var zones = new[] { Zone("A", 0, 0, .6, 1), Zone("B", .5, 0, .5, 1) };
    Assert.Contains(ZoneGeometry.Validate(zones).Errors, error => error.Code == "overlap");
}
```

- [ ] **Step 2: Run the geometry tests and verify RED**

Run: `dotnet test tests/SnapZones.Tests/SnapZones.Tests.csproj --filter "FullyQualifiedName~ZoneGeometryTests"`
Expected: FAIL because `ZoneGeometry` does not exist.

- [ ] **Step 3: Implement deterministic conversion, hit-testing, and validation**

```csharp
public static PixelRect ToPixels(NormalizedRect zone, MonitorWorkArea area, LayoutMetrics metrics)
{
    var innerWidth = Math.Max(1, area.Width - (2 * metrics.OuterMargin));
    var innerHeight = Math.Max(1, area.Height - (2 * metrics.OuterMargin));
    var left = area.X + metrics.OuterMargin + (int)Math.Round(zone.X * innerWidth)
        + (zone.X > 0 ? metrics.ZoneGap / 2 : 0);
    var top = area.Y + metrics.OuterMargin + (int)Math.Round(zone.Y * innerHeight)
        + (zone.Y > 0 ? metrics.ZoneGap / 2 : 0);
    var right = area.X + metrics.OuterMargin + (int)Math.Round((zone.X + zone.Width) * innerWidth)
        - (zone.X + zone.Width < 1 ? metrics.ZoneGap - metrics.ZoneGap / 2 : 0);
    var bottom = area.Y + metrics.OuterMargin + (int)Math.Round((zone.Y + zone.Height) * innerHeight)
        - (zone.Y + zone.Height < 1 ? metrics.ZoneGap - metrics.ZoneGap / 2 : 0);
    return new PixelRect(left, top, Math.Max(1, right - left), Math.Max(1, bottom - top));
}
```

- [ ] **Step 4: Write and run failing editor tests**

```csharp
[Fact]
public void Reset_restores_the_saved_layout_after_multiple_draft_edits()
{
    var session = new LayoutEditorSession(SavedMonitorLayout());
    var added = session.AddZone("Neu", new NormalizedRect(.5, 0, .5, 1));
    session.MoveZone(added.Id, new NormalizedRect(.4, 0, .6, 1));
    session.Reset();
    Assert.Equal(SavedMonitorLayout().Zones, session.Zones);
    Assert.False(session.IsDirty);
}
```

Run: `dotnet test tests/SnapZones.Tests/SnapZones.Tests.csproj --filter "FullyQualifiedName~LayoutEditorSessionTests"`
Expected: FAIL because `LayoutEditorSession` does not exist.

- [ ] **Step 5: Implement `LayoutEditorSession`, verify all tests, commit**

Run: `dotnet test SnapZones.sln`
Expected: PASS with zone boundary, overlap, edge hit, add/move/resize/delete/reset cases covered.

```powershell
git add -- src/SnapZones.Core/Geometry src/SnapZones.Core/Editor tests/SnapZones.Tests/Geometry tests/SnapZones.Tests/Editor
git commit -m "feat: add zone geometry and editor state"
```

---

### Task 3: Versioned persistence and profile switching

**Files:**
- Create: `src/SnapZones.Core/Persistence/{IConfigurationRepository,ConfigurationLoadResult,JsonConfigurationRepository}.cs`
- Create: `src/SnapZones.Core/Profiles/ProfileService.cs`
- Test: `tests/SnapZones.Tests/Persistence/JsonConfigurationRepositoryTests.cs`
- Test: `tests/SnapZones.Tests/Profiles/ProfileServiceTests.cs`

**Interfaces:**
- Produces: `Task<ConfigurationLoadResult> LoadAsync(CancellationToken)`, `Task SaveAsync(SnapConfiguration, CancellationToken)`, `LayoutProfile Activate(Guid)`, and `LayoutProfile ActivateQuickSlot(int)`.

- [ ] **Step 1: Write persistence RED tests using a real temporary directory**

```csharp
[Fact]
public async Task Save_then_load_preserves_profiles_and_leaves_no_temp_file()
{
    using var directory = new TemporaryDirectory();
    var repository = new JsonConfigurationRepository(directory.Path);
    var expected = ConfigurationSamples.TwoProfiles();
    await repository.SaveAsync(expected, CancellationToken.None);
    var actual = await repository.LoadAsync(CancellationToken.None);
    Assert.Equal(expected, actual.Configuration);
    Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
}

[Fact]
public async Task Load_backs_up_invalid_json_and_returns_defaults()
{
    using var directory = new TemporaryDirectory();
    await File.WriteAllTextAsync(Path.Combine(directory.Path, "settings.json"), "{");
    var result = await new JsonConfigurationRepository(directory.Path).LoadAsync(CancellationToken.None);
    Assert.True(result.RecoveredFromError);
    Assert.Single(Directory.GetFiles(directory.Path, "settings.invalid-*.json"));
}
```

- [ ] **Step 2: Run tests, verify RED, then implement atomic JSON operations**

Run: `dotnet test tests/SnapZones.Tests/SnapZones.Tests.csproj --filter "FullyQualifiedName~JsonConfigurationRepositoryTests"`
Expected RED: missing repository types.

Implementation requirement: write `settings.<guid>.tmp`, flush and close it, then call `File.Move(temp, settings, true)`; on invalid input move the original to `settings.invalid-<UTC timestamp>.json` and return `CreateDefault()` with an error message.

- [ ] **Step 3: Write profile RED tests**

```csharp
[Fact]
public void ActivateQuickSlot_changes_active_profile_and_rejects_duplicate_slots()
{
    var service = new ProfileService(ConfigurationSamples.TwoProfiles());
    var evening = service.ActivateQuickSlot(2);
    Assert.Equal("Abend", evening.Name);
    Assert.Equal(evening.Id, service.Configuration.Settings.ActiveProfileId);
    Assert.Throws<InvalidOperationException>(() => service.AssignQuickSlot(service.Configuration.Profiles[0].Id, 2));
}
```

- [ ] **Step 4: Implement profile service, verify GREEN, commit**

Run: `dotnet test SnapZones.sln`
Expected: PASS including missing profile, slot range 1-9, duplicate slot, and active-profile fallback.

```powershell
git add -- src/SnapZones.Core/Persistence src/SnapZones.Core/Profiles tests/SnapZones.Tests/Persistence tests/SnapZones.Tests/Profiles
git commit -m "feat: persist configuration and switch profiles"
```

---

### Task 4: Monitor discovery and stable layout matching

**Files:**
- Create: `src/SnapZones.Core/Monitors/{LiveMonitor,MonitorMatch,MonitorMatcher}.cs`
- Create: `src/SnapZones.Windows/SnapZones.Windows.csproj`
- Create: `src/SnapZones.Windows/Native/{User32,DisplayConfigNative,NativeTypes}.cs`
- Create: `src/SnapZones.Windows/Displays/{IMonitorService,WindowsMonitorService}.cs`
- Test: `tests/SnapZones.Tests/Monitors/MonitorMatcherTests.cs`
- Test: `tests/SnapZones.Tests/Monitors/WindowsMonitorServiceIntegrationTests.cs`

**Interfaces:**
- Produces: `IReadOnlyList<MonitorMatch> MonitorMatcher.Match(saved, live)` and `IReadOnlyList<LiveMonitor> IMonitorService.GetMonitors()`.

- [ ] **Step 1: Write matching RED tests**

```csharp
[Fact]
public void Match_prefers_stable_id_then_device_name_then_primary_fallback()
{
    var saved = MonitorSamples.SavedPair();
    var live = MonitorSamples.LivePairWithRenamedDisplayNumbers();
    var matches = MonitorMatcher.Match(saved, live);
    Assert.All(matches, match => Assert.Equal(MonitorMatchQuality.StableId, match.Quality));
}

[Fact]
public void Match_never_assigns_one_live_monitor_twice()
{
    var matches = MonitorMatcher.Match(MonitorSamples.AmbiguousSavedPair(), MonitorSamples.SingleLive());
    Assert.Single(matches.Where(match => match.Live is not null));
}
```

- [ ] **Step 2: Verify RED, implement deterministic one-to-one matching, verify GREEN**

Run: `dotnet test tests/SnapZones.Tests/SnapZones.Tests.csproj --filter "FullyQualifiedName~MonitorMatcherTests"`
Expected RED then GREEN after stable ID, device name, resolution/position and primary fallback stages.

- [ ] **Step 3: Add focused Win32 declarations and monitor service**

`WindowsMonitorService.GetMonitors()` must call `EnumDisplayMonitors` and `GetMonitorInfo`, then enrich `MONITORINFOEX.szDevice` with `QueryDisplayConfig`/`DisplayConfigGetDeviceInfo` target device path and friendly name. If enrichment fails, emit a `LiveMonitor` with GDI device name as stable ID and never abort enumeration.

- [ ] **Step 4: Build x64 and run live monitor enumeration**

Add a Windows-only integration test that calls `GetMonitors()`, requires at least one result, verifies unique non-empty stable IDs, positive work-area sizes and DPI values of at least 96. Run: `dotnet test tests/SnapZones.Tests/SnapZones.Tests.csproj --filter "FullyQualifiedName~WindowsMonitorServiceIntegrationTests" && dotnet build SnapZones.sln -c Release -p:Platform=x64`.
Expected: one or more live monitors and zero P/Invoke marshalling/build warnings.

- [ ] **Step 5: Commit**

```powershell
git add -- src/SnapZones.Core/Monitors src/SnapZones.Windows tests/SnapZones.Tests/Monitors SnapZones.sln
git commit -m "feat: discover and match Windows monitors"
```

---

### Task 5: Window drag state machine, hooks, snapping, and overlays

**Files:**
- Create: `src/SnapZones.Core/Drag/{DragAction,DragState,WindowSnapshot,WindowCandidateEvaluator,WindowDragCoordinator}.cs`
- Create: `src/SnapZones.Windows/Windows/{IWindowService,WindowsWindowService}.cs`
- Create: `src/SnapZones.Windows/Hooks/{IWindowMoveHook,WindowMoveHook}.cs`
- Create: `src/SnapZones.App/SnapZones.App.csproj`, `src/SnapZones.App/app.manifest`
- Create: `src/SnapZones.App/Overlays/{MonitorOverlayWindow.xaml,MonitorOverlayWindow.xaml.cs,OverlayManager.cs}`
- Test: `tests/SnapZones.Tests/Drag/{WindowCandidateEvaluatorTests,WindowDragCoordinatorTests}.cs`

**Interfaces:**
- Produces: `WindowDragCoordinator.Start(nint, WindowSnapshot, PointInt)`, `Update(PointInt)`, `Cancel()`, `End()`, event `Action<DragAction> ActionRequested`; `IWindowMoveHook.MoveStarted/MoveEnded`; `IWindowService.TrySnap(nint, PixelRect)`; `HookCircuitBreaker.RecordEvent(DateTimeOffset)` and `Trip(Exception?)`.

- [ ] **Step 1: Write candidate and state-machine RED tests**

```csharp
[Fact]
public void Start_requests_overlay_for_titlebar_drag_only()
{
    var coordinator = DragSamples.CreateCoordinator();
    var actions = new List<DragAction>();
    coordinator.ActionRequested += actions.Add;
    coordinator.Start((nint)42, WindowSamples.NormalTitlebarWindow(), new PointInt(110, 20));
    Assert.Contains(actions, action => action is ShowOverlaysAction);
}

[Fact]
public void Escape_then_end_never_requests_snap()
{
    var coordinator = DragSamples.ActiveCoordinatorOverZone();
    var actions = new List<DragAction>();
    coordinator.ActionRequested += actions.Add;
    coordinator.Cancel();
    coordinator.End();
    Assert.DoesNotContain(actions, action => action is SnapWindowAction);
    Assert.Contains(actions, action => action is HideOverlaysAction);
}
```

- [ ] **Step 2: Verify RED, implement the pure state machine, verify GREEN**

Run: `dotnet test tests/SnapZones.Tests/SnapZones.Tests.csproj --filter "FullyQualifiedName~Drag"`
Expected: RED for missing coordinator, then PASS for start/update/monitor-crossing/highlight/cancel/end cases.

- [ ] **Step 3: Implement Win32 window boundary and hook**

Write failing tests proving `HookCircuitBreaker` trips on a callback exception and the 101st event inside ten seconds, but not on 100 events or events outside the window. `WindowMoveHook` registers only when `Enable()` is explicitly called, handles `EVENT_SYSTEM_MOVESIZESTART` and `EVENT_SYSTEM_MOVESIZEEND` using `WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS`, roots child handles with `GetAncestor`, and posts callbacks through the supplied `SynchronizationContext`. Every callback catches exceptions and routes them to the circuit breaker; a trip disposes the hook and requests `HideOverlaysAction`. `WindowsWindowService` uses bounded `SendMessageTimeout(WM_NCHITTEST)`, `DwmGetWindowAttribute`, styles, cloaking state, `ShowWindow(SW_RESTORE)`, and `SetWindowPos`; every public operation rechecks `IsWindow`.

- [ ] **Step 4: Implement reusable click-through per-monitor overlays**

Set WPF ownership to none and native extended styles `WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW`; render zones in physical-monitor coordinates converted through each overlay's DPI transform. `OverlayManager.Show`, `Highlight`, and `HideAll` reuse windows and never activate them.

- [ ] **Step 5: Build, run focused tests, commit**

Run: `dotnet test SnapZones.sln && dotnet build SnapZones.sln -c Release -p:Platform=x64`
Expected: all tests pass; app and Windows projects compile without warnings.

```powershell
git add -- src/SnapZones.Core/Drag src/SnapZones.Windows src/SnapZones.App tests/SnapZones.Tests/Drag SnapZones.sln
git commit -m "feat: snap dragged windows through monitor overlays"
```

---

### Task 6: Layout editor and profile/settings UI

**Files:**
- Create: `src/SnapZones.App/{App.xaml,App.xaml.cs}`
- Create: `src/SnapZones.App/ViewModels/{ViewModelBase,RelayCommand,MainViewModel,LayoutEditorViewModel,SettingsViewModel}.cs`
- Create: `src/SnapZones.App/Views/{MainWindow.xaml,MainWindow.xaml.cs}`
- Create: `src/SnapZones.App/Controls/{LayoutCanvas,ZoneThumb}.cs`
- Create: `src/SnapZones.App/Themes/Theme.xaml`
- Test: `tests/SnapZones.Tests/Editor/LayoutTemplatesTests.cs`

**Interfaces:**
- Consumes: editor session, monitor matches, profile service and repository.
- Produces: editor operations for select/add/delete/move/eight-direction resize/apply template/save/reset; profile add/rename/delete/activate; settings binding for overlay scope, trigger mode, margin, gap, colour and opacity.

- [ ] **Step 1: Write template RED tests**

```csharp
[Theory]
[InlineData(LayoutTemplate.TwoColumns, 2)]
[InlineData(LayoutTemplate.ThreeColumns, 3)]
[InlineData(LayoutTemplate.MainAndSide, 2)]
[InlineData(LayoutTemplate.Grid2x2, 4)]
public void CreateTemplate_returns_non_overlapping_full_bounds(LayoutTemplate template, int count)
{
    var zones = LayoutTemplates.Create(template);
    Assert.Equal(count, zones.Count);
    Assert.True(ZoneGeometry.Validate(zones).IsValid);
    Assert.Equal(1.0, zones.Sum(zone => zone.Bounds.Width * zone.Bounds.Height), 6);
}
```

- [ ] **Step 2: Verify RED, implement templates in core, verify GREEN**

Run: `dotnet test tests/SnapZones.Tests/SnapZones.Tests.csproj --filter "FullyQualifiedName~LayoutTemplatesTests"`
Expected: RED then PASS for all four templates.

- [ ] **Step 3: Implement view models and editor interaction**

The canvas maps monitor aspect ratio into its available bounds. Pointer down stores zone ID, drag origin and original normalized bounds; pointer move clamps the translated/resized rectangle to `[0,1]`; pointer up commits one editor operation. Invalid overlap is rendered red and disables «Speichern». Numeric fields edit the same draft object and use invariant internal values with current-culture display.

- [ ] **Step 4: Implement the intentional Windows-native visual system**

Use `Segoe UI Variable Display` for headings, `Segoe UI Variable Text` for body and `Cascadia Mono` only for coordinates. Derive system light/dark background, surface, border, text and muted text brushes; use `#2F6FED` only for selection and snap targets. Keep one signature element: the accurately proportioned monitor canvas with a subtle physical bezel and zone handles; use 4 px control radius and no decorative gradients.

- [ ] **Step 5: Keyboard/accessibility pass, build, commit**

Add visible focus styles, tab order, access keys, `AutomationProperties.Name`, minimum 44 px primary targets, and reduced-motion behaviour. Run: `dotnet build SnapZones.sln -c Release -p:Platform=x64 && dotnet test SnapZones.sln`.

```powershell
git add -- src/SnapZones.App src/SnapZones.Core/Editor tests/SnapZones.Tests/Editor
git commit -m "feat: add layout editor and settings interface"
```

---

### Task 7: Tray, hotkeys, autostart, lifecycle, and diagnostics

**Files:**
- Create: `src/SnapZones.Windows/Hotkeys/{IGlobalHotkeyService,GlobalHotkeyService}.cs`
- Create: `src/SnapZones.Windows/Startup/{IStartupService,WindowsStartupService}.cs`
- Create: `src/SnapZones.App/Services/{ApplicationController,TrayIconService,FileLog,SingleInstanceService,DiagnosticRunner}.cs`
- Create: `src/SnapZones.App/Services/{ProfileChangedToast.xaml,ProfileChangedToast.xaml.cs}`
- Modify: `src/SnapZones.App/App.xaml.cs`
- Test: `tests/SnapZones.Tests/Profiles/QuickSlotRegistrationPlanTests.cs`

**Interfaces:**
- Produces: hotkey registration result per slot, startup `bool IsEnabled`/`SetEnabled(bool)`, single-instance activation message, and `--diagnostics` JSON output.

- [ ] **Step 1: Write failing hotkey registration-plan tests**

```csharp
[Fact]
public void Build_registers_only_unique_slots_and_reports_conflicts()
{
    var result = QuickSlotRegistrationPlan.Build(ConfigurationSamples.WithDuplicateQuickSlot());
    Assert.Single(result.Registrations);
    Assert.Single(result.Errors);
    Assert.Equal(2, result.Errors[0].Slot);
}
```

- [ ] **Step 2: Verify RED, implement plan, verify GREEN**

Run: `dotnet test tests/SnapZones.Tests/SnapZones.Tests.csproj --filter "FullyQualifiedName~QuickSlotRegistrationPlanTests"`
Expected: RED then PASS for empty, valid, duplicate and out-of-range slots.

- [ ] **Step 3: Implement native services and application lifecycle**

Create one hidden `HwndSource` for `RegisterHotKey` messages; register `MOD_CONTROL | MOD_ALT` plus digits 1-9 and return per-slot conflicts without failing startup. While snapping is active, also register `MOD_CONTROL | MOD_ALT | MOD_SHIFT` plus `F12`; it immediately hides overlays, disposes the hook, persists `SnappingEnabled = false`, and never restarts automatically. Autostart writes/removes only value `SnapZones` in HKCU Run and quotes the executable path, but is invoked only from the explicit UI toggle. A named mutex plus registered window message causes a second process to show the existing main window. Shutdown always disposes tray, hooks, overlays, hotkeys and mutex.

- [ ] **Step 4: Compose tray and drag workflow**

The tray menu contains current profile, checked profile list, active toggle, editor, autostart and exit. `ApplicationController` owns the active configuration; profile changes rebuild overlay definitions and hotkey labels, show a neutral two-second `ProfileChangedToast`, then persist asynchronously. Hook callbacks enter the coordinator; coordinator actions call overlay or window services; no native callback performs disk or UI work directly.

- [ ] **Step 5: Add diagnostics, verify, commit**

`SnapZones.exe --diagnostics` prints schema version, configuration load status, monitor stable IDs/work areas/DPI, WinEvent API availability, overlay style definition, hotkey conflicts and autostart read state as JSON, then exits without registering a hook or changing settings.

Run: `dotnet test SnapZones.sln && dotnet run --project src/SnapZones.App -c Release -- --diagnostics`.
Expected: tests pass and diagnostics exit 0 with at least one monitor, `hookRegistered: false` and `settingsChanged: false`.

```powershell
git add -- src/SnapZones.Windows src/SnapZones.App src/SnapZones.Core/Profiles tests/SnapZones.Tests/Profiles
git commit -m "feat: add tray lifecycle hotkeys and diagnostics"
```

---

### Task 8: Documentation, verification script, visual check, and publish

**Files:**
- Create: `docs/README.md`
- Create: `scripts/verify.ps1`
- Create: `outputs/SnapZones-prototype/` through `dotnet publish`
- Create: `outputs/SnapZones-Pruefbericht.md`

**Interfaces:**
- Consumes: complete solution and `--diagnostics` mode.
- Produces: reproducible verification command and user-facing x64 prototype folder.

- [ ] **Step 1: Write the German operating guide**

Document launch, tray, editor gestures, templates, save/reset, quick slots, immediate/Shift trigger, all/active monitor overlay, autostart, configuration paths, diagnostic command and administrator-window limitation.

- [ ] **Step 2: Implement strict verification script**

```powershell
$ErrorActionPreference = 'Stop'
dotnet restore "$PSScriptRoot\..\SnapZones.sln"
dotnet test "$PSScriptRoot\..\SnapZones.sln" -c Release --no-restore
dotnet build "$PSScriptRoot\..\SnapZones.sln" -c Release --no-restore -p:Platform=x64
dotnet publish "$PSScriptRoot\..\src\SnapZones.App\SnapZones.App.csproj" -c Release -r win-x64 --self-contained true -o "$PSScriptRoot\..\outputs\SnapZones-prototype"
& "$PSScriptRoot\..\outputs\SnapZones-prototype\SnapZones.exe" --diagnostics | Tee-Object "$PSScriptRoot\..\outputs\diagnostics.json"
if ($LASTEXITCODE -ne 0) { throw "Die Diagnose ist fehlgeschlagen." }
```

- [ ] **Step 3: Run fresh automated verification**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1`
Expected: restore/build/publish exit 0; all tests pass; `outputs/SnapZones-prototype/SnapZones.exe` and valid `outputs/diagnostics.json` exist.

- [ ] **Step 4: Perform running UI and drag checks**

Open the published app, confirm that no hook is active and capture the main editor at its default 1280×800 size. Inspect focus/contrast/clipping and remove one unnecessary visual element if it does not encode state. Explicitly enable snapping, then test only Notepad for drag start, zone highlight, release placement, Escape cancellation, cross-monitor drag, emergency stop and profile switch before trying Explorer or a browser. Record exact monitor count, scales, tested applications, protection results and any untestable multi-monitor case in `outputs/SnapZones-Pruefbericht.md`.

- [ ] **Step 5: Re-run verification after visual fixes and commit**

Run: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1` and `git diff --check`.
Expected: fresh zero-failure output and no whitespace errors.

```powershell
git add -- docs/README.md scripts/verify.ps1 outputs/SnapZones-Pruefbericht.md src tests
git commit -m "docs: verify and package SnapZones prototype"
```

---

## Completion checklist

- [ ] Every spec requirement in sections 1-9 maps to a task above; every section 2.2 exclusion remains absent.
- [ ] Every behavioural test was observed failing for the expected missing behaviour before implementation.
- [ ] Full Release tests, x64 build, self-contained publish and diagnostics pass freshly.
- [ ] Running editor and drag-overlay behaviour were checked on every monitor currently available.
- [ ] `git status --short` contains no accidental build files, user configuration or logs.
- [ ] Deliverables exist only under `outputs/SnapZones-prototype` plus `outputs/SnapZones-Pruefbericht.md`.
