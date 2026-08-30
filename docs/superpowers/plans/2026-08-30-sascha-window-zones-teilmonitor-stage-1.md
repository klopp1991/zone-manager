# Sascha Window Zones Teilmonitor Stage 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (- [ ]) syntax for tracking.

**Goal:** Build the shared Teilmonitor core, explicit placement commands, session-only restore history, and route the existing drag workflow through that core without changing native Windows maximise behaviour.

**Architecture:** Existing ZoneDefinition geometry remains canonical and is exposed through PartMonitorTarget and PartMonitorResolver. A pure PartMonitorCommandService calculates targets and owns session history through an injected IPartMonitorWindowGateway; WindowsWindowService implements that gateway with documented Win32 calls, while WindowDragCoordinator emits semantic target IDs instead of directly calculated window rectangles.

**Tech Stack:** C# 12, .NET 8, WPF, documented Win32 P/Invoke, xUnit, PowerShell verification

**Spec:** docs/superpowers/specs/2026-08-30-sascha-window-zones-teilmonitor-os-integration-design.md

## Global Constraints

- Target Windows 11 x64 and .NET 8; add no NuGet dependency.
- Keep ZoneDefinition, monitor IDs, profile IDs, persisted JSON property names, and schema version compatible.
- Normal Windows maximise, Win + Pfeil, Win + Z, taskbar behaviour, and Windows virtual desktops remain unchanged.
- Use no driver, Windows service, Explorer injection, foreign-process injection, private API, or undocumented registry value.
- Keep native callbacks free of file, UI, configuration, and monitor-topology work.
- Use DE-CH UI text and neutral German comments; identifiers remain idiomatic English C#.
- Preserve the existing emergency stop and keep snapping disabled by default.
- Do not reset, stash, overwrite, or silently include pre-existing worktree changes.
- Before editing any listed path, run git diff -- path and reconcile concurrent edits; execution must pause if an overlapping change cannot be preserved.
- The current worktree already contains uncommitted changes in overlapping UI and test files. Start implementation only after their owner has committed them or explicitly included them in this work.
- Every behavioural production change starts with a failing focused test.
- Each task commit must contain only the paths listed for that task.

---

## File Structure

### New core files

- src/SnapZones.Core/PartMonitors/PartMonitorTarget.cs — one physical monitor and its ordered persisted zones viewed as Teilmonitors.
- src/SnapZones.Core/PartMonitors/PartMonitorPlacement.cs — resolved physical monitor ID, Teilmonitor ID, and exact pixel bounds.
- src/SnapZones.Core/PartMonitors/PartMonitorResolver.cs — point lookup, exact target lookup, and deterministic cyclic navigation.
- src/SnapZones.Core/PartMonitors/WindowIdentity.cs — handle, process ID, and window class used to detect stale or reused handles.
- src/SnapZones.Core/PartMonitors/WindowPlacementSnapshot.cs — neutral representation of WINDOWPLACEMENT data.
- src/SnapZones.Core/PartMonitors/PlacementHistory.cs — bounded session-only history with peek-then-discard semantics.
- src/SnapZones.Core/PartMonitors/PartMonitorCommand.cs — immutable fill, cycle, and restore commands.
- src/SnapZones.Core/PartMonitors/PartMonitorCommandResult.cs — structured command result without UI text.
- src/SnapZones.Core/PartMonitors/IPartMonitorWindowGateway.cs — documented OS boundary consumed by the core command service.
- src/SnapZones.Core/PartMonitors/PartMonitorCommandService.cs — single implementation of capture, place, remember, and restore behaviour.

### Modified existing files

- src/SnapZones.Core/Drag/DragAction.cs — replace pixel-based SnapWindowAction with semantic FillPartMonitorAction.
- src/SnapZones.Core/Drag/DragState.cs — remove DragMonitorTarget after all consumers use PartMonitorTarget.
- src/SnapZones.Core/Drag/WindowDragCoordinator.cs — resolve hover targets through PartMonitorResolver.
- src/SnapZones.Core/Geometry/MonitorWorkArea.cs — expose one shared half-open point containment check.
- src/SnapZones.Windows/Native/NativeTypes.cs — add WINDOWPLACEMENT-compatible native structure.
- src/SnapZones.Windows/Native/User32.cs — add GetClassNameW, GetWindowPlacement, and SetWindowPlacement.
- src/SnapZones.Windows/Windows/IWindowService.cs — compose the new placement gateway with existing cursor and inspection operations.
- src/SnapZones.Windows/Windows/WindowsWindowService.cs — capture, validate, fill, and restore window placement.
- src/SnapZones.App/Services/ApplicationController.cs — construct the resolver and command service and execute semantic drag actions.
- src/SnapZones.App/Overlays/OverlayManager.cs — consume PartMonitorTarget.
- src/SnapZones.App/Overlays/MonitorOverlayWindow.xaml.cs — render PartMonitorTarget.PartMonitors.
- docs/README.md — describe Teilmonitor semantics and unchanged normal maximise behaviour.

### Tests

- tests/SnapZones.Tests/PartMonitors/PartMonitorResolverTests.cs
- tests/SnapZones.Tests/PartMonitors/PlacementHistoryTests.cs
- tests/SnapZones.Tests/PartMonitors/PartMonitorCommandServiceTests.cs
- tests/SnapZones.Tests/PartMonitors/WindowsWindowPlacementGatewayTests.cs
- tests/SnapZones.Tests/Drag/WindowDragCoordinatorTests.cs
- tests/SnapZones.Tests/Drag/WindowsSafetyBoundaryTests.cs
- tests/SnapZones.Tests/Theme/OverlayPresentationTests.cs

---

### Task 1: Resolve ordered Teilmonitor targets

**Files:**
- Create: src/SnapZones.Core/PartMonitors/PartMonitorTarget.cs
- Create: src/SnapZones.Core/PartMonitors/PartMonitorPlacement.cs
- Create: src/SnapZones.Core/PartMonitors/PartMonitorResolver.cs
- Test: tests/SnapZones.Tests/PartMonitors/PartMonitorResolverTests.cs

**Interfaces:**
- Consumes: LiveMonitor, ZoneDefinition, LayoutMetrics, ZoneGeometry, PointInt.
- Produces: PartMonitorTarget, PartMonitorPlacement, PartMonitorResolver.FindPhysicalMonitor, FindAt, Resolve, and Cycle.

- [ ] **Step 1: Write failing resolver tests**

Create tests/SnapZones.Tests/PartMonitors/PartMonitorResolverTests.cs with literal monitor IDs, zone IDs, negative desktop coordinates, a boundary point, exact expected pixels, and wrap-around:

~~~csharp
using SnapZones.Core.Geometry;
using SnapZones.Core.Models;
using SnapZones.Core.Monitors;
using SnapZones.Core.PartMonitors;
using Xunit;

namespace SnapZones.Tests.PartMonitors;

