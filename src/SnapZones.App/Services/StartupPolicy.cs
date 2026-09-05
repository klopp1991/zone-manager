namespace SnapZones.App.Services;

public enum StartupDisposition
{
    StartVisible,
    StartHidden,
    ActivateRunningInstance,
    ExitDuplicate,

    /// <summary>Mit <c>--exit</c> gestartet: die laufende Instanz um ihr Ende bitten, dann selbst enden.</summary>
    StopRunningInstance
}

public static class StartupPolicy
{
    public static StartupDisposition Decide(IEnumerable<string> arguments, bool isPrimary)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var list = arguments.ToArray();
        var isAutoStart = StartupArguments.Contains(list, StartupArguments.Autostart);
        var isExit = StartupArguments.Contains(list, StartupArguments.Exit);
        if (isPrimary)
        {
            // Niemand laeuft, den man beenden koennte: die Bitte ist erfuellt.
            if (isExit)
            {
                return StartupDisposition.ExitDuplicate;
            }

            return isAutoStart
                ? StartupDisposition.StartHidden
                : StartupDisposition.StartVisible;
        }

        if (isExit)
        {
            return StartupDisposition.StopRunningInstance;
        }

        return isAutoStart
            ? StartupDisposition.ExitDuplicate
            : StartupDisposition.ActivateRunningInstance;
    }
}
