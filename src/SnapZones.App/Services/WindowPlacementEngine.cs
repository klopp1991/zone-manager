using SnapZones.Core.Geometry;
using SnapZones.Core.Placement;
using SnapZones.Windows.Hooks;
using SnapZones.Windows.Windows;

namespace SnapZones.App.Services;

public sealed class WindowPlacementEngine : IWindowPlacementEngine
{
    private static readonly TimeSpan[] InspectionDelays =
    [
        TimeSpan.FromMilliseconds(100),
        TimeSpan.FromMilliseconds(300),
        TimeSpan.FromMilliseconds(700)
    ];

    private static readonly TimeSpan CaptureDelay = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan OwnPlacementSuppression = TimeSpan.FromMilliseconds(750);
    private const int MaximumCatalogEntries = 500;

    private readonly IWindowLifecycleHook lifecycleHook;
    private readonly IPlacementWindowService windowService;
    private readonly WindowPlacementSaveCoordinator saveCoordinator;
    private readonly Func<PlacementEnvironment> environmentFactory;
    private readonly int ownProcessId;
    private readonly Action<string> log;
    private readonly Func<TimeSpan, CancellationToken, Task> delay;
    private readonly TimeProvider timeProvider;
    private readonly object synchronization = new();
    private readonly Dictionary<nint, HandleState> handleStates = [];
    private readonly HashSet<nint> processedHandles = [];
    private readonly HashSet<nint> pendingShownHandles = [];
    private readonly HashSet<Task> operations = [];

    private CancellationTokenSource runCancellation = new();
    private bool running;
    private bool emergencyStopped;
    private bool subscribed;

    public WindowPlacementEngine(
        IWindowLifecycleHook lifecycleHook,
        IPlacementWindowService windowService,
        WindowPlacementSaveCoordinator saveCoordinator,
        WindowPlacementCatalog initialCatalog,
        Func<PlacementEnvironment> environmentFactory,
        int ownProcessId,
        Action<string> log,
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        TimeProvider? timeProvider = null)
    {
        this.lifecycleHook = lifecycleHook ?? throw new ArgumentNullException(nameof(lifecycleHook));
        this.windowService = windowService ?? throw new ArgumentNullException(nameof(windowService));
        this.saveCoordinator = saveCoordinator ?? throw new ArgumentNullException(nameof(saveCoordinator));
        Catalog = initialCatalog ?? throw new ArgumentNullException(nameof(initialCatalog));
        this.environmentFactory = environmentFactory ?? throw new ArgumentNullException(nameof(environmentFactory));
        this.ownProcessId = ownProcessId;
        this.log = log ?? throw new ArgumentNullException(nameof(log));
        this.delay = delay ?? Task.Delay;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public WindowPlacementCatalog Catalog { get; private set; }

    public event Action<WindowPlacementCatalog>? CatalogChanged;

    public void Start()
    {
        lock (synchronization)
        {
            if (running || emergencyStopped)
            {
                return;
            }

            runCancellation.Dispose();
            runCancellation = new CancellationTokenSource();
            running = true;
            lifecycleHook.EventReceived += OnLifecycleEvent;
            lifecycleHook.EmergencyStopped += OnHookEmergencyStopped;
            subscribed = true;
        }

        try
        {
            lifecycleHook.Enable();
        }
        catch
        {
            Stop();
            throw;
        }
    }

    public void Stop()
    {
        CancellationTokenSource[] cancellations;
        var removeSubscriptions = false;
        lock (synchronization)
        {
            if (!running && !subscribed)
            {
                return;
            }

            running = false;
            cancellations = handleStates.Values
                .SelectMany(state => state.CaptureCancellation is null
                    ? new[] { state.LifetimeCancellation }
                    : new[] { state.LifetimeCancellation, state.CaptureCancellation })
                .Prepend(runCancellation)
                .Distinct()
                .ToArray();
            handleStates.Clear();
            pendingShownHandles.Clear();
            processedHandles.Clear();
            removeSubscriptions = subscribed;
            subscribed = false;
        }

        foreach (var cancellation in cancellations)
        {
            cancellation.Cancel();
        }

        if (removeSubscriptions)
        {
            lifecycleHook.EventReceived -= OnLifecycleEvent;
            lifecycleHook.EmergencyStopped -= OnHookEmergencyStopped;
        }

        lifecycleHook.Disable();
    }

    public void EmergencyStop()
    {
        lock (synchronization)
        {
            emergencyStopped = true;
        }

        Stop();
    }

    public void ReplaceCatalog(WindowPlacementCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        lock (synchronization)
        {
            if (running)
            {
                throw new InvalidOperationException("Der Platzierungskatalog kann nur im gestoppten Zustand ersetzt werden.");
            }

            Catalog = catalog;
        }

        RaiseCatalogChanged(catalog);
    }

    public async Task ApplyNowAsync(WindowIdentity identity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        cancellationToken.ThrowIfCancellationRequested();
        var entry = FindCatalogEntry(identity);
        if (entry is null)
        {
            return;
        }

        var environment = environmentFactory();
        foreach (var windowHandle in windowService.EnumerateEligibleWindows(ownProcessId))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = windowService.Inspect(windowHandle, ownProcessId);
            if (snapshot?.Identity != identity)
            {
                continue;
            }

            var bounds = PlacementGeometry.Resolve(entry, environment.Monitors, environment.Zones);
            if (windowService.TryPlace(windowHandle, bounds, entry.WasMaximized))
            {
                MarkOwnPlacement(windowHandle);
            }

            return;
        }

        await Task.CompletedTask;
    }