public sealed class PartMonitorResolverTests
{
    private static readonly Guid LeftId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid RightId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid FullId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public void FindAt_resolves_negative_desktop_coordinates_and_boundary_to_right_part()
    {
        var resolver = CreateResolver();

        var placement = resolver.FindAt(new PointInt(-960, 400));

        Assert.NotNull(placement);
        Assert.Equal("LEFT-MONITOR", placement.MonitorId);
        Assert.Equal(RightId, placement.PartMonitorId);
        Assert.Equal(new PixelRect(-960, 0, 960, 1040), placement.Bounds);
    }

    [Fact]
    public void Resolve_applies_layout_margins_and_gap_once()
    {
        var resolver = CreateResolver(new LayoutMetrics(8, 8));

        var placement = resolver.Resolve("LEFT-MONITOR", LeftId);

        Assert.NotNull(placement);
        Assert.Equal(new PixelRect(-1912, 8, 948, 1024), placement.Bounds);
    }

    [Fact]
    public void Cycle_uses_monitor_then_zone_order_and_wraps()
    {
        var resolver = CreateResolver();

        var next = resolver.Cycle("RIGHT-MONITOR", FullId, 1);
        var previous = resolver.Cycle("LEFT-MONITOR", LeftId, -1);

        Assert.Equal(LeftId, next?.PartMonitorId);
        Assert.Equal(FullId, previous?.PartMonitorId);
    }

    private static PartMonitorResolver CreateResolver(LayoutMetrics? metrics = null)
    {
        var left = new LiveMonitor(
            new MonitorIdentity("LEFT-MONITOR", "DISPLAY1", "Links"),
            new MonitorWorkArea(-1920, 0, 1920, 1040),
            96,
            96,
            false);
        var right = new LiveMonitor(
            new MonitorIdentity("RIGHT-MONITOR", "DISPLAY2", "Rechts"),
            new MonitorWorkArea(0, 0, 1920, 1040),
            96,
            96,
            true);

        return new PartMonitorResolver(
        [
            new PartMonitorTarget(left,
            [
                new ZoneDefinition(LeftId, "Links", new NormalizedRect(0, 0, 0.5, 1)),
                new ZoneDefinition(RightId, "Rechts", new NormalizedRect(0.5, 0, 0.5, 1))
            ]),
            new PartMonitorTarget(right,
            [
                new ZoneDefinition(FullId, "Voll", NormalizedRect.Full)
            ])
        ],
        metrics ?? new LayoutMetrics(0, 0));
    }
}
~~~

- [ ] **Step 2: Run the focused tests and verify RED**

Run:

~~~powershell
dotnet test SnapZones.sln -c Release --filter FullyQualifiedName~PartMonitorResolverTests
~~~

Expected: compilation fails because SnapZones.Core.PartMonitors and its types do not exist.

- [ ] **Step 3: Add the target and placement records**

Create PartMonitorTarget.cs:

~~~csharp
using SnapZones.Core.Models;
using SnapZones.Core.Monitors;

namespace SnapZones.Core.PartMonitors;

public sealed record PartMonitorTarget(
    LiveMonitor Monitor,
    IReadOnlyList<ZoneDefinition> PartMonitors);
~~~

Create PartMonitorPlacement.cs:

~~~csharp
using SnapZones.Core.Geometry;
using SnapZones.Core.Models;

namespace SnapZones.Core.PartMonitors;

public sealed record PartMonitorPlacement(
    string MonitorId,
    Guid PartMonitorId,
    PixelRect Bounds);
~~~

- [ ] **Step 4: Implement deterministic resolution**

Create PartMonitorResolver.cs:

~~~csharp
using SnapZones.Core.Geometry;
using SnapZones.Core.Models;

namespace SnapZones.Core.PartMonitors;

public sealed class PartMonitorResolver
{
    private readonly IReadOnlyList<PartMonitorTarget> targets;
    private readonly LayoutMetrics metrics;

    public PartMonitorResolver(IReadOnlyList<PartMonitorTarget> targets, LayoutMetrics metrics)
    {
        this.targets = targets;
        this.metrics = metrics;
    }

    public PartMonitorTarget? FindPhysicalMonitor(PointInt point) =>
        targets.FirstOrDefault(target => target.Monitor.WorkArea.Contains(point));

    public PartMonitorPlacement? FindAt(PointInt point)
    {
        var target = FindPhysicalMonitor(point);
        if (target is null)
        {
            return null;
        }

        var partMonitor = ZoneGeometry.HitTest(
            target.PartMonitors,
            target.Monitor.WorkArea,
            metrics,
            point);
        return partMonitor is null ? null : ToPlacement(target, partMonitor.Id);
    }

    public PartMonitorPlacement? Resolve(string monitorId, Guid partMonitorId)
    {
        var target = targets.FirstOrDefault(candidate =>
            string.Equals(candidate.Monitor.Identity.StableId, monitorId, StringComparison.OrdinalIgnoreCase));
        return target is null ? null : ToPlacement(target, partMonitorId);
    }

    public PartMonitorPlacement? Cycle(
        string currentMonitorId,
        Guid currentPartMonitorId,
        int offset)
    {
        if (offset is not (-1 or 1))
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        var ordered = targets
            .SelectMany(target => target.PartMonitors.Select(partMonitor => (target, partMonitor)))
            .ToArray();
        var current = Array.FindIndex(ordered, item =>
            string.Equals(item.target.Monitor.Identity.StableId, currentMonitorId, StringComparison.OrdinalIgnoreCase) &&
            item.partMonitor.Id == currentPartMonitorId);
        if (current < 0 || ordered.Length == 0)
        {
            return null;
        }

        var destination = (current + offset + ordered.Length) % ordered.Length;
        return ToPlacement(ordered[destination].target, ordered[destination].partMonitor.Id);
    }

    private PartMonitorPlacement? ToPlacement(PartMonitorTarget target, Guid partMonitorId)
    {
        var partMonitor = target.PartMonitors.FirstOrDefault(candidate => candidate.Id == partMonitorId);
        return partMonitor is null
            ? null
            : new PartMonitorPlacement(
                target.Monitor.Identity.StableId,
                partMonitor.Id,
                ZoneGeometry.ToPixels(partMonitor.Bounds, target.Monitor.WorkArea, metrics));
    }
}
~~~

Add this method to MonitorWorkArea in src/SnapZones.Core/Geometry/MonitorWorkArea.cs:

~~~csharp
public bool Contains(PointInt point) =>
    point.X >= X && point.X < X + Width &&
    point.Y >= Y && point.Y < Y + Height;
~~~

- [ ] **Step 5: Run focused and geometry tests**

Run:

~~~powershell
dotnet test SnapZones.sln -c Release --filter "FullyQualifiedName~PartMonitorResolverTests|FullyQualifiedName~ZoneGeometryTests"
~~~

Expected: all selected tests pass.

- [ ] **Step 6: Commit only Task 1**

