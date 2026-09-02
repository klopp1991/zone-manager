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
        FillPartMonitorSpanCommand span => Place(
            span.WindowHandle,
            resolver.ResolveSpan(span.MonitorId, span.PartMonitorIds)),
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

        var outcome = gateway.ApplyNormal(previous.Identity, placement.Bounds);
        if (!outcome.Succeeded)
        {
            // Ein bewegtes, aber nicht passendes Fenster (Mindestgroesse) bleibt trotzdem im Verlauf,
            // damit «zurueck zur vorherigen Position» weiterhin funktioniert.
            if (outcome.WindowMoved)
            {
                history.Remember(previous);
            }

            return new PartMonitorCommandResult(PartMonitorCommandStatus.WindowsRejected, placement, outcome);
        }

        history.Remember(previous);
        return new PartMonitorCommandResult(PartMonitorCommandStatus.Successful, placement, outcome);
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
