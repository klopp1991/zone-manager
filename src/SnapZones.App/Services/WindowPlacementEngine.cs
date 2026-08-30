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
    private readonly Action? sideEffectWaitObserver;
    private readonly object synchronization = new();
    private readonly object lifecycleSynchronization = new();
    private readonly object sideEffectSynchronization = new();
    private readonly Dictionary<nint, HandleState> handleStates = [];
    private readonly Dictionary<nint, long> handleEpochs = [];
    private readonly Dictionary<nint, WindowIdentity> processedIdentities = [];
    private readonly HashSet<nint> pendingShownHandles = [];
    private readonly HashSet<Task> operations = [];
    private readonly Queue<CatalogPublication> pendingCatalogPublications = [];

    private bool running;
    private bool emergencyStopped;
    private bool subscribed;
    private bool publishingCatalogs;
    private TaskCompletionSource? catalogPublicationCompletion;
    private TaskCompletionSource? activeSideEffectCompletion;
    private int activeSideEffectThreadId;
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
        TimeProvider? timeProvider = null,
        Action? sideEffectWaitObserver = null)
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
        this.sideEffectWaitObserver = sideEffectWaitObserver;
    }

    public WindowPlacementCatalog Catalog { get; private set; }

    public event Action<WindowPlacementCatalog>? CatalogChanged;

    public void Start()
    {
        lock (lifecycleSynchronization)
        {
            HandleState[] staleHandleStates = [];
            var started = false;
            ExecuteInvalidation(() =>
            {
                if (running || emergencyStopped)
                {
                    return;
                }

                staleHandleStates = handleStates.Values.ToArray();
                handleStates.Clear();
                handleEpochs.Clear();
                pendingShownHandles.Clear();
                processedIdentities.Clear();
                engineGeneration++;
                running = true;
                lifecycleHook.EventReceived += OnLifecycleEvent;
                lifecycleHook.EmergencyStopped += OnHookEmergencyStopped;
                subscribed = true;
                started = true;
            });

            if (!started)
            {
                return;
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
            StopCoreLocked(emergencyStop: true);
        }
    }

    private void StopCoreLocked(bool emergencyStop = false)
    {
        CancellationTokenSource[] cancellations = [];
        var removeSubscriptions = false;
        var stopped = false;
        ExecuteInvalidation(() =>
        {
            if (emergencyStop)
            {
                emergencyStopped = true;
            }

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
            processedIdentities.Clear();
            removeSubscriptions = subscribed;
            subscribed = false;
            stopped = true;
        });

        if (!stopped)
        {
            return;
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
        ExecuteInvalidation(() =>
        {
            if (running)
            {
                throw new InvalidOperationException("Der Platzierungskatalog kann nur im gestoppten Zustand ersetzt werden.");
            }

            Catalog = catalog;
            engineGeneration++;
            publish = QueueCatalogPublicationLocked(catalog, persist: false);
        });

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
                HandleState state;
                lock (synchronization)
                {
                    state = GetOrCreateHandleStateLocked(windowHandle);
                    context = CreateContextLocked(windowHandle, state);
                }

                var snapshot = windowService.Inspect(windowHandle, ownProcessId);
                if (snapshot?.Identity != identity)
                {
                    continue;
                }

                var bounds = PlacementGeometry.Resolve(entry, environment.Monitors, environment.Zones);
                TryPlaceIfCurrent(windowHandle, state, context, identity, bounds, entry.WasMaximized);

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
            await CaptureSnapshotAsync(snapshot, profileId, monitorStableId, zoneId, context, allowMissingCurrentWindow: false, cancellationToken).ConfigureAwait(false);
        }, propagateExceptions: false);
    }

    public async Task FlushAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            Task[] pending;
            Task? pendingPublication;
            lock (synchronization)
            {
                pending = operations.Where(operation => !operation.IsCompleted).ToArray();
                pendingPublication = publishingCatalogs ? catalogPublicationCompletion?.Task : null;
            }

            if (pending.Length != 0 || pendingPublication is not null)
            {
                var pendingWork = pendingPublication is null
                    ? pending
                    : pending.Append(pendingPublication).ToArray();
                await Task.WhenAll(pendingWork).WaitAsync(cancellationToken).ConfigureAwait(false);
                continue;
            }

            await saveCoordinator.FlushAsync(cancellationToken).ConfigureAwait(false);
            lock (synchronization)
            {
                if (!operations.Any(operation => !operation.IsCompleted) &&
                    !publishingCatalogs &&
                    pendingCatalogPublications.Count == 0)
                {
                    return;
                }
            }
        }
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
        HandleState state = null!;
        HandleState? previousState = null;
        OperationContext context = null!;
        WindowIdentity? processedIdentity = null;
        lock (synchronization)
        {
            if (!running || pendingShownHandles.Contains(windowHandle))
            {
                return;
            }

            if (processedIdentities.TryGetValue(windowHandle, out var knownIdentity))
            {
                processedIdentity = knownIdentity;
            }
        }

        PlacementWindowSnapshot? currentSnapshot = null;
        if (processedIdentity is not null)
        {
            currentSnapshot = windowService.Inspect(windowHandle, ownProcessId);
        }

        var scheduled = false;
        ExecuteInvalidation(() =>
        {
            if (!running || pendingShownHandles.Contains(windowHandle))
            {
                return;
            }

            if (processedIdentities.TryGetValue(windowHandle, out var currentProcessedIdentity))
            {
                if (currentSnapshot?.Identity == currentProcessedIdentity)
                {
                    return;
                }

                processedIdentities.Remove(windowHandle);
            }

            handleStates.TryGetValue(windowHandle, out previousState);
            state = CreateNextHandleStateLocked(windowHandle, lastSnapshot: null);
            pendingShownHandles.Add(windowHandle);
            context = CreateContextLocked(windowHandle, state);
            scheduled = true;
        });

        if (!scheduled)
        {
            return;
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
            MarkProcessed(context, snapshot.Identity);
            return;
        }

        if (resolution.Rule?.Action == WindowPlacementMode.Exclude)
        {
            MarkProcessed(context, snapshot.Identity);
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
                MarkProcessed(context, snapshot.Identity);
                return;
            }

            targetBounds = targetZone.Bounds;
        }
        else
        {
            var entry = FindCatalogEntry(snapshot.Identity);
            if (entry is null)
            {
                MarkProcessed(context, snapshot.Identity);
                return;
            }

            targetBounds = PlacementGeometry.Resolve(entry, environment.Monitors, environment.Zones);
            maximize = entry.WasMaximized;
        }

        cancellationToken.ThrowIfCancellationRequested();
        TryPlaceIfCurrent(windowHandle, state, context, snapshot.Identity, targetBounds, maximize);
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
        HandleState state = null!;
        HandleState previousState = null!;
        OperationContext context = null!;
        CancellationTokenSource? pendingCapture = null;
        var scheduled = false;
        ExecuteInvalidation(() =>
        {
            if (!running)
            {
                return;
            }

            previousState = GetOrCreateHandleStateLocked(windowHandle);
            pendingCapture = previousState.CaptureCancellation;
            state = CreateNextHandleStateLocked(windowHandle, previousState.LastSnapshot);
            context = CreateContextLocked(windowHandle, state);
            scheduled = true;
        });

        if (!scheduled)
        {
            return;
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
        var usesCachedSnapshot = false;
        if (snapshot is not null)
        {
            SetCachedSnapshot(windowHandle, state, snapshot);
        }
        else if (allowCachedSnapshot)
        {
            snapshot = state.LastSnapshot;
            usesCachedSnapshot = snapshot is not null;
        }

        if (snapshot is null ||
            snapshot.IsMinimized ||
            IsShownPending(windowHandle) ||
            IsCaptureSuppressed(windowHandle, state))
        {
            return;
        }

        await CaptureSnapshotAsync(snapshot, null, null, null, context, usesCachedSnapshot, cancellationToken).ConfigureAwait(false);
    }

    private Task CaptureSnapshotAsync(
        PlacementWindowSnapshot snapshot,
        Guid? explicitProfileId,
        string? explicitMonitorStableId,
        Guid? explicitZoneId,
        OperationContext context,
        bool allowMissingCurrentWindow,
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
        StoreEntry(entry, context, snapshot.Identity, allowMissingCurrentWindow);
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

    private void StoreEntry(
        WindowPlacementEntry entry,
        OperationContext context,
        WindowIdentity expectedIdentity,
        bool allowMissingCurrentWindow)
    {
        var currentSnapshot = windowService.Inspect(context.WindowHandle, ownProcessId);
        if (currentSnapshot is null && !allowMissingCurrentWindow ||
            currentSnapshot is not null &&
            (currentSnapshot.IsMinimized || currentSnapshot.Identity != expectedIdentity))
        {
            return;
        }

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
        catalogPublicationCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        return true;
    }

    private void PublishCatalogsInOrder()
    {
        while (true)
        {
            CatalogPublication? publication = null;
            TaskCompletionSource? completion = null;
            lock (synchronization)
            {
                if (pendingCatalogPublications.Count == 0)
                {
                    publishingCatalogs = false;
                    completion = catalogPublicationCompletion;
                    catalogPublicationCompletion = null;
                }
                else
                {
                    publication = pendingCatalogPublications.Dequeue();
                }
            }

            if (completion is not null)
            {
                completion.TrySetResult();
                return;
            }

            RaiseCatalogChanged(publication!.Catalog);
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

    private void TryPlaceIfCurrent(
        nint windowHandle,
        HandleState state,
        OperationContext context,
        WindowIdentity expectedIdentity,
        PixelRect targetBounds,
        bool maximize)
    {
        var currentSnapshot = windowService.Inspect(windowHandle, ownProcessId);
        if (currentSnapshot is null ||
            currentSnapshot.IsMinimized ||
            currentSnapshot.Identity != expectedIdentity)
        {
            return;
        }

        SetCachedSnapshot(windowHandle, state, currentSnapshot);
        TaskCompletionSource reservation;
        while (true)
        {
            Task? waitForActiveSideEffect = null;
            lock (sideEffectSynchronization)
            {
                if (activeSideEffectCompletion is null)
                {
                    lock (synchronization)
                    {
                        if (!IsContextValidLocked(context))
                        {
                            return;
                        }
                    }

                    reservation = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    activeSideEffectCompletion = reservation;
                    activeSideEffectThreadId = Environment.CurrentManagedThreadId;
                    break;
                }

                waitForActiveSideEffect = activeSideEffectCompletion.Task;
            }

            sideEffectWaitObserver?.Invoke();
            waitForActiveSideEffect.GetAwaiter().GetResult();
        }

        try
        {
            var revalidatedSnapshot = windowService.Inspect(windowHandle, ownProcessId);
            if (revalidatedSnapshot is null ||
                revalidatedSnapshot.IsMinimized ||
                revalidatedSnapshot.Identity != expectedIdentity)
            {
                return;
            }

            SetCachedSnapshot(windowHandle, state, revalidatedSnapshot);
            lock (synchronization)
            {
                if (!IsContextValidLocked(context))
                {
                    return;
                }
            }

            if (!windowService.TryPlace(windowHandle, targetBounds, maximize))
            {
                return;
            }

            lock (synchronization)
            {
                if (IsContextValidLocked(context))
                {
                    state.SuppressCaptureUntilUtc = timeProvider.GetUtcNow() + OwnPlacementSuppression;
                    processedIdentities[windowHandle] = expectedIdentity;
                }
            }
        }
        finally
        {
            lock (sideEffectSynchronization)
            {
                if (ReferenceEquals(activeSideEffectCompletion, reservation))
                {
                    activeSideEffectCompletion = null;
                    activeSideEffectThreadId = 0;
                }
            }

            reservation.TrySetResult();
        }
    }

    private void ExecuteInvalidation(Action action)
    {
        while (true)
        {
            Task? waitForActiveSideEffect = null;
            lock (sideEffectSynchronization)
            {
                if (activeSideEffectCompletion is null ||
                    activeSideEffectThreadId == Environment.CurrentManagedThreadId)
                {
                    lock (synchronization)
                    {
                        action();
                    }

                    return;
                }

                waitForActiveSideEffect = activeSideEffectCompletion.Task;
            }

            sideEffectWaitObserver?.Invoke();
            waitForActiveSideEffect.GetAwaiter().GetResult();
        }
    }

    private void MarkProcessed(OperationContext context, WindowIdentity identity)
    {
        lock (synchronization)
        {
            if (IsContextValidLocked(context))
            {
                processedIdentities[context.WindowHandle] = identity;
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
            processedIdentities.Remove(windowHandle);
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