~~~powershell
git add -- src/SnapZones.Core/PartMonitors/PartMonitorTarget.cs src/SnapZones.Core/PartMonitors/PartMonitorPlacement.cs src/SnapZones.Core/PartMonitors/PartMonitorResolver.cs src/SnapZones.Core/Geometry/MonitorWorkArea.cs tests/SnapZones.Tests/PartMonitors/PartMonitorResolverTests.cs
git commit --only src/SnapZones.Core/PartMonitors/PartMonitorTarget.cs src/SnapZones.Core/PartMonitors/PartMonitorPlacement.cs src/SnapZones.Core/PartMonitors/PartMonitorResolver.cs src/SnapZones.Core/Geometry/MonitorWorkArea.cs tests/SnapZones.Tests/PartMonitors/PartMonitorResolverTests.cs -m "feat: add logical part monitor resolution"
~~~

---

### Task 2: Keep bounded session-only placement history

**Files:**
- Create: src/SnapZones.Core/PartMonitors/WindowIdentity.cs
- Create: src/SnapZones.Core/PartMonitors/WindowPlacementSnapshot.cs
- Create: src/SnapZones.Core/PartMonitors/PlacementHistory.cs
- Test: tests/SnapZones.Tests/PartMonitors/PlacementHistoryTests.cs

**Interfaces:**
- Consumes: PixelRect and PointInt.
- Produces: WindowIdentity, WindowPlacementSnapshot, PlacementHistory.Remember, TryPeek, DiscardTop, and Remove.

- [ ] **Step 1: Write failing bounded-history tests**

~~~csharp
using SnapZones.Core.Geometry;
using SnapZones.Core.PartMonitors;
using Xunit;

namespace SnapZones.Tests.PartMonitors;

public sealed class PlacementHistoryTests
{
    private static readonly WindowIdentity Window = new((nint)42, 100, "TestWindow");

    [Fact]
    public void Remember_keeps_only_configured_depth_and_peeks_newest()
    {
        var history = new PlacementHistory(maxDepth: 2);
        history.Remember(Snapshot(1));
        history.Remember(Snapshot(2));
        history.Remember(Snapshot(3));

        Assert.True(history.TryPeek(Window, out var newest));
        Assert.Equal(3, newest.NormalPosition.X);
        Assert.True(history.DiscardTop(Window));
        Assert.True(history.TryPeek(Window, out var remaining));
        Assert.Equal(2, remaining.NormalPosition.X);
    }

    [Fact]
    public void DiscardTop_does_not_affect_other_window()
    {
        var history = new PlacementHistory();
        var other = new WindowIdentity((nint)43, 100, "TestWindow");
        history.Remember(Snapshot(1));
        history.Remember(Snapshot(2) with { Identity = other });

        Assert.True(history.DiscardTop(Window));
        Assert.False(history.TryPeek(Window, out _));
        Assert.True(history.TryPeek(other, out _));
    }

    private static WindowPlacementSnapshot Snapshot(int x) => new(
        Window,
        Flags: 0,
        ShowCommand: 1,
        MinPosition: new PointInt(-1, -1),
        MaxPosition: new PointInt(-1, -1),
        NormalPosition: new PixelRect(x, 20, 800, 600));
}
~~~

- [ ] **Step 2: Run the focused tests and verify RED**

Run:

~~~powershell
dotnet test SnapZones.sln -c Release --filter FullyQualifiedName~PlacementHistoryTests
~~~

Expected: compilation fails because placement history types do not exist.

- [ ] **Step 3: Add neutral placement value types**

Create WindowIdentity.cs:

~~~csharp
namespace SnapZones.Core.PartMonitors;

public readonly record struct WindowIdentity(
    nint Handle,
    uint ProcessId,
    string WindowClass);
~~~

Create WindowPlacementSnapshot.cs:

~~~csharp
using SnapZones.Core.Geometry;

namespace SnapZones.Core.PartMonitors;

public sealed record WindowPlacementSnapshot(
    WindowIdentity Identity,
    uint Flags,
    uint ShowCommand,
    PointInt MinPosition,
    PointInt MaxPosition,
    PixelRect NormalPosition);
~~~

- [ ] **Step 4: Implement bounded peek-then-discard history**

Create PlacementHistory.cs:

~~~csharp
namespace SnapZones.Core.PartMonitors;

public sealed class PlacementHistory
{
    private readonly int maxDepth;
    private readonly Dictionary<WindowIdentity, List<WindowPlacementSnapshot>> entries = [];

    public PlacementHistory(int maxDepth = 30)
    {
        if (maxDepth < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxDepth));
        }

        this.maxDepth = maxDepth;
    }

    public void Remember(WindowPlacementSnapshot snapshot)
    {
        if (!entries.TryGetValue(snapshot.Identity, out var history))
        {
            history = [];
            entries.Add(snapshot.Identity, history);
        }

        history.Add(snapshot);
        if (history.Count > maxDepth)
        {
            history.RemoveAt(0);
        }
    }

    public bool TryPeek(WindowIdentity identity, out WindowPlacementSnapshot snapshot)
    {
        if (entries.TryGetValue(identity, out var history) && history.Count > 0)
        {
            snapshot = history[^1];
            return true;
        }

        snapshot = null!;
        return false;
    }

    public bool DiscardTop(WindowIdentity identity)
    {
        if (!entries.TryGetValue(identity, out var history) || history.Count == 0)
        {
            return false;
        }

        history.RemoveAt(history.Count - 1);
        if (history.Count == 0)
        {
            entries.Remove(identity);
        }

        return true;
    }

    public void Remove(WindowIdentity identity) => entries.Remove(identity);
}
~~~

- [ ] **Step 5: Run focused tests**

Run:

~~~powershell
dotnet test SnapZones.sln -c Release --filter FullyQualifiedName~PlacementHistoryTests
~~~

Expected: both tests pass.

- [ ] **Step 6: Commit only Task 2**

~~~powershell
git add -- src/SnapZones.Core/PartMonitors/WindowIdentity.cs src/SnapZones.Core/PartMonitors/WindowPlacementSnapshot.cs src/SnapZones.Core/PartMonitors/PlacementHistory.cs tests/SnapZones.Tests/PartMonitors/PlacementHistoryTests.cs
git commit --only src/SnapZones.Core/PartMonitors/WindowIdentity.cs src/SnapZones.Core/PartMonitors/WindowPlacementSnapshot.cs src/SnapZones.Core/PartMonitors/PlacementHistory.cs tests/SnapZones.Tests/PartMonitors/PlacementHistoryTests.cs -m "feat: add session placement history"
~~~

---

### Task 3: Execute semantic fill, cycle, and restore commands

**Files:**
- Create: src/SnapZones.Core/PartMonitors/PartMonitorCommand.cs
- Create: src/SnapZones.Core/PartMonitors/PartMonitorCommandResult.cs
- Create: src/SnapZones.Core/PartMonitors/IPartMonitorWindowGateway.cs
- Create: src/SnapZones.Core/PartMonitors/PartMonitorCommandService.cs
- Test: tests/SnapZones.Tests/PartMonitors/PartMonitorCommandServiceTests.cs

