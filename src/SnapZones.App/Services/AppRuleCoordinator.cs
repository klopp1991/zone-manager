using SnapZones.Core.AppRules;
using SnapZones.Core.Geometry;
using SnapZones.Core.Layouts;
using SnapZones.Core.Models;
using SnapZones.Core.Monitors;
using SnapZones.Core.PartMonitors;
using SnapZones.Windows.Windows;

namespace SnapZones.App.Services;

public enum AppRuleExecutionStatus
{
    NoMatch,
    Applied,
    CandidateUnavailable,
    TargetMissing,
    WindowsRejected,
    Cancelled,
    Excluded
}

public sealed record AppRuleExecutionResult(AppRuleExecutionStatus Status, Guid? RuleId = null);

public interface IAppRuleWindowGateway
{
    WindowRuleCandidate? Inspect(nint windowHandle);
    IReadOnlyList<WindowRuleCandidate> GetCandidates();
    bool TrySnap(nint windowHandle, PixelRect bounds);

    /// <summary>Wie <see cref="TrySnap"/>, mit gemessenem Ergebnis und Begruendung.</summary>
    PlacementOutcome Snap(nint windowHandle, PixelRect bounds) =>
        TrySnap(windowHandle, bounds)
            ? PlacementOutcome.Success()
            : PlacementOutcome.Rejected("Windows hat die Platzierung abgelehnt.");
}

public sealed class WindowServiceAppRuleGateway(IWindowService windowService, int ownProcessId)
    : IAppRuleWindowGateway
{
    public WindowRuleCandidate? Inspect(nint windowHandle) =>
        windowService.InspectRuleCandidate(windowHandle, ownProcessId);

    public IReadOnlyList<WindowRuleCandidate> GetCandidates() =>
        windowService.GetRuleCandidates(ownProcessId);

    public bool TrySnap(nint windowHandle, PixelRect bounds) =>
        windowService.TrySnap(windowHandle, bounds);

    public PlacementOutcome Snap(nint windowHandle, PixelRect bounds) =>
        windowService.Snap(windowHandle, bounds);
}

