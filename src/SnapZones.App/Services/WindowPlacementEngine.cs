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
    private readonly object lifecycleSynchronization = new();
    private readonly Dictionary<nint, HandleState> handleStates = [];
    private readonly Dictionary<nint, long> handleEpochs = [];
    private readonly HashSet<nint> processedHandles = [];
    private readonly HashSet<nint> pendingShownHandles = [];
    private readonly HashSet<Task> operations = [];
    private readonly Queue<CatalogPublication> pendingCatalogPublications = [];

    private bool running;
    private bool emergencyStopped;
    private bool subscribed;
    private bool publishingCatalogs;
    private long catalogVersion;
    private long engineGeneration;

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
        lock (lifecycleSynchronization)
        {
            HandleState[] staleHandleStates;
            lock (synchronization)
            {
                if (running || emergencyStopped)
                {
                    return;
                }

                staleHandleStates = handleStates.Values.ToArray();
                handleStates.Clear();
                handleEpochs.Clear();
                pendingShownHandles.Clear();
                processedHandles.Clear();
                engineGeneration++;
                running = true;
                lifecycleHook.EventReceived += OnLifecycleEvent;
                lifecycleHook.EmergencyStopped += OnHookEmergencyStopped;
                subscribed = true;
            }

            foreach (var staleState in staleHandleStates)
            {
                Cancel(staleState.CaptureCancellation);
                Cancel(staleState.LifetimeCancellation);
            }

            try
            {
                lifecycleHook.Enable();
            }
            catch
            {
                StopCoreLocked();
                throw;
            }
        }
    }

    public void Stop()
    {
        lock (lifecycleSynchronization)
        {
            StopCoreLocked();
        }
    }

    public void EmergencyStop()
    {
        lock (lifecycleSynchronization)
        {
            lock (synchronization)
            {
                emergencyStopped = true;
            }

            StopCoreLocked();
        }
    }

    private void StopCoreLocked()
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
            engineGeneration++;
            cancellations = handleStates.Values
                .SelectMany(state => state.CaptureCancellation is null
                    ? new[] { state.LifetimeCancellation }
                    : new[] { state.LifetimeCancellation, state.CaptureCancellation })
                .Distinct()
                .ToArray();
            handleStates.Clear();
            handleEpochs.Clear();
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

    public void ReplaceCatalog(WindowPlacementCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        var publish = false;
        lock (synchronization)
        {
            if (running)
            {
                throw new InvalidOperationException("Der Platzierungskatalog kann nur im gestoppten Zustand ersetzt werden.");
            }

            Catalog = catalog;
            engineGeneration++;
            publish = QueueCatalogPublicationLocked(catalog, persist: false);
        }

        if (publish)
        {
            PublishCatalogsInOrder();
        }
    }

    public Task ApplyNowAsync(WindowIdentity identity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        return RunOperation(async () =>
        {
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
                OperationContext context;
                lock (synchronization)
                {
                    var state = GetOrCreateHandleStateLocked(windowHandle);
                    context = CreateContextLocked(windowHandle, state);
                }

                var snapshot = windowService.Inspect(windowHandle, ownProcessId);
                if (snapshot?.Identity != identity)
                {
                    continue;
                }

                var bounds = PlacementGeometry.Resolve(entry, environment.Monitors, environment.Zones);
                var currentSnapshot = windowService.Inspect(windowHandle, ownProcessId);
                if (currentSnapshot?.Identity != identity || currentSnapshot.IsMinimized)
                {
                    return;
                }

                lock (synchronization)
                {
                    if (!IsContextValidLocked(context))
                    {
                        return;
                    }
                }

                if (windowService.TryPlace(windowHandle, bounds, entry.WasMaximized))
                {
                    MarkOwnPlacement(context);
                }

                return;
            }

            await Task.CompletedTask;
        }, propagateExceptions: true);
    }

    public void Forget(WindowIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        var publish = false;
        lock (synchronization)
        {
            var retained = Catalog.Entries.Where(entry => entry.Identity != identity).ToArray();
            if (retained.Length != Catalog.Entries.Count)
            {
                var changedCatalog = new WindowPlacementCatalog(WindowPlacementCatalog.CurrentSchemaVersion, retained);
                Catalog = changedCatalog;
                publish = QueueCatalogPublicationLocked(changedCatalog, persist: true);
            }
        }

        if (publish)
        {
            PublishCatalogsInOrder();
        }
    }

    public void RememberExplicitZone(nint windowHandle, Guid profileId, string monitorStableId, Guid zoneId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(monitorStableId);
        CancellationToken cancellationToken;
        HandleState state;
        OperationContext context;
        lock (synchronization)
        {
            if (!running)
            {
                return;
            }

            state = GetOrCreateHandleStateLocked(windowHandle);
            cancellationToken = state.LifetimeCancellation.Token;
            context = CreateContextLocked(windowHandle, state);
        }

        _ = RunOperation(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = windowService.Inspect(windowHandle, ownProcessId);
            if (snapshot is null || snapshot.IsMinimized)
            {
                return;
            }

            SetCachedSnapshot(windowHandle, state, snapshot);
            await CaptureSnapshotAsync(snapshot, profileId, monitorStableId, zoneId, context, cancellationToken).ConfigureAwait(false);
        }, propagateExceptions: false);
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
        HandleState? previousState;
        OperationContext context;
        lock (synchronization)
        {
            if (!running || processedHandles.Contains(windowHandle))
            {
                return;
            }

            handleStates.TryGetValue(windowHandle, out previousState);
            state = CreateNextHandleStateLocked(windowHandle, lastSnapshot: null);
            pendingShownHandles.Add(windowHandle);
            context = CreateContextLocked(windowHandle, state);
        }

        if (previousState is not null)
        {
            Cancel(previousState.CaptureCancellation);
            Cancel(previousState.LifetimeCancellation);
        }
        _ = RunOperation(async () =>
        {
            try
            {
                await RestoreShownAsync(windowHandle, state, context, state.LifetimeCancellation.Token).ConfigureAwait(false);
            }
            finally
            {
                lock (synchronization)
                {
                    if (IsContextValidLocked(context))
                    {
                        pendingShownHandles.Remove(windowHandle);
                    }
                }
            }
        }, propagateExceptions: false);
    }

    private async Task RestoreShownAsync(
        nint windowHandle,
        HandleState state,
        OperationContext context,
        CancellationToken cancellationToken)
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
            MarkProcessed(context);
            return;
        }

        if (resolution.Rule?.Action == WindowPlacementMode.Exclude)
        {
            MarkProcessed(context);
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
                MarkProcessed(context);
                return;
            }

            targetBounds = targetZone.Bounds;
        }
        else
        {
            var entry = FindCatalogEntry(snapshot.Identity);
            if (entry is null)
            {
                MarkProcessed(context);
                return;
            }

            targetBounds = PlacementGeometry.Resolve(entry, environment.Monitors, environment.Zones);
            maximize = entry.WasMaximized;
        }

        var currentSnapshot = windowService.Inspect(windowHandle, ownProcessId);
        if (currentSnapshot is null ||
            currentSnapshot.IsMinimized ||
            currentSnapshot.Identity != snapshot.Identity)
        {
            return;
        }

        SetCachedSnapshot(windowHandle, state, currentSnapshot);
        cancellationToken.ThrowIfCancellationRequested();
        lock (synchronization)
        {
            if (!IsContextValidLocked(context))
            {
                return;
            }
        }

        if (windowService.TryPlace(windowHandle, targetBounds, maximize))
        {
            lock (synchronization)
            {
                if (IsContextValidLocked(context))
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
        OperationContext context;
        CancellationTokenSource captureCancellation;
        CancellationTokenSource? previousCapture;
        lock (synchronization)
        {
            if (!running)
            {
                return;
            }

            state = GetOrCreateHandleStateLocked(windowHandle);
            context = CreateContextLocked(windowHandle, state);
            previousCapture = state.CaptureCancellation;
            captureCancellation = CancellationTokenSource.CreateLinkedTokenSource(state.LifetimeCancellation.Token);
            state.CaptureCancellation = captureCancellation;
        }

        Cancel(previousCapture);

        _ = RunOperation(async () =>
        {
            var disposeCapture = false;
            try
            {
                await delay(CaptureDelay, captureCancellation.Token).ConfigureAwait(false);
                await CaptureWindowAsync(windowHandle, state, context, allowCachedSnapshot: false, captureCancellation.Token).ConfigureAwait(false);
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
                        disposeCapture = true;
                    }
                }

                if (disposeCapture)
                {
                    captureCancellation.Dispose();
                }
            }
        }, propagateExceptions: false);
    }

    private void ScheduleImmediateCapture(nint windowHandle, bool removeAfterCapture)
    {
        HandleState state;
        OperationContext context;
        CancellationTokenSource? pendingCapture;
        lock (synchronization)
        {
            if (!running)
            {
                return;
            }

            state = GetOrCreateHandleStateLocked(windowHandle);
            context = CreateContextLocked(windowHandle, state);
            pendingCapture = state.CaptureCancellation;
            state.CaptureCancellation = null;
        }

        Cancel(pendingCapture);

        _ = RunOperation(async () =>
        {
            try
            {
                await CaptureWindowAsync(
                    windowHandle,
                    state,
                    context,
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
        }, propagateExceptions: false);
    }

    private void ScheduleDestroyedCapture(nint windowHandle)
    {
        HandleState state;
        HandleState previousState;
        OperationContext context;
        CancellationTokenSource? pendingCapture;
        lock (synchronization)
        {
            if (!running)
            {
                return;
            }

            previousState = GetOrCreateHandleStateLocked(windowHandle);
            pendingCapture = previousState.CaptureCancellation;
            state = CreateNextHandleStateLocked(windowHandle, previousState.LastSnapshot);
            context = CreateContextLocked(windowHandle, state);
        }

        Cancel(pendingCapture);
        Cancel(previousState.LifetimeCancellation);

        _ = RunOperation(async () =>
        {
            try
            {
                await CaptureWindowAsync(
                    windowHandle,
                    state,
                    context,
                    allowCachedSnapshot: true,
                    state.LifetimeCancellation.Token).ConfigureAwait(false);
            }
            finally
            {
                RemoveHandle(windowHandle, state);
            }
        }, propagateExceptions: false);
    }

    private async Task CaptureWindowAsync(
        nint windowHandle,
        HandleState state,
        OperationContext context,
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

        await CaptureSnapshotAsync(snapshot, null, null, null, context, cancellationToken).ConfigureAwait(false);
    }

    private Task CaptureSnapshotAsync(
        PlacementWindowSnapshot snapshot,
        Guid? explicitProfileId,
        string? explicitMonitorStableId,
        Guid? explicitZoneId,
        OperationContext context,
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
        StoreEntry(entry, context);
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

    private void StoreEntry(WindowPlacementEntry entry, OperationContext context)
    {
        var publish = false;
        lock (synchronization)
        {
            if (!IsContextValidLocked(context))
            {
                return;
            }

            var changedCatalog = new WindowPlacementCatalog(
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
            publish = QueueCatalogPublicationLocked(changedCatalog, persist: true);
        }

        if (publish)
        {
            PublishCatalogsInOrder();
        }
    }

    private bool QueueCatalogPublicationLocked(WindowPlacementCatalog catalog, bool persist)
    {
        pendingCatalogPublications.Enqueue(new CatalogPublication(++catalogVersion, catalog, persist));
        if (publishingCatalogs)
        {
            return false;
        }

        publishingCatalogs = true;
        return true;
    }

    private void PublishCatalogsInOrder()
    {
        while (true)
        {
            CatalogPublication publication;
            lock (synchronization)
            {
                if (pendingCatalogPublications.Count == 0)
                {
                    publishingCatalogs = false;
                    return;
                }

                publication = pendingCatalogPublications.Dequeue();
            }

            RaiseCatalogChanged(publication.Catalog);
            if (publication.Persist)
            {
                saveCoordinator.RequestSave(publication.Catalog);
            }
        }
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

    private void MarkProcessed(OperationContext context)
    {
        lock (synchronization)
        {
            if (IsContextValidLocked(context))
            {
                processedHandles.Add(context.WindowHandle);
            }
        }
    }

    private void MarkOwnPlacement(OperationContext context)
    {
        lock (synchronization)
        {
            if (IsContextValidLocked(context) && handleStates.TryGetValue(context.WindowHandle, out var state))
            {
                state.SuppressCaptureUntilUtc = timeProvider.GetUtcNow() + OwnPlacementSuppression;
            }
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
        if (snapshot.IsMinimized)
        {
            return;
        }

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
            state = CreateNextHandleStateLocked(windowHandle, lastSnapshot: null);
        }

        return state;
    }

    private HandleState CreateNextHandleStateLocked(nint windowHandle, PlacementWindowSnapshot? lastSnapshot)
    {
        var epoch = handleEpochs.TryGetValue(windowHandle, out var previousEpoch)
            ? previousEpoch + 1
            : 1;
        handleEpochs[windowHandle] = epoch;
        var state = new HandleState(
            epoch,
            new CancellationTokenSource())
        {
            LastSnapshot = lastSnapshot
        };
        handleStates[windowHandle] = state;
        return state;
    }

    private OperationContext CreateContextLocked(nint windowHandle, HandleState state) =>
        new(engineGeneration, windowHandle, state.Epoch);

    private bool IsContextValidLocked(OperationContext context) =>
        context.Generation == engineGeneration &&
        handleEpochs.TryGetValue(context.WindowHandle, out var epoch) &&
        epoch == context.HandleEpoch &&
        handleStates.TryGetValue(context.WindowHandle, out var state) &&
        state.Epoch == context.HandleEpoch;

    private void RemoveHandle(nint windowHandle, HandleState state)
    {
        var removed = false;
        lock (synchronization)
        {
            if (!handleStates.TryGetValue(windowHandle, out var currentState) || !ReferenceEquals(currentState, state))
            {
                return;
            }

            handleStates.Remove(windowHandle);
            pendingShownHandles.Remove(windowHandle);
            processedHandles.Remove(windowHandle);
            removed = true;
        }

        if (removed)
        {
            Cancel(state.CaptureCancellation);
            Cancel(state.LifetimeCancellation);
        }
    }

    private static void Cancel(CancellationTokenSource? cancellation)
    {
        cancellation?.Cancel();
    }

    private Task RunOperation(Func<Task> operation, bool propagateExceptions)
    {
        var trackingCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callerCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (synchronization)
        {
            operations.Add(trackingCompletion.Task);
        }

        async Task ExecuteAsync()
        {
            Exception? failure = null;
            try
            {
                await operation().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                failure = exception;
                if (!propagateExceptions && exception is not OperationCanceledException)
                {
                    log($"Fensterplatzierung fehlgeschlagen: {exception.Message}");
                }
            }
            finally
            {
                lock (synchronization)
                {
                    operations.Remove(trackingCompletion.Task);
                }

                trackingCompletion.TrySetResult();
                if (failure is OperationCanceledException canceled)
                {
                    callerCompletion.TrySetCanceled(canceled.CancellationToken);
                }
                else if (failure is not null && propagateExceptions)
                {
                    callerCompletion.TrySetException(failure);
                }
                else
                {
                    callerCompletion.TrySetResult();
                }
            }
        }

        _ = ExecuteAsync();
        return propagateExceptions ? callerCompletion.Task : trackingCompletion.Task;
    }

    private sealed class HandleState(long epoch, CancellationTokenSource lifetimeCancellation)
    {
        public long Epoch { get; } = epoch;
        public CancellationTokenSource LifetimeCancellation { get; } = lifetimeCancellation;
        public CancellationTokenSource? CaptureCancellation { get; set; }
        public PlacementWindowSnapshot? LastSnapshot { get; set; }
        public DateTimeOffset SuppressCaptureUntilUtc { get; set; }
    }

    private sealed record CatalogPublication(long Version, WindowPlacementCatalog Catalog, bool Persist);
    private sealed record OperationContext(long Generation, nint WindowHandle, long HandleEpoch);
}