**Interfaces:**
- Consumes: PartMonitorResolver, PlacementHistory, WindowPlacementSnapshot.
- Produces: FillPartMonitorCommand, CyclePartMonitorCommand, RestorePreviousPlacementCommand, PartMonitorCommandStatus, PartMonitorCommandResult, IPartMonitorWindowGateway, and PartMonitorCommandService.Execute.

- [ ] **Step 1: Write failing command-service tests with an in-memory gateway**

Create the complete tests/SnapZones.Tests/PartMonitors/PartMonitorCommandServiceTests.cs:

~~~csharp
using SnapZones.Core.Geometry;
using SnapZones.Core.Models;
using SnapZones.Core.Monitors;
using SnapZones.Core.PartMonitors;
using Xunit;

namespace SnapZones.Tests.PartMonitors;

public sealed class PartMonitorCommandServiceTests
{
    private static readonly Guid LeftId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid RightId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void Fill_applies_exact_target_and_remembers_previous_placement_only_after_success()
    {
        var gateway = new FakeGateway();
        var history = new PlacementHistory();
        var service = CreateService(gateway, history);

        var result = service.Execute(new FillPartMonitorCommand((nint)42, "DISPLAY-A", RightId));

        Assert.Equal(PartMonitorCommandStatus.Successful, result.Status);
        Assert.Equal(new PixelRect(960, 0, 960, 1040), gateway.AppliedBounds);
        Assert.True(history.TryPeek(gateway.Current.Identity, out _));
    }

    [Fact]
    public void Fill_does_not_record_history_when_windows_rejects_placement()
    {
        var gateway = new FakeGateway { AcceptApply = false };
        var history = new PlacementHistory();
        var service = CreateService(gateway, history);

        var result = service.Execute(new FillPartMonitorCommand((nint)42, "DISPLAY-A", RightId));

        Assert.Equal(PartMonitorCommandStatus.WindowsRejected, result.Status);
        Assert.False(history.TryPeek(gateway.Current.Identity, out _));
    }

    [Fact]
    public void Restore_discards_history_only_after_windows_accepts_restore()
    {
        var gateway = new FakeGateway();
        var history = new PlacementHistory();
        history.Remember(gateway.Current);
        var service = CreateService(gateway, history);

        gateway.AcceptRestore = false;
        Assert.Equal(
            PartMonitorCommandStatus.WindowsRejected,
            service.Execute(new RestorePreviousPlacementCommand((nint)42)).Status);
        Assert.True(history.TryPeek(gateway.Current.Identity, out _));

        gateway.AcceptRestore = true;
        Assert.Equal(
            PartMonitorCommandStatus.Successful,
            service.Execute(new RestorePreviousPlacementCommand((nint)42)).Status);
        Assert.False(history.TryPeek(gateway.Current.Identity, out _));
    }

    [Fact]
    public void Cycle_wraps_and_unknown_target_does_not_capture_window()
    {
        var gateway = new FakeGateway();
        var service = CreateService(gateway, new PlacementHistory());

        var cycle = service.Execute(new CyclePartMonitorCommand(
            (nint)42,
            "DISPLAY-A",
            RightId,
            1));
        var missing = service.Execute(new FillPartMonitorCommand(
            (nint)42,
            "MISSING",
            RightId));

        Assert.Equal(PartMonitorCommandStatus.Successful, cycle.Status);
        Assert.Equal(LeftId, cycle.Placement?.PartMonitorId);
        Assert.Equal(PartMonitorCommandStatus.TargetMissing, missing.Status);
        Assert.Equal(1, gateway.CaptureCount);
    }

    private static PartMonitorCommandService CreateService(
        IPartMonitorWindowGateway gateway,
        PlacementHistory history)
    {
        var monitor = new LiveMonitor(
            new MonitorIdentity("DISPLAY-A", "DISPLAY1", "Haupt"),
            new MonitorWorkArea(0, 0, 1920, 1040),
            96,
            96,
            true);
        var resolver = new PartMonitorResolver(
        [
            new PartMonitorTarget(monitor,
            [
                new ZoneDefinition(LeftId, "Links", new NormalizedRect(0, 0, 0.5, 1)),
                new ZoneDefinition(RightId, "Rechts", new NormalizedRect(0.5, 0, 0.5, 1))
            ])
        ],
        new LayoutMetrics(0, 0));
        return new PartMonitorCommandService(resolver, history, gateway);
    }

    private sealed class FakeGateway : IPartMonitorWindowGateway
    {
        public WindowPlacementSnapshot Current { get; } = new(
            new WindowIdentity((nint)42, 100, "TestWindow"),
            0,
            1,
            new PointInt(-1, -1),
            new PointInt(-1, -1),
            new PixelRect(20, 20, 800, 600));

        public bool AcceptApply { get; set; } = true;
        public bool AcceptRestore { get; set; } = true;
        public int CaptureCount { get; private set; }
        public PixelRect? AppliedBounds { get; private set; }

        public WindowPlacementSnapshot? Capture(nint windowHandle)
        {
            CaptureCount++;
            return windowHandle == Current.Identity.Handle ? Current : null;
        }

        public bool TryApplyNormal(WindowIdentity identity, PixelRect bounds)
        {
            AppliedBounds = bounds;
            return AcceptApply;
        }

        public bool TryRestore(WindowPlacementSnapshot snapshot) => AcceptRestore;
    }
}
~~~

- [ ] **Step 2: Run the focused tests and verify RED**

Run:

~~~powershell
dotnet test SnapZones.sln -c Release --filter FullyQualifiedName~PartMonitorCommandServiceTests
~~~

Expected: compilation fails because command and gateway types do not exist.

- [ ] **Step 3: Add commands, results, and gateway contract**

Create PartMonitorCommand.cs:

~~~csharp
namespace SnapZones.Core.PartMonitors;

public abstract record PartMonitorCommand(nint WindowHandle);

public sealed record FillPartMonitorCommand(
    nint WindowHandle,
    string MonitorId,
    Guid PartMonitorId) : PartMonitorCommand(WindowHandle);

public sealed record CyclePartMonitorCommand(
    nint WindowHandle,
    string CurrentMonitorId,
    Guid CurrentPartMonitorId,
    int Offset) : PartMonitorCommand(WindowHandle);

public sealed record RestorePreviousPlacementCommand(
    nint WindowHandle) : PartMonitorCommand(WindowHandle);
~~~

Create PartMonitorCommandResult.cs:

~~~csharp
namespace SnapZones.Core.PartMonitors;

public enum PartMonitorCommandStatus
{
    Successful,
    NotEligible,
    TargetMissing,
    WindowsRejected,
    NoPreviousPlacement
}

public sealed record PartMonitorCommandResult(
    PartMonitorCommandStatus Status,
    PartMonitorPlacement? Placement = null);
~~~

Create IPartMonitorWindowGateway.cs:

~~~csharp
using SnapZones.Core.Geometry;

