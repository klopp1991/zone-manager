namespace SnapZones.App.Services;

public enum StartupMode
{
    Normal,
    Diagnostics,
    DpiProbe
}

public static class StartupModeResolver
{
    public static StartupMode Resolve(IEnumerable<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var values = arguments.ToArray();
        if (values.Contains("--dpi-probe", StringComparer.OrdinalIgnoreCase))
        {
            return StartupMode.DpiProbe;
        }

        return values.Contains("--diagnostics", StringComparer.OrdinalIgnoreCase)
            ? StartupMode.Diagnostics
            : StartupMode.Normal;
    }
}