public sealed class AppRuleCoordinator : IDisposable
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(250);
    private readonly Func<SnapConfiguration> configurationProvider;
    private readonly IReadOnlyList<LiveMonitor> monitors;
    private readonly IAppRuleWindowGateway windowGateway;
    private readonly Func<TimeSpan, CancellationToken, Task> delay;
    private readonly Action<string>? reportStatus;
    private readonly SemaphoreSlim executor = new(1, 1);
    private readonly object cancellationLock = new();
    private CancellationTokenSource pending = new();
    private bool disposed;

    public AppRuleCoordinator(
        Func<SnapConfiguration> configurationProvider,
        IReadOnlyList<LiveMonitor> monitors,
        IAppRuleWindowGateway windowGateway,
        Func<TimeSpan, CancellationToken, Task>? delay = null,
        Action<string>? reportStatus = null)
    {
        this.configurationProvider = configurationProvider ?? throw new ArgumentNullException(nameof(configurationProvider));
        this.monitors = monitors ?? throw new ArgumentNullException(nameof(monitors));
        this.windowGateway = windowGateway ?? throw new ArgumentNullException(nameof(windowGateway));
        this.delay = delay ?? Task.Delay;
        this.reportStatus = reportStatus;
    }

    public async Task<AppRuleExecutionResult> HandleAsync(AppRuleEvent eventType, nint windowHandle)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var token = CurrentToken();
        try
        {
            var initialCandidate = windowGateway.Inspect(windowHandle);
            if (initialCandidate is null)
            {
                return new AppRuleExecutionResult(AppRuleExecutionStatus.CandidateUnavailable);
            }

            var configuration = configurationProvider();
            if (AppExclusionMatcher.IsExcluded(configuration.AppExclusions, initialCandidate.Identity))
            {
                return new AppRuleExecutionResult(AppRuleExecutionStatus.Excluded);
            }

            var rule = AppRuleMatcher.Resolve(configuration.AppRules, eventType, initialCandidate.Identity);
            if (rule is null)
            {
                return new AppRuleExecutionResult(AppRuleExecutionStatus.NoMatch);
            }

            return await HandleResolvedRuleAsync(rule, eventType, initialCandidate, token);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return new AppRuleExecutionResult(AppRuleExecutionStatus.Cancelled);
        }
    }

    public async Task<IReadOnlyList<AppRuleExecutionResult>> HandleLayoutActivatedAsync(Guid layoutId)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var token = CurrentToken();
        try
        {
            var operations = new List<Task<AppRuleExecutionResult>>();
            foreach (var candidate in windowGateway.GetCandidates())
            {
                var configuration = configurationProvider();
                if (AppExclusionMatcher.IsExcluded(configuration.AppExclusions, candidate.Identity))
                {
                    continue;
                }

                var rule = AppRuleMatcher.Resolve(
                    configuration.AppRules.Where(candidateRule => candidateRule.TargetLayoutId == layoutId),
                    AppRuleEvent.LayoutActivated,
                    candidate.Identity);
                if (rule is not null)
                {
                    operations.Add(HandleResolvedRuleAsync(
                        rule,
                        AppRuleEvent.LayoutActivated,
                        candidate,
                        token));
                }
            }

            return await Task.WhenAll(operations);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return [new AppRuleExecutionResult(AppRuleExecutionStatus.Cancelled)];
        }
    }

    public void CancelPending()
    {
        lock (cancellationLock)
        {
            pending.Cancel();
            pending.Dispose();
            pending = new CancellationTokenSource();
        }
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        lock (cancellationLock)
        {
            pending.Cancel();
            pending.Dispose();
        }
        executor.Dispose();
    }

    private async Task<AppRuleExecutionResult> ExecuteAsync(
        Guid ruleId,
        AppRuleEvent eventType,
        WindowRuleCandidate initialCandidate,
        Guid? requiredTargetLayoutId,
        CancellationToken token)
    {
        for (var attempt = 0; ; attempt++)
        {
            token.ThrowIfCancellationRequested();
            var configuration = configurationProvider();
            var rule = configuration.AppRules.FirstOrDefault(candidate => candidate.Id == ruleId);
            if (rule is null ||
                !rule.IsEnabled ||
                rule.Event != eventType ||
                (requiredTargetLayoutId.HasValue && rule.TargetLayoutId != requiredTargetLayoutId.Value))
            {
                return new AppRuleExecutionResult(AppRuleExecutionStatus.NoMatch, ruleId);
            }

            var currentCandidate = windowGateway.Inspect(initialCandidate.WindowHandle);
            if (currentCandidate is null ||
                currentCandidate.Identity.ProcessId != initialCandidate.Identity.ProcessId ||
                !AppRuleMatcher.Matches(rule, currentCandidate.Identity))
            {
                return new AppRuleExecutionResult(AppRuleExecutionStatus.CandidateUnavailable, ruleId);
            }

            // Der Ausschluss wird unmittelbar vor dem Platzieren erneut geprueft, weil zwischen Auswahl
            // und Ausfuehrung eine konfigurierte Verzoegerung liegen kann.
            if (AppExclusionMatcher.IsExcluded(configuration.AppExclusions, currentCandidate.Identity))
            {
                return new AppRuleExecutionResult(AppRuleExecutionStatus.Excluded, ruleId);
            }

            if (!TryResolveTarget(configuration, rule, out var bounds, out var targetName))
            {
                reportStatus?.Invoke($"App-Regel pausiert: Ziel für {rule.DisplayName} fehlt.");
                return new AppRuleExecutionResult(AppRuleExecutionStatus.TargetMissing, ruleId);
            }

            var outcome = windowGateway.Snap(currentCandidate.WindowHandle, bounds);
            if (outcome.Succeeded)
            {
                reportStatus?.Invoke($"App-Regel angewendet: {rule.DisplayName} → {targetName}");
                return new AppRuleExecutionResult(AppRuleExecutionStatus.Applied, ruleId);
            }

            // Ein bewegtes Fenster mit Mindestgroesse wird durch Wiederholen nicht kleiner.
            if (attempt >= rule.RetryCount || outcome.WindowMoved)
            {
                reportStatus?.Invoke(
                    $"App-Regel konnte {rule.DisplayName} nicht positionieren: "
                        + (outcome.Rejection ?? "Windows hat die Platzierung abgelehnt."));
                return new AppRuleExecutionResult(AppRuleExecutionStatus.WindowsRejected, ruleId);
            }

            await delay(RetryDelay, token);
        }
    }

    private async Task<AppRuleExecutionResult> HandleResolvedRuleAsync(
        AppRule rule,
        AppRuleEvent eventType,
        WindowRuleCandidate initialCandidate,
        CancellationToken token)
    {
        if (rule.DelayMilliseconds > 0)
        {
            await delay(TimeSpan.FromMilliseconds(rule.DelayMilliseconds), token);
        }

        await executor.WaitAsync(token);
        try
        {
            return await ExecuteAsync(
                rule.Id,
                eventType,
                initialCandidate,
                eventType == AppRuleEvent.LayoutActivated ? rule.TargetLayoutId : null,
                token);
        }
        finally
        {
            executor.Release();
        }
    }

    private bool TryResolveTarget(
        SnapConfiguration configuration,
        AppRule rule,
        out PixelRect bounds,
        out string targetName)
    {
        bounds = default;
        targetName = string.Empty;
        var layout = configuration.Layouts.FirstOrDefault(candidate => candidate.Id == rule.TargetLayoutId);
        var zone = layout?.Zones.FirstOrDefault(candidate => candidate.Id == rule.TargetZoneId);
        var monitor = layout is null
            ? null
            : monitors.FirstOrDefault(candidate => LayoutService.BelongsToMonitor(candidate.Identity, layout.Monitor));
        if (layout is null || zone is null || monitor is null)
        {
            return false;
        }

        // Dieselbe Geometrie wie Overlay und Ziehpfad: Aussen- und Zonenabstand gelten auch hier.
        bounds = ZoneGeometry.ToPixels(
            zone.Bounds,
            monitor.WorkArea,
            new LayoutMetrics(configuration.Settings.EffectiveOuterMargins, configuration.Settings.ZoneGap));
        targetName = $"{layout.Name} / {zone.Name}";
        return true;
    }

    private CancellationToken CurrentToken()
    {
        lock (cancellationLock)
        {
            return pending.Token;
        }
    }
}