namespace SnapZones.Core.PartMonitors;

public interface IPartMonitorWindowGateway
{
    WindowPlacementSnapshot? Capture(nint windowHandle);
    bool TryApplyNormal(WindowIdentity identity, PixelRect bounds);
    bool TryRestore(WindowPlacementSnapshot snapshot);
}
~~~

- [ ] **Step 4: Implement the command service**

Create PartMonitorCommandService.cs:

~~~csharp
namespace SnapZones.Core.PartMonitors;

public sealed class PartMonitorCommandService
{
    private readonly PartMonitorResolver resolver;
    private readonly PlacementHistory history;
    private readonly IPartMonitorWindowGateway gateway;

    public PartMonitorCommandService(
        PartMonitorResolver resolver,
        PlacementHistory history,
        IPartMonitorWindowGateway gateway)
    {
        this.resolver = resolver;
        this.history = history;
        this.gateway = gateway;
    }

    public PartMonitorCommandResult Execute(PartMonitorCommand command) => command switch
    {
        FillPartMonitorCommand fill => Place(
            fill.WindowHandle,
            resolver.Resolve(fill.MonitorId, fill.PartMonitorId)),
        CyclePartMonitorCommand cycle => Place(
            cycle.WindowHandle,
            resolver.Cycle(
                cycle.CurrentMonitorId,
                cycle.CurrentPartMonitorId,
                cycle.Offset)),
        RestorePreviousPlacementCommand restore => Restore(restore.WindowHandle),
        _ => throw new ArgumentOutOfRangeException(nameof(command))
    };

    private PartMonitorCommandResult Place(nint windowHandle, PartMonitorPlacement? placement)
    {
        if (placement is null)
        {
            return new PartMonitorCommandResult(PartMonitorCommandStatus.TargetMissing);
        }

        var previous = gateway.Capture(windowHandle);
        if (previous is null)
        {
            return new PartMonitorCommandResult(PartMonitorCommandStatus.NotEligible);
        }

        if (!gateway.TryApplyNormal(previous.Identity, placement.Bounds))
        {
            return new PartMonitorCommandResult(
                PartMonitorCommandStatus.WindowsRejected,
                placement);
        }

        history.Remember(previous);
        return new PartMonitorCommandResult(PartMonitorCommandStatus.Successful, placement);
    }

    private PartMonitorCommandResult Restore(nint windowHandle)
    {
        var current = gateway.Capture(windowHandle);
        if (current is null)
        {
            return new PartMonitorCommandResult(PartMonitorCommandStatus.NotEligible);
        }

        if (!history.TryPeek(current.Identity, out var previous))
        {
            return new PartMonitorCommandResult(PartMonitorCommandStatus.NoPreviousPlacement);
        }

        if (!gateway.TryRestore(previous))
        {
            return new PartMonitorCommandResult(PartMonitorCommandStatus.WindowsRejected);
        }

        history.DiscardTop(current.Identity);
        return new PartMonitorCommandResult(PartMonitorCommandStatus.Successful);
    }
}
~~~

- [ ] **Step 5: Run all PartMonitors tests**

Run:

~~~powershell
dotnet test SnapZones.sln -c Release --filter FullyQualifiedName~PartMonitors
~~~

Expected: all PartMonitors tests pass.

- [ ] **Step 6: Commit only Task 3**

~~~powershell
git add -- src/SnapZones.Core/PartMonitors/PartMonitorCommand.cs src/SnapZones.Core/PartMonitors/PartMonitorCommandResult.cs src/SnapZones.Core/PartMonitors/IPartMonitorWindowGateway.cs src/SnapZones.Core/PartMonitors/PartMonitorCommandService.cs tests/SnapZones.Tests/PartMonitors/PartMonitorCommandServiceTests.cs
git commit --only src/SnapZones.Core/PartMonitors/PartMonitorCommand.cs src/SnapZones.Core/PartMonitors/PartMonitorCommandResult.cs src/SnapZones.Core/PartMonitors/IPartMonitorWindowGateway.cs src/SnapZones.Core/PartMonitors/PartMonitorCommandService.cs tests/SnapZones.Tests/PartMonitors/PartMonitorCommandServiceTests.cs -m "feat: add part monitor placement commands"
~~~

---

### Task 4: Implement the documented Windows placement gateway

**Files:**
- Modify: src/SnapZones.Windows/Native/NativeTypes.cs
- Modify: src/SnapZones.Windows/Native/User32.cs
- Modify: src/SnapZones.Windows/Windows/IWindowService.cs
- Modify: src/SnapZones.Windows/Windows/WindowsWindowService.cs
- Test: tests/SnapZones.Tests/PartMonitors/WindowsWindowPlacementGatewayTests.cs
- Modify test: tests/SnapZones.Tests/Drag/WindowsSafetyBoundaryTests.cs

**Interfaces:**
- Consumes: IPartMonitorWindowGateway and neutral placement types from Task 3.
- Produces: WindowsWindowService.Capture, TryApplyNormal, and TryRestore with identity revalidation before each mutation.

- [ ] **Step 1: Write failing invalid-handle and roundtrip tests**

Create WindowsWindowPlacementGatewayTests.cs:

~~~csharp
using System.Windows.Forms;
using SnapZones.Core.Geometry;
using SnapZones.Core.PartMonitors;
using SnapZones.Windows.Windows;
using Xunit;

namespace SnapZones.Tests.PartMonitors;

public sealed class WindowsWindowPlacementGatewayTests
{
    [Fact]
    public void Invalid_handle_is_rejected_without_side_effects()
    {
        var service = new WindowsWindowService();
        var identity = new WindowIdentity(0, 0, string.Empty);

        Assert.Null(service.Capture(0));
        Assert.False(service.TryApplyNormal(identity, new PixelRect(0, 0, 800, 600)));
    }

    [Fact]
    public void Visible_window_can_be_filled_then_restored()
    {
        using var form = new Form
        {
            StartPosition = FormStartPosition.Manual,
            Bounds = new System.Drawing.Rectangle(80, 90, 640, 480)
        };
        form.Show();
        Application.DoEvents();
        var service = new WindowsWindowService();
        var original = Assert.IsType<WindowPlacementSnapshot>(service.Capture(form.Handle));
        Assert.True(service.TryApplyNormal(
            original.Identity,
            new PixelRect(160, 170, 800, 600)));
        Assert.True(service.TryRestore(original));
        Application.DoEvents();

        var restored = Assert.IsType<WindowPlacementSnapshot>(service.Capture(form.Handle));
        Assert.Equal(original.NormalPosition, restored.NormalPosition);
    }
}
~~~

Extend the existing invalid-handle test in WindowsSafetyBoundaryTests with Capture and TryApplyNormal assertions. Retain the current TrySnap assertion until Task 5 removes the compatibility method:

~~~csharp
var identity = new WindowIdentity(0, 0, string.Empty);
Assert.Null(service.Capture(0));
Assert.False(service.TryApplyNormal(identity, new PixelRect(0, 0, 800, 600)));
Assert.False(service.TrySnap(0, new PixelRect(0, 0, 800, 600)));
~~~

