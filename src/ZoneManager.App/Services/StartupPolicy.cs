namespace ZoneManager.App.Services;

public enum StartupDisposition
{
    StartVisible,
    StartHidden,
    ActivateRunningInstance,
    ExitDuplicate
}

public static class StartupPolicy
{
    public static StartupDisposition Decide(IEnumerable<string> arguments, bool isPrimary)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var isAutoStart = arguments.Contains("--autostart", StringComparer.OrdinalIgnoreCase);
        if (isPrimary)
        {
            return isAutoStart
                ? StartupDisposition.StartHidden
                : StartupDisposition.StartVisible;
        }

        return isAutoStart
            ? StartupDisposition.ExitDuplicate
            : StartupDisposition.ActivateRunningInstance;
    }
}
