using System.IO;

namespace SnapZones.App.Services;

public sealed record ApplicationDataPaths(string ConfigurationDirectory, string LogDirectory)
{
    public static ApplicationDataPaths Resolve(
        string executablePath,
        string roamingRoot,
        string localRoot)
    {
        var executableDirectory = Path.GetDirectoryName(executablePath)
            ?? throw new ArgumentException("Ausführbarer Pfad hat kein Verzeichnis.", nameof(executablePath));

        return File.Exists(Path.Combine(executableDirectory, "portable.flag"))
            ? new ApplicationDataPaths(
                Path.Combine(executableDirectory, "Data"),
                Path.Combine(executableDirectory, "Logs"))
            : new ApplicationDataPaths(
                Path.Combine(roamingRoot, "SnapZones"),
                Path.Combine(localRoot, "SnapZones", "logs"));
    }
}