- [ ] **Step 2: Run the Windows placement tests and verify RED**

Run:

~~~powershell
dotnet test SnapZones.sln -c Release --filter "FullyQualifiedName~WindowsWindowPlacementGatewayTests|FullyQualifiedName~WindowsSafetyBoundaryTests"
~~~

Expected: compilation fails because WindowsWindowService does not implement the placement gateway.

- [ ] **Step 3: Add native WINDOWPLACEMENT declarations**

Append to NativeTypes.cs:

~~~csharp
[StructLayout(LayoutKind.Sequential)]
internal struct WindowPlacementNative
{
    public uint Length;
    public uint Flags;
    public uint ShowCommand;
    public PointNative MinPosition;
    public PointNative MaxPosition;
    public RectNative NormalPosition;
}
~~~

Add to User32.cs:

~~~csharp
[DllImport("user32.dll", EntryPoint = "GetClassNameW", CharSet = CharSet.Unicode)]
internal static extern int GetClassName(nint window, StringBuilder className, int maximumCount);

[DllImport("user32.dll", SetLastError = true)]
[return: MarshalAs(UnmanagedType.Bool)]
internal static extern bool GetWindowPlacement(
    nint window,
    ref WindowPlacementNative placement);

[DllImport("user32.dll", SetLastError = true)]
[return: MarshalAs(UnmanagedType.Bool)]
internal static extern bool SetWindowPlacement(
    nint window,
    ref WindowPlacementNative placement);
~~~

Add using System.Text to User32.cs.

- [ ] **Step 4: Compose the gateway into IWindowService**

Compose the new gateway into IWindowService and retain TrySnap as a compatibility member until Task 5 migrates ApplicationController:

~~~csharp
using SnapZones.Core.Drag;
using SnapZones.Core.Geometry;
using SnapZones.Core.PartMonitors;

namespace SnapZones.Windows.Windows;

public interface IWindowService : IPartMonitorWindowGateway
{
    WindowSnapshot? Inspect(nint window, PointInt cursor, int ownProcessId);
    bool TrySnap(nint window, PixelRect bounds);
    bool TryGetCursorPosition(out PointInt point);
    bool IsEscapePressed();
    bool IsShiftPressed();
}
~~~

- [ ] **Step 5: Implement capture and identity revalidation**

Add using System.Runtime.InteropServices, using System.Text, and using SnapZones.Core.PartMonitors to WindowsWindowService.cs. Add these methods and helpers to WindowsWindowService, reusing the existing SetWindowPos flags:

~~~csharp
public WindowPlacementSnapshot? Capture(nint window)
{
    if (!TryGetIdentity(window, out var identity))
    {
        return null;
    }

    var placement = new WindowPlacementNative
    {
        Length = (uint)Marshal.SizeOf<WindowPlacementNative>()
    };
    if (!User32.GetWindowPlacement(window, ref placement))
    {
        return null;
    }

    return new WindowPlacementSnapshot(
        identity,
        placement.Flags,
        placement.ShowCommand,
        new PointInt(placement.MinPosition.X, placement.MinPosition.Y),
        new PointInt(placement.MaxPosition.X, placement.MaxPosition.Y),
        ToPixelRect(placement.NormalPosition));
}

public bool TryApplyNormal(WindowIdentity identity, PixelRect bounds)
{
    if (!MatchesCurrentIdentity(identity) || bounds.Width < 1 || bounds.Height < 1)
    {
        return false;
    }

    _ = User32.ShowWindow(identity.Handle, Restore);
    return User32.SetWindowPos(
        identity.Handle,
        0,
        bounds.X,
        bounds.Y,
        bounds.Width,
        bounds.Height,
        NoZOrder | NoActivate | NoOwnerZOrder | AsyncWindowPosition);
}

public bool TryRestore(WindowPlacementSnapshot snapshot)
{
    if (!MatchesCurrentIdentity(snapshot.Identity))
    {
        return false;
    }

    var placement = new WindowPlacementNative
    {
        Length = (uint)Marshal.SizeOf<WindowPlacementNative>(),
        Flags = snapshot.Flags,
        ShowCommand = snapshot.ShowCommand,
        MinPosition = new PointNative
        {
            X = snapshot.MinPosition.X,
            Y = snapshot.MinPosition.Y
        },
        MaxPosition = new PointNative
        {
            X = snapshot.MaxPosition.X,
            Y = snapshot.MaxPosition.Y
        },
        NormalPosition = ToNativeRect(snapshot.NormalPosition)
    };
    return User32.SetWindowPlacement(snapshot.Identity.Handle, ref placement);
}

private static bool MatchesCurrentIdentity(WindowIdentity expected) =>
    TryGetIdentity(expected.Handle, out var current) && current == expected;

private static bool TryGetIdentity(nint window, out WindowIdentity identity)
{
    identity = new WindowIdentity(0, 0, string.Empty);
    if (window == 0 || !User32.IsWindow(window))
    {
        return false;
    }

    _ = User32.GetWindowThreadProcessId(window, out var processId);
    var className = new StringBuilder(256);
    if (processId == 0 || User32.GetClassName(window, className, className.Capacity) < 1)
    {
        return false;
    }

    identity = new WindowIdentity(window, processId, className.ToString());
    return true;
}
~~~

Add these exact conversion helpers and retain the existing TrySnap implementation until Task 5:

~~~csharp
private static PixelRect ToPixelRect(RectNative rectangle) => new(
    rectangle.Left,
    rectangle.Top,
    rectangle.Right - rectangle.Left,
    rectangle.Bottom - rectangle.Top);

private static RectNative ToNativeRect(PixelRect rectangle) => new()
{
    Left = rectangle.X,
    Top = rectangle.Y,
    Right = rectangle.Right,
    Bottom = rectangle.Bottom
};
~~~

- [ ] **Step 6: Run focused Windows tests**

Run:

~~~powershell
dotnet test SnapZones.sln -c Release --filter "FullyQualifiedName~WindowsWindowPlacementGatewayTests|FullyQualifiedName~WindowsSafetyBoundaryTests"
~~~

Expected: all selected tests pass; no window remains moved after the roundtrip test.

- [ ] **Step 7: Commit only Task 4**

~~~powershell
git add -- src/SnapZones.Windows/Native/NativeTypes.cs src/SnapZones.Windows/Native/User32.cs src/SnapZones.Windows/Windows/IWindowService.cs src/SnapZones.Windows/Windows/WindowsWindowService.cs tests/SnapZones.Tests/PartMonitors/WindowsWindowPlacementGatewayTests.cs tests/SnapZones.Tests/Drag/WindowsSafetyBoundaryTests.cs
git commit --only src/SnapZones.Windows/Native/NativeTypes.cs src/SnapZones.Windows/Native/User32.cs src/SnapZones.Windows/Windows/IWindowService.cs src/SnapZones.Windows/Windows/WindowsWindowService.cs tests/SnapZones.Tests/PartMonitors/WindowsWindowPlacementGatewayTests.cs tests/SnapZones.Tests/Drag/WindowsSafetyBoundaryTests.cs -m "feat: add safe Windows placement gateway"
~~~