    public void Forget(WindowIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        WindowPlacementCatalog? changedCatalog = null;
        lock (synchronization)
        {
            var retained = Catalog.Entries.Where(entry => entry.Identity != identity).ToArray();
            if (retained.Length != Catalog.Entries.Count)
            {
                changedCatalog = new WindowPlacementCatalog(WindowPlacementCatalog.CurrentSchemaVersion, retained);
                Catalog = changedCatalog;
            }
        }

        if (changedCatalog is not null)
        {
            PersistCatalogChange(changedCatalog);
        }
    }

    public void RememberExplicitZone(nint windowHandle, Guid profileId, string monitorStableId, Guid zoneId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(monitorStableId);
        CancellationToken cancellationToken;
        HandleState state;
        lock (synchronization)
        {
            if (!running)
            {
                return;
            }

            state = GetOrCreateHandleStateLocked(windowHandle);
            cancellationToken = state.LifetimeCancellation.Token;
        }

        RunOperation(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = windowService.Inspect(windowHandle, ownProcessId);
            if (snapshot is null || snapshot.IsMinimized)
            {
                return;
            }

            SetCachedSnapshot(windowHandle, state, snapshot);
            await CaptureSnapshotAsync(snapshot, profileId, monitorStableId, zoneId, cancellationToken).ConfigureAwait(false);
        });
    }

    public async Task FlushAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            Task[] pending;
            lock (synchronization)
            {
                pending = operations.Where(operation => !operation.IsCompleted).ToArray();
            }

            if (pending.Length == 0)
            {
                break;
            }

            await Task.WhenAll(pending).WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        await saveCoordinator.FlushAsync(cancellationToken).ConfigureAwait(false);
    }

    private void OnLifecycleEvent(WindowLifecycleEvent lifecycleEvent)
    {
        switch (lifecycleEvent.Kind)
        {
            case WindowLifecycleEventKind.Shown:
                ScheduleShown(lifecycleEvent.WindowHandle);
                break;
            case WindowLifecycleEventKind.LocationChanged:
            case WindowLifecycleEventKind.MinimizeEnded:
                ScheduleDelayedCapture(lifecycleEvent.WindowHandle);
                break;
            case WindowLifecycleEventKind.MoveSizeEnded:
            case WindowLifecycleEventKind.Hidden:
                ScheduleImmediateCapture(lifecycleEvent.WindowHandle, removeAfterCapture: false);
                break;
            case WindowLifecycleEventKind.Destroyed:
                ScheduleDestroyedCapture(lifecycleEvent.WindowHandle);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(lifecycleEvent));
        }
    }

    private void OnHookEmergencyStopped(string message)
    {
        log(message);
        EmergencyStop();
    }

    private void ScheduleShown(nint windowHandle)
    {
        HandleState state;
        lock (synchronization)
        {
            if (!running || processedHandles.Contains(windowHandle) || !pendingShownHandles.Add(windowHandle))
            {
                return;
            }

            state = GetOrCreateHandleStateLocked(windowHandle);
        }

        RunOperation(async () =>
        {
            try
            {
                await RestoreShownAsync(windowHandle, state, state.LifetimeCancellation.Token).ConfigureAwait(false);
            }
            finally
            {
                lock (synchronization)
                {
                    pendingShownHandles.Remove(windowHandle);
                }
            }
        });
    }

    private async Task RestoreShownAsync(nint windowHandle, HandleState state, CancellationToken cancellationToken)
    {
        PlacementWindowSnapshot? snapshot = null;
        foreach (var inspectionDelay in InspectionDelays)
        {
            await delay(inspectionDelay, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            snapshot = windowService.Inspect(windowHandle, ownProcessId);
            if (snapshot is not null)
            {
                break;
            }
        }

        if (snapshot is null)
        {
            return;
        }

        SetCachedSnapshot(windowHandle, state, snapshot);
        var environment = environmentFactory();
        if (!environment.Configuration.Settings.RestoreWindowPlacementEnabled)
        {
            return;
        }

        var resolution = PlacementRuleResolver.Resolve(
            snapshot.Identity,
            snapshot.Title,
            environment.Configuration.Settings.EffectiveWindowPlacementRules);
        if (resolution.HasConflict)
        {
            log($"Regelkonflikt für {snapshot.Identity.ApplicationKey}.");
            MarkProcessed(windowHandle);
            return;
        }

        if (resolution.Rule?.Action == WindowPlacementMode.Exclude)
        {
            MarkProcessed(windowHandle);
            return;
        }

        PixelRect targetBounds;
        var maximize = false;
        if (resolution.Rule?.Action == WindowPlacementMode.FixedZone)
        {
            var targetZone = ResolveFixedZone(resolution.Rule, environment);
            if (targetZone is null)
            {
                log($"Die Zielzone für {snapshot.Identity.ApplicationKey} ist nicht verfügbar.");
                MarkProcessed(windowHandle);
                return;
            }

            targetBounds = targetZone.Bounds;
        }
        else
        {
            var entry = FindCatalogEntry(snapshot.Identity);
            if (entry is null)
            {
                MarkProcessed(windowHandle);
                return;
            }

            targetBounds = PlacementGeometry.Resolve(entry, environment.Monitors, environment.Zones);
            maximize = entry.WasMaximized;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (windowService.TryPlace(windowHandle, targetBounds, maximize))
        {
            lock (synchronization)
            {
                if (handleStates.TryGetValue(windowHandle, out var currentState) && ReferenceEquals(currentState, state))
                {
                    state.SuppressCaptureUntilUtc = timeProvider.GetUtcNow() + OwnPlacementSuppression;
                    processedHandles.Add(windowHandle);
                }
            }
        }
    }

    private void ScheduleDelayedCapture(nint windowHandle)
    {
        HandleState state;
        CancellationTokenSource captureCancellation;
        CancellationTokenSource? previousCapture;
        lock (synchronization)
        {
            if (!running)
            {
                return;
            }

            state = GetOrCreateHandleStateLocked(windowHandle);
            previousCapture = state.CaptureCancellation;
            captureCancellation = CancellationTokenSource.CreateLinkedTokenSource(state.LifetimeCancellation.Token);
            state.CaptureCancellation = captureCancellation;
        }

        previousCapture?.Cancel();

        RunOperation(async () =>
        {
            try
            {
                await delay(CaptureDelay, captureCancellation.Token).ConfigureAwait(false);
                await CaptureWindowAsync(windowHandle, state, allowCachedSnapshot: false, captureCancellation.Token).ConfigureAwait(false);
            }
            finally
            {
                lock (synchronization)
                {
                    if (handleStates.TryGetValue(windowHandle, out var currentState) &&
                        ReferenceEquals(currentState, state) &&
                        ReferenceEquals(state.CaptureCancellation, captureCancellation))
                    {
                        state.CaptureCancellation = null;
                    }
                }

                captureCancellation.Dispose();
            }
        });
    }

    private void ScheduleImmediateCapture(nint windowHandle, bool removeAfterCapture)
    {
        HandleState state;
        CancellationTokenSource? pendingCapture;
        lock (synchronization)
        {
            if (!running)
            {
                return;
            }

            state = GetOrCreateHandleStateLocked(windowHandle);
            pendingCapture = state.CaptureCancellation;
        }

        pendingCapture?.Cancel();

        RunOperation(async () =>
        {
            try
            {
                await CaptureWindowAsync(
                    windowHandle,
                    state,
                    allowCachedSnapshot: true,
                    state.LifetimeCancellation.Token).ConfigureAwait(false);
            }
            finally
            {
                if (removeAfterCapture)
                {
                    RemoveHandle(windowHandle, state);
                }
            }
        });
    }

    private void ScheduleDestroyedCapture(nint windowHandle)
    {
        HandleState state;
        CancellationToken runToken;
        CancellationTokenSource? pendingCapture;
        lock (synchronization)
        {
            if (!running)
            {
                return;
            }

            state = GetOrCreateHandleStateLocked(windowHandle);
            pendingCapture = state.CaptureCancellation;
            runToken = runCancellation.Token;
        }

        pendingCapture?.Cancel();
        state.LifetimeCancellation.Cancel();

        RunOperation(async () =>
        {
            try
            {
                await CaptureWindowAsync(windowHandle, state, allowCachedSnapshot: true, runToken).ConfigureAwait(false);
            }
            finally
            {
                RemoveHandle(windowHandle, state);
            }
        });
    }

    private async Task CaptureWindowAsync(
        nint windowHandle,
        HandleState state,
        bool allowCachedSnapshot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = windowService.Inspect(windowHandle, ownProcessId);
        if (snapshot is not null)
        {
            SetCachedSnapshot(windowHandle, state, snapshot);
        }
        else if (allowCachedSnapshot)
        {
            snapshot = state.LastSnapshot;
        }

        if (snapshot is null ||
            snapshot.IsMinimized ||
            IsShownPending(windowHandle) ||
            IsCaptureSuppressed(windowHandle, state))
        {
            return;
        }

        await CaptureSnapshotAsync(snapshot, null, null, null, cancellationToken).ConfigureAwait(false);
    }

    private Task CaptureSnapshotAsync(
        PlacementWindowSnapshot snapshot,
        Guid? explicitProfileId,
        string? explicitMonitorStableId,
        Guid? explicitZoneId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var environment = environmentFactory();
        if (!environment.Configuration.Settings.RestoreWindowPlacementEnabled)
        {
            return Task.CompletedTask;
        }

        var resolution = PlacementRuleResolver.Resolve(
            snapshot.Identity,
            snapshot.Title,
            environment.Configuration.Settings.EffectiveWindowPlacementRules);
        if (resolution.HasConflict)
        {
            log($"Regelkonflikt für {snapshot.Identity.ApplicationKey}.");
            return Task.CompletedTask;
        }

        if (resolution.Rule?.Action == WindowPlacementMode.Exclude)
        {
            return Task.CompletedTask;
        }

        var monitor = explicitMonitorStableId is null
            ? FindBestMonitor(snapshot.NormalBounds, environment.Monitors)
            : environment.Monitors.FirstOrDefault(item => item.StableId == explicitMonitorStableId);
        if (monitor is null)
        {
            return Task.CompletedTask;
        }

        Guid? zoneId;
        if (explicitZoneId is not null)
        {
            var targetExists = explicitProfileId is not null &&
                environment.Configuration.Profiles.Any(profile => profile.Id == explicitProfileId) &&
                environment.Zones.Any(zone =>
                    zone.ProfileId == explicitProfileId &&
                    zone.MonitorStableId == monitor.StableId &&
                    zone.ZoneId == explicitZoneId);
            if (!targetExists)
            {
                log($"Die Zielzone für {snapshot.Identity.ApplicationKey} ist nicht verfügbar.");
                return Task.CompletedTask;
            }

            zoneId = explicitZoneId;
        }
        else
        {
            var zones = environment.Zones
                .Where(zone =>
                    zone.ProfileId == environment.Configuration.Settings.ActiveProfileId &&
                    zone.MonitorStableId == monitor.StableId)
                .ToArray();
            zoneId = PlacementGeometry.ClassifyZone(snapshot.NormalBounds, zones);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var entry = new WindowPlacementEntry(
            snapshot.Identity,
            monitor.StableId,
            zoneId,
            monitor.WorkArea,
            snapshot.NormalBounds,
            PlacementGeometry.Normalize(snapshot.NormalBounds, monitor.WorkArea),
            snapshot.IsMaximized,
            timeProvider.GetUtcNow());
        StoreEntry(entry);
        return Task.CompletedTask;
    }

    private static PlacementZoneTarget? ResolveFixedZone(WindowPlacementRule rule, PlacementEnvironment environment)
    {
        if (rule.ProfileId is not Guid profileId ||
            rule.ZoneId is not Guid zoneId ||
            string.IsNullOrWhiteSpace(rule.MonitorStableId) ||
            !environment.Configuration.Profiles.Any(profile => profile.Id == profileId) ||
            !environment.Monitors.Any(monitor => monitor.StableId == rule.MonitorStableId))
        {
            return null;
        }

        return environment.Zones.FirstOrDefault(zone =>
            zone.ProfileId == profileId &&
            zone.MonitorStableId == rule.MonitorStableId &&
            zone.ZoneId == zoneId);
    }

    private static PlacementMonitorTarget? FindBestMonitor(
        PixelRect bounds,
        IReadOnlyList<PlacementMonitorTarget> monitors)
    {
        PlacementMonitorTarget? best = null;
        long bestOverlap = 0;
        foreach (var monitor in monitors)
        {
            var right = Math.Min((long)bounds.X + bounds.Width, (long)monitor.WorkArea.X + monitor.WorkArea.Width);
            var bottom = Math.Min((long)bounds.Y + bounds.Height, (long)monitor.WorkArea.Y + monitor.WorkArea.Height);
            var left = Math.Max(bounds.X, monitor.WorkArea.X);
            var top = Math.Max(bounds.Y, monitor.WorkArea.Y);
            var overlap = Math.Max(0, right - left) * Math.Max(0, bottom - top);
            if (overlap > bestOverlap)
            {
                best = monitor;
                bestOverlap = overlap;
            }
        }

        return best ?? monitors.FirstOrDefault(monitor => monitor.IsPrimary) ?? monitors.FirstOrDefault();
    }

    private void StoreEntry(WindowPlacementEntry entry)
    {
        WindowPlacementCatalog changedCatalog;
        lock (synchronization)
        {
            changedCatalog = new WindowPlacementCatalog(
                WindowPlacementCatalog.CurrentSchemaVersion,
                Catalog.Entries
                    .Where(existing => existing.Identity != entry.Identity)
                    .Append(entry)
                    .OrderByDescending(existing => existing.LastUpdatedUtc)
                    .GroupBy(existing => existing.Identity)
                    .Select(group => group.First())
                    .Take(MaximumCatalogEntries)
                    .ToArray());
            Catalog = changedCatalog;
        }

        PersistCatalogChange(changedCatalog);
    }

    private void PersistCatalogChange(WindowPlacementCatalog catalog)
    {
        RaiseCatalogChanged(catalog);
        saveCoordinator.RequestSave(catalog);
    }

    private void RaiseCatalogChanged(WindowPlacementCatalog catalog)
    {
        try
        {
            CatalogChanged?.Invoke(catalog);
        }
        catch (Exception exception)
        {
            log($"Katalogänderung konnte nicht gemeldet werden: {exception.Message}");
        }
    }

    private WindowPlacementEntry? FindCatalogEntry(WindowIdentity identity)
    {
        lock (synchronization)
        {
            return Catalog.Entries.FirstOrDefault(entry => entry.Identity == identity);
        }
    }

    private void MarkProcessed(nint windowHandle)
    {
        lock (synchronization)
        {
            processedHandles.Add(windowHandle);
        }
    }

    private void MarkOwnPlacement(nint windowHandle)
    {
        lock (synchronization)
        {
            var state = GetOrCreateHandleStateLocked(windowHandle);
            state.SuppressCaptureUntilUtc = timeProvider.GetUtcNow() + OwnPlacementSuppression;
        }
    }

    private bool IsCaptureSuppressed(nint windowHandle, HandleState state)
    {
        lock (synchronization)
        {
            return handleStates.TryGetValue(windowHandle, out var currentState) &&
                ReferenceEquals(currentState, state) &&
                timeProvider.GetUtcNow() < state.SuppressCaptureUntilUtc;
        }
    }

    private bool IsShownPending(nint windowHandle)
    {
        lock (synchronization)
        {
            return pendingShownHandles.Contains(windowHandle);
        }
    }

    private void SetCachedSnapshot(nint windowHandle, HandleState state, PlacementWindowSnapshot snapshot)
    {
        lock (synchronization)
        {
            if (handleStates.TryGetValue(windowHandle, out var currentState) && ReferenceEquals(currentState, state))
            {
                state.LastSnapshot = snapshot;
            }
        }
    }

    private HandleState GetOrCreateHandleStateLocked(nint windowHandle)
    {
        if (!handleStates.TryGetValue(windowHandle, out var state))
        {
            state = new HandleState(CancellationTokenSource.CreateLinkedTokenSource(runCancellation.Token));
            handleStates.Add(windowHandle, state);
        }

        return state;
    }

    private void RemoveHandle(nint windowHandle, HandleState state)
    {
        lock (synchronization)
        {
            if (!handleStates.TryGetValue(windowHandle, out var currentState) || !ReferenceEquals(currentState, state))
            {
                return;
            }

            handleStates.Remove(windowHandle);
            pendingShownHandles.Remove(windowHandle);
            processedHandles.Remove(windowHandle);
        }

        state.CaptureCancellation?.Cancel();
        state.CaptureCancellation?.Dispose();
        state.LifetimeCancellation.Dispose();
    }

    private void RunOperation(Func<Task> operation)
    {
        async Task ExecuteAsync()
        {
            try
            {
                await operation().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                log($"Fensterplatzierung fehlgeschlagen: {exception.Message}");
            }
        }

        var task = ExecuteAsync();
        lock (synchronization)
        {
            if (!task.IsCompleted)
            {
                operations.Add(task);
            }
        }

        if (!task.IsCompleted)
        {
            _ = task.ContinueWith(
                completedTask =>
                {
                    lock (synchronization)
                    {
                        operations.Remove(completedTask);
                    }
                },
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }
    }

    private sealed class HandleState(CancellationTokenSource lifetimeCancellation)
    {
        public CancellationTokenSource LifetimeCancellation { get; } = lifetimeCancellation;
        public CancellationTokenSource? CaptureCancellation { get; set; }
        public PlacementWindowSnapshot? LastSnapshot { get; set; }
        public DateTimeOffset SuppressCaptureUntilUtc { get; set; }
    }
}
