namespace SnapZones.Core.Setup;

public enum InstallationState
{
    /// <summary>Das Programm läuft aus einem beliebigen Verzeichnis; es ist nicht installiert.</summary>
    NotInstalled,

    /// <summary>Am Installationsort liegt bereits eine Programmdatei; sie wird ersetzt.</summary>
    UpgradeInPlace,

    /// <summary>Das Programm läuft bereits aus dem Installationsverzeichnis.</summary>
    AlreadyInstalled
}

/// <summary>
/// Beschreibt, was eine Installation tun würde, bevor irgendetwas geschrieben wird.
///
/// Die Installation ist bewusst kein eigenes Setup-Programm, sondern ein Modus derselben Programmdatei:
/// <c>ZoneManager.exe --install</c>. Ein getrenntes Setup müsste die 66 MB grosse Programmdatei ein
/// zweites Mal enthalten und wäre in der Auslieferung doppelt so gross.
/// </summary>
public sealed record InstallationPlan(
    InstallationState State,
    string SourcePath,
    string TargetDirectory,
    string TargetPath,
    string ShortcutPath)
{
    /// <summary>Verzeichnisname unter «Programme». Ohne Apostroph, der in Pfaden nur Ärger macht.</summary>
    public const string DirectoryName = "ZoneManager";

    /// <summary>Name im Startmenü und in «Apps und Features».</summary>
    public const string DisplayName = "Sascha's Zone Manager";

    /// <summary>Schlüsselname unter <c>…\CurrentVersion\Uninstall</c>.</summary>
    public const string UninstallKeyName = "ZoneManager";

    public const string ExecutableName = "ZoneManager.exe";

    /// <summary>Ob die Programmdatei tatsächlich kopiert werden muss.</summary>
    public bool RequiresCopy => State != InstallationState.AlreadyInstalled;

    public static InstallationPlan Create(string sourcePath, string programFilesDirectory, string startMenuDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(programFilesDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(startMenuDirectory);

        var targetDirectory = Path.Combine(programFilesDirectory, DirectoryName);
        var targetPath = Path.Combine(targetDirectory, ExecutableName);
        var shortcutPath = Path.Combine(startMenuDirectory, DisplayName + ".lnk");
        var state = SamePath(sourcePath, targetPath)
            ? InstallationState.AlreadyInstalled
            : File.Exists(targetPath)
                ? InstallationState.UpgradeInPlace
                : InstallationState.NotInstalled;

        return new InstallationPlan(
            state,
            Path.GetFullPath(sourcePath),
            targetDirectory,
            targetPath,
            shortcutPath);
    }

    /// <summary>
    /// Die Werte, die «Apps und Features» anzeigt. <c>NoModify</c> und <c>NoRepair</c> sind gesetzt, weil
    /// es weder eine Änderungs- noch eine Reparaturfunktion gibt; ohne sie böte Windows Schaltflächen an,
    /// die ins Leere führen.
    /// </summary>
    public IReadOnlyDictionary<string, string> BuildUninstallEntry(string version) =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["DisplayName"] = DisplayName,
            ["DisplayVersion"] = version,
            ["Publisher"] = "Sascha",
            ["DisplayIcon"] = TargetPath,
            ["InstallLocation"] = TargetDirectory,
            ["UninstallString"] = $"\"{TargetPath}\" --uninstall",
            ["QuietUninstallString"] = $"\"{TargetPath}\" --uninstall --silent",
            ["NoModify"] = "1",
            ["NoRepair"] = "1"
        };

    private static bool SamePath(string first, string second)
    {
        try
        {
            return string.Equals(
                Path.GetFullPath(first).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(second).TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }
}