---

### Task 5: Route the existing drag workflow through Teilmonitor commands

**Files:**
- Modify: src/SnapZones.Core/Drag/DragAction.cs
- Modify: src/SnapZones.Core/Drag/DragState.cs
- Modify: src/SnapZones.Core/Drag/WindowDragCoordinator.cs
- Modify: src/SnapZones.App/Services/ApplicationController.cs
- Modify: src/SnapZones.App/Overlays/OverlayManager.cs
- Modify: src/SnapZones.App/Overlays/MonitorOverlayWindow.xaml.cs
- Modify: src/SnapZones.Windows/Windows/IWindowService.cs
- Modify: src/SnapZones.Windows/Windows/WindowsWindowService.cs
- Modify: docs/README.md
- Test: tests/SnapZones.Tests/Drag/WindowDragCoordinatorTests.cs
- Test: tests/SnapZones.Tests/Drag/WindowsSafetyBoundaryTests.cs
- Test: tests/SnapZones.Tests/Theme/OverlayPresentationTests.cs

**Interfaces:**
- Consumes: PartMonitorTarget, PartMonitorResolver, PartMonitorCommandService, and WindowsWindowService from Tasks 1 to 4.
- Produces: FillPartMonitorAction and an end-to-end drag path that calculates exact pixels only inside PartMonitorResolver.

- [ ] **Step 1: Change drag tests to require semantic target IDs**

Replace SnapWindowAction assertions in WindowDragCoordinatorTests with:

~~~csharp
var fill = Assert.IsType<FillPartMonitorAction>(actions[2]);
Assert.Equal((nint)42, fill.WindowHandle);
Assert.Equal("A", fill.MonitorId);
Assert.Equal(Guid.Parse("22222222-2222-2222-2222-222222222222"), fill.PartMonitorId);
~~~

Add using SnapZones.Core.PartMonitors and replace each test target construction and cancel assertion exactly as follows, retaining the existing monitor and zones variables:

~~~csharp
new PartMonitorTarget(first, zones)
new PartMonitorTarget(
    second,
    [new ZoneDefinition(Guid.NewGuid(), "Voll", NormalizedRect.Full)])

Assert.DoesNotContain(actions, action => action is FillPartMonitorAction);
~~~

- [ ] **Step 2: Run drag tests and verify RED**

Run:

~~~powershell
dotnet test SnapZones.sln -c Release --filter FullyQualifiedName~WindowDragCoordinatorTests
~~~

Expected: compilation fails because FillPartMonitorAction is absent and WindowDragCoordinator still consumes DragMonitorTarget.

- [ ] **Step 3: Replace the pixel action with a semantic action**

In DragAction.cs replace SnapWindowAction with:

~~~csharp
public sealed record FillPartMonitorAction(
    nint WindowHandle,
    string MonitorId,
    Guid PartMonitorId) : DragAction;
~~~

Remove DragMonitorTarget from DragState.cs and its now-unused model and monitor using directives.

- [ ] **Step 4: Refactor WindowDragCoordinator to use the resolver**

Change the constructor and fields to:

~~~csharp
private readonly IReadOnlyList<PartMonitorTarget> targets;
private readonly PartMonitorResolver resolver;
private readonly OverlayScope overlayScope;
private nint windowHandle;
private PartMonitorPlacement? hoverPlacement;

public WindowDragCoordinator(
    IReadOnlyList<PartMonitorTarget> targets,
    LayoutMetrics metrics,
    OverlayScope overlayScope)
{
    this.targets = targets;
    resolver = new PartMonitorResolver(targets, metrics);
    this.overlayScope = overlayScope;
}
~~~

Use resolver.FindPhysicalMonitor in Start, resolver.FindAt in Update, and emit:

~~~csharp
public void Update(PointInt cursor)
{
    if (State != DragState.Tracking)
    {
        return;
    }

    var placement = resolver.FindAt(cursor);
    if (placement == hoverPlacement)
    {
        return;
    }

    hoverPlacement = placement;
    ActionRequested?.Invoke(new HighlightZoneAction(
        placement?.MonitorId,
        placement?.PartMonitorId));
}
~~~

In Start replace FindTarget with resolver.FindPhysicalMonitor. Keep the existing overlay-scope selection over targets. At End emit only:

~~~csharp
if (hoverPlacement is not null)
{
    ActionRequested?.Invoke(new FillPartMonitorAction(
        windowHandle,
        hoverPlacement.MonitorId,
        hoverPlacement.PartMonitorId));
}
~~~

Reset only the new hover state in ResetState:

~~~csharp
State = DragState.Idle;
windowHandle = 0;
hoverPlacement = null;
~~~

Delete the former hoverTarget and hoverZone fields, direct ZoneGeometry.ToPixels call, and private FindTarget method because PartMonitorResolver now owns all three decisions.

- [ ] **Step 5: Update overlays without changing their visuals**

Add using SnapZones.Core.PartMonitors to OverlayManager and MonitorOverlayWindow, remove their Drag namespace dependency where no longer required, and apply these exact type/property substitutions:

~~~csharp
private IReadOnlyDictionary<string, PartMonitorTarget> targets =
    new Dictionary<string, PartMonitorTarget>();

public void UpdateTargets(IReadOnlyList<PartMonitorTarget> newTargets)

private PartMonitorTarget? target;

public void ShowFor(
    PartMonitorTarget newTarget,
    LayoutMetrics newMetrics,
    string colour,
    double opacity,
    bool displayZoneNames)

foreach (var zone in target.PartMonitors)
~~~

All Geometry, colour, DPI, inset, label, and hit-test presentation logic remains byte-for-byte equivalent apart from these substitutions.

In OverlayPresentationTests add using SnapZones.Core.PartMonitors and replace its existing target construction only:

~~~csharp
var target = new PartMonitorTarget(
    monitor,
[
    new ZoneDefinition(firstId, "Links", new NormalizedRect(0, 0, 0.5, 1)),
    new ZoneDefinition(secondId, "Rechts", new NormalizedRect(0.5, 0, 0.5, 1))
]);
~~~

Keep its visual border, inset, label, and opacity assertions unchanged.

- [ ] **Step 6: Wire one command service into ApplicationController**

Add using SnapZones.Core.PartMonitors and these fields:

~~~csharp
private readonly PlacementHistory placementHistory = new();
private PartMonitorCommandService? partMonitorCommands;
~~~

Replace BuildTargets with:

