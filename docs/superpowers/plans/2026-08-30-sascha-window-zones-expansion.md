# Sascha Window Zones Expansion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rename and extend the Windows 11 app with precise percent/pixel zone editing, automatic free-space placement, magnetic editing, real monitor names, system theming, complete help text, and safe links to Windows display controls.

**Architecture:** Normalized rectangles remain the persisted source of truth while focused pure services perform unit conversion, free-rectangle search, magnetism, and asymmetric margin conversion. Windows-specific code is limited to documented display identity, theme-frame, and settings-launch boundaries; unsupported system mutations are explained instead of performed. WPF composes these services without moving geometry into code-behind.

**Tech Stack:** C# 12, .NET 8, WPF, Win32 P/Invoke, System.Text.Json, xUnit, PowerShell asset/verification scripts

**Spec:** `docs/superpowers/specs/2026-08-30-sascha-window-zones-expansion.md`

## Global Constraints

- Target Windows 11 x64 only.
- Use DE-CH UI copy and neutral German code comments; identifiers remain idiomatic English C#.
- Request no administrator rights and use no driver, service, process injection, Explorer hook, undocumented DPI packet, or undocumented registry mutation.
- Preserve existing `%APPDATA%\SnapZones` configuration and `%LOCALAPPDATA%\SnapZones` logs during the product rename.
- Keep snapping and autostart disabled by default and preserve the emergency stop.
- Every behavioural production method starts with a failing test; XAML and P/Invoke declarations are verified by build and diagnostics.

---

### Task 1: Core geometry and configuration

**Files:**
- Create: `src/SnapZones.Core/Geometry/ZoneEditorGeometry.cs`
- Create: `src/SnapZones.Core/Geometry/LargestFreeRectangle.cs`
- Create: `src/SnapZones.Core/Geometry/ZoneMagnetism.cs`
- Create: `src/SnapZones.Core/Models/EdgeInsets.cs`
- Modify: `src/SnapZones.Core/Models/{AppSettings,LayoutMetrics}.cs`
- Modify: `src/SnapZones.Core/Geometry/ZoneGeometry.cs`
- Test: `tests/SnapZones.Tests/Geometry/{ZoneEditorGeometry,LargestFreeRectangle,ZoneMagnetism,ZoneGeometry}Tests.cs`

**Interfaces:**
- Produces: `ZoneEditorGeometry.ToPixels`, `ZoneEditorGeometry.FromPositionAndSize`, `ZoneEditorGeometry.FromMargins`, `LargestFreeRectangle.Find`, `ZoneMagnetism.SnapMove`, `ZoneMagnetism.SnapResize`, and `EdgeInsets`.

- [ ] Write literal expected-value tests for percentage/pixel conversion, margin conversion, largest-free selection and magnetic edges.
- [ ] Run the focused tests and verify RED because the new types do not exist.
- [ ] Implement the minimal pure services and asymmetric `LayoutMetrics` conversion.
- [ ] Run focused tests and the full suite; refactor only while green.

### Task 2: Editor state and interaction

**Files:**
- Modify: `src/SnapZones.App/ViewModels/LayoutEditorViewModel.cs`
- Modify: `src/SnapZones.App/Controls/LayoutCanvas.cs`
- Modify: `src/SnapZones.App/Views/MainWindow.xaml.cs`
- Test: `tests/SnapZones.Tests/Editor/LayoutEditorViewModelTests.cs`

**Interfaces:**
- Consumes: Task 1 geometry services.
- Produces: `ZoneInputUnit`, `ZoneInputDefinition`, editor conversion properties, deterministic `AddZone()` success/failure, monitor pixel dimensions, and configurable magnet threshold.

- [ ] Add tests proving a half-width first zone produces a full-height right-side second zone, full occupancy returns a failure, and pixel/margin edits update normalized bounds.
- [ ] Run the focused tests and verify RED against the old offset placement and percent-only API.
- [ ] Implement the view-model API and connect canvas move/resize snapping with `Alt` bypass.
- [ ] Run focused tests and the full suite.

### Task 3: Documented Windows integrations

**Files:**
- Create: `src/SnapZones.Windows/Displays/DisplayPathIdentityProvider.cs`
- Create: `src/SnapZones.Windows/Displays/DisplayPathIdentity.cs`
- Create: `src/SnapZones.Windows/Theme/WindowsThemeReader.cs`
- Create: `src/SnapZones.Windows/Theme/WindowThemeFrame.cs`
- Modify: `src/SnapZones.Windows/Displays/WindowsMonitorService.cs`
- Modify: `src/SnapZones.Windows/Native/{NativeTypes,User32,DwmApi}.cs`
- Test: `tests/SnapZones.Tests/Monitors/DisplayPathIdentityTests.cs`

**Interfaces:**
- Produces: a GDI-device-name map containing EDID-friendly name and stable monitor path, plus documented system-theme read/frame application.

- [ ] Add a pure matching test whose fallback would incorrectly keep `Generic PnP Monitor`.
- [ ] Run the test and verify RED because display-path identity mapping is absent.
- [ ] Implement QueryDisplayConfig/GetDeviceInfo enumeration and deterministic fallback selection.
- [ ] Run the focused test, Windows monitor integration test, and full suite.

### Task 4: Product identity, theme, settings and help UI

**Files:**
- Create: `src/SnapZones.App/Assets/SaschaWindowZones.svg`
- Create: `scripts/build-icon.ps1`
- Create: `src/SnapZones.App/Services/ThemeService.cs`
- Modify: `src/SnapZones.App/{App.xaml,App.xaml.cs,SnapZones.App.csproj}`
- Modify: `src/SnapZones.App/Themes/Theme.xaml`
- Modify: `src/SnapZones.App/Views/{MainWindow.xaml,MainWindow.xaml.cs}`
- Modify: `src/SnapZones.App/ViewModels/{MainViewModel,MonitorChoice,SettingsViewModel}.cs`
- Modify: `src/SnapZones.App/Services/{ApplicationController,TrayIconService}.cs`
- Modify: `src/SnapZones.App/Overlays/MonitorOverlayWindow.xaml`

**Interfaces:**
- Consumes: Tasks 1-3.
- Produces: `SaschaWindowZones.exe`, multi-size application/tray icon, live System/Light/Dark theme, Profile-first navigation, two-line monitor cards, primary add-zone action, unit/definition inspector, and safe `ms-settings:` actions.

- [ ] Generate the icon deterministically and configure the application/tray resources.
- [ ] Reshape the XAML using the approved text mockup and DynamicResource theme colours.
- [ ] Add explanatory text to every setting and explicit support boundaries to monitor controls.
- [ ] Build the app with warnings as errors and correct all compile/XAML errors.

### Task 5: Compatibility, documentation and release verification

**Files:**
- Modify: `tests/SnapZones.Tests/Persistence/JsonConfigurationRepositoryTests.cs`
- Modify: `docs/README.md`
- Modify: `scripts/verify.ps1`

**Interfaces:**
- Produces: backward-compatible configuration loading, updated operating instructions, self-contained `outputs/Sascha-Window-Zones-prototype`, diagnostics JSON, and DPI evidence.

- [ ] Add a test that loads a schema-1 settings file without new optional settings and preserves safe defaults.
- [ ] Run it RED if deserialization does not apply the new defaults; implement only the required compatibility fix.
- [ ] Update documentation and release script names without changing the safety assertions.
- [ ] Run `scripts/verify.ps1` and confirm all tests, build, publish, diagnostics, DPI and artefact checks pass.

