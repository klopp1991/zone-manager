namespace ZoneManager.Core.PartMonitors;

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