~~~csharp
private IReadOnlyList<PartMonitorTarget> BuildTargets(LayoutProfile activeProfile)
{
    var result = new List<PartMonitorTarget>();
    foreach (var monitor in monitors)
    {
        var layout = activeProfile.Monitors.FirstOrDefault(saved =>
            string.Equals(
                saved.Monitor.StableId,
                monitor.Identity.StableId,
                StringComparison.OrdinalIgnoreCase) ||
            string.Equals(
                saved.Monitor.DeviceName,
                monitor.Identity.DeviceName,
                StringComparison.OrdinalIgnoreCase));
        var partMonitors = layout?.Zones ??
        [
            new ZoneDefinition(Guid.NewGuid(), "Voll", NormalizedRect.Full)
        ];
        result.Add(new PartMonitorTarget(monitor, partMonitors));
    }

    return result;
}
~~~

In Reconfigure create exactly one resolver and command service for the active immutable target snapshot:

~~~csharp
var metrics = new LayoutMetrics(
    newConfiguration.Settings.EffectiveOuterMargins,
    newConfiguration.Settings.ZoneGap);
var resolver = new PartMonitorResolver(targets, metrics);
partMonitorCommands = new PartMonitorCommandService(
    resolver,
    placementHistory,
    windowService);
coordinator = new WindowDragCoordinator(
    targets,
    metrics,
    newConfiguration.Settings.OverlayScope);
~~~

Replace the SnapWindowAction case with:

~~~csharp
case FillPartMonitorAction fill:
    var result = partMonitorCommands?.Execute(new FillPartMonitorCommand(
        fill.WindowHandle,
        fill.MonitorId,
        fill.PartMonitorId));
    if (result?.Status == PartMonitorCommandStatus.Successful)
    {
        log.Write("DEBUG", "Fenster wurde in einen Teilmonitor eingerastet.");
    }
    else
    {
        log.Write(
            "WARN",
            "Teilmonitor-Platzierung abgelehnt: " +
            (result?.Status.ToString() ?? "Komponente nicht bereit"));
    }
    break;
~~~

Do not add hotkeys, title-bar interception, rules, or workspace behaviour in this stage.

- [ ] **Step 7: Remove the obsolete direct snap API**

After all callers use PartMonitorCommandService, remove TrySnap from IWindowService and WindowsWindowService and remove its old invalid-handle assertion. Keep SetWindowPos private to WindowsWindowService through TryApplyNormal.

- [ ] **Step 8: Document the delivered semantics**

Add this neutral paragraph under Monitore und Windows-Anzeige in docs/README.md:

~~~markdown
Jede gespeicherte Zone ist zugleich ein logischer Teilmonitor. Ein Fenster, das per Overlay oder Teilmonitor-Befehl platziert wird, bleibt ein normales Fenster. Der Windows-Maximieren-Button, Doppelklick auf die Titelleiste, Win + Pfeil und Win + Z verwenden weiterhin die gesamte Windows-Arbeitsfläche des physischen Monitors.
~~~

- [ ] **Step 9: Run focused drag, overlay, and PartMonitors tests**

Run:

~~~powershell
dotnet test SnapZones.sln -c Release --filter "FullyQualifiedName~WindowDragCoordinatorTests|FullyQualifiedName~OverlayPresentationTests|FullyQualifiedName~PartMonitors"
~~~

Expected: all selected tests pass.

- [ ] **Step 10: Run the full test suite and build**

Run:

~~~powershell
dotnet test SnapZones.sln -c Release
dotnet build SnapZones.sln -c Release --no-restore
~~~

Expected: zero failed tests, zero build errors, and zero warnings.

- [ ] **Step 11: Commit only Task 5 after resolving pre-existing overlapping edits**

Do not run this commit until git diff confirms that existing changes in MonitorOverlayWindow.xaml.cs, OverlayPresentationTests.cs, and docs/README.md were preserved intentionally.

~~~powershell
git add -- src/SnapZones.Core/Drag/DragAction.cs src/SnapZones.Core/Drag/DragState.cs src/SnapZones.Core/Drag/WindowDragCoordinator.cs src/SnapZones.App/Services/ApplicationController.cs src/SnapZones.App/Overlays/OverlayManager.cs src/SnapZones.App/Overlays/MonitorOverlayWindow.xaml.cs src/SnapZones.Windows/Windows/IWindowService.cs src/SnapZones.Windows/Windows/WindowsWindowService.cs docs/README.md tests/SnapZones.Tests/Drag/WindowDragCoordinatorTests.cs tests/SnapZones.Tests/Drag/WindowsSafetyBoundaryTests.cs tests/SnapZones.Tests/Theme/OverlayPresentationTests.cs
git commit --only src/SnapZones.Core/Drag/DragAction.cs src/SnapZones.Core/Drag/DragState.cs src/SnapZones.Core/Drag/WindowDragCoordinator.cs src/SnapZones.App/Services/ApplicationController.cs src/SnapZones.App/Overlays/OverlayManager.cs src/SnapZones.App/Overlays/MonitorOverlayWindow.xaml.cs src/SnapZones.Windows/Windows/IWindowService.cs src/SnapZones.Windows/Windows/WindowsWindowService.cs docs/README.md tests/SnapZones.Tests/Drag/WindowDragCoordinatorTests.cs tests/SnapZones.Tests/Drag/WindowsSafetyBoundaryTests.cs tests/SnapZones.Tests/Theme/OverlayPresentationTests.cs -m "refactor: route dragging through part monitor commands"
~~~

---

### Task 6: Verify release safety and unchanged Windows maximise behaviour

**Files:**
- Verify only: scripts/verify.ps1
- Verify only: outputs/Sascha-Window-Zones-prototype/SaschaWindowZones.exe
- Verify only: outputs/sascha-window-zones-diagnostics.json

**Interfaces:**
- Consumes: completed Tasks 1 to 5.
- Produces: build, test, publish, diagnostics, DPI, and artefact evidence without changing production code.

- [ ] **Step 1: Run the complete repository verification**

~~~powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1
~~~

Expected: restore, all tests, Release build, self-contained win-x64 publish, root executable copy, diagnostics, DPI verification, and artefact checks all pass.

- [ ] **Step 2: Run the safe diagnostics explicitly**

~~~powershell
./SaschaWindowZones.exe --diagnostics
~~~

Expected: valid JSON with hookRegistered=false and settingsChanged=false; diagnostics do not activate snapping or change a Windows setting.

- [ ] **Step 3: Perform the Windows interaction acceptance**

On one real normal window:

1. Enable snapping explicitly.
2. Drag it to a Teilmonitor and verify the exact Teilmonitor bounds.
3. Click the normal maximise button and verify the whole physical monitor work area is used.
4. Restore the window and verify Win + Pfeil and Win + Z still use native Windows behaviour.
5. Trigger the emergency stop and verify overlays disappear and no further drag is handled.
6. Exit the application and restart it with snapping still disabled.

- [ ] **Step 4: Record evidence without committing generated artefacts unless already tracked**

Record the test count, publish path, diagnostic result, DPI result, and the six manual acceptance outcomes in the implementation handoff. Do not add logs, diagnostics JSON, published binaries, or user configuration to Git unless the repository already tracks that exact artefact.
