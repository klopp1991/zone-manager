using System.IO;
using Microsoft.Win32;
using SnapZones.Core.Setup;

namespace SnapZones.Windows.Setup;

public enum InstallationOutcome
{
    Installed,
    AlreadyCurrent,
    Failed
}

public sealed record InstallationResult(InstallationOutcome Outcome, string Message, string? InstalledPath = null);

public enum RemovalOutcome
{
    Removed,
    NotInstalled,
    Failed
}

public sealed record RemovalResult(RemovalOutcome Outcome, string Message);

/// <summary>
/// Führt eine Installation nach «Programme» aus und entfernt sie wieder.
///
/// Beides verlangt Administratorrechte für den Schreibzugriff auf «Programme» und den Uninstall-Schlüssel
/// unter <c>HKEY_LOCAL_MACHINE</c>. Die Anwendung besitzt sie im Normalbetrieb ohnehin.
/// </summary>
public sealed class InstallationService
{
    private const string UninstallKeyPath =
        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" + InstallationPlan.UninstallKeyName;

    public static InstallationPlan CreatePlan(string sourcePath) => InstallationPlan.Create(
        sourcePath,
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms));

    /// <summary>Ob eine Installation registriert ist, und wo sie liegt.</summary>
    public static string? InstalledPath
    {
        get
        {
            using var key = Registry.LocalMachine.OpenSubKey(UninstallKeyPath, writable: false);
            return key?.GetValue("DisplayIcon") as string;
        }
    }

    public InstallationResult Install(InstallationPlan plan, string version)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        if (plan.State == InstallationState.AlreadyInstalled)
        {
            // Auch dann werden Registrierung und Verknuepfung erneuert: sie koennen fehlen, wenn eine
            // fruehere Installation nur teilweise durchlief.
            RegisterUninstall(plan, version);
            TryCreateShortcut(plan);
            return new InstallationResult(
                InstallationOutcome.AlreadyCurrent,
                "Das Programm läuft bereits aus dem Installationsverzeichnis.",
                plan.TargetPath);
        }

        try
        {
            Directory.CreateDirectory(plan.TargetDirectory);
            CopyOverwritingRunningFile(plan.SourcePath, plan.TargetPath);

            // Der Fensterhelfer wird mitgenommen, wenn er neben der Quelldatei liegt. Fehlt er, bleibt
            // die Installation gueltig; das Programm geht dann seinen Weg ueber die UAC-Abfrage.
            if (File.Exists(plan.SourceHelperPath))
            {
                CopyOverwritingRunningFile(plan.SourceHelperPath, plan.TargetHelperPath);
            }
            RegisterUninstall(plan, version);
            TryCreateShortcut(plan);
            return new InstallationResult(
                InstallationOutcome.Installed,
                $"Installiert nach {plan.TargetDirectory}.",
                plan.TargetPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return new InstallationResult(
                InstallationOutcome.Failed,
                $"Die Installation ist fehlgeschlagen: {exception.Message}");
        }
    }

    /// <summary>
    /// Entfernt Programmdatei, Verknüpfung und Registrierung. Die Einstellungen unter
    /// <c>%APPDATA%\SnapZones</c> bleiben unberührt — sie gehören dem Benutzer, nicht dem Programm, und
    /// eine Neuinstallation soll sie wiederfinden.
    /// </summary>
    public RemovalResult Uninstall()
    {
        var installed = InstalledPath;
        if (installed is null || !File.Exists(installed))
        {
            Registry.LocalMachine.DeleteSubKeyTree(UninstallKeyPath, throwOnMissingSubKey: false);
            return new RemovalResult(RemovalOutcome.NotInstalled, "Es ist keine Installation registriert.");
        }

        var directory = Path.GetDirectoryName(installed);
        var problems = new List<string>();
        var plan = InstallationPlan.Create(
            installed!,
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms));

        TryDelete(plan.ShortcutPath, problems);
        TryDelete(plan.TargetHelperPath, problems);

        // Die laufende Programmdatei laesst sich nicht loeschen, wohl aber umbenennen. Sie wird deshalb
        // beiseitegeschoben und zum Loeschen beim naechsten Neustart vorgemerkt.
        if (!TryDelete(installed, problems) && !TryScheduleDeletion(installed, problems))
        {
            problems.Add("Die Programmdatei bleibt bis zum nächsten Neustart liegen.");
        }

        try
        {
            Registry.LocalMachine.DeleteSubKeyTree(UninstallKeyPath, throwOnMissingSubKey: false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            problems.Add($"Der Eintrag in «Apps und Features» blieb bestehen: {exception.Message}");
        }

        if (directory is not null && Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
        {
            TryDeleteDirectory(directory, problems);
        }

        return problems.Count == 0
            ? new RemovalResult(
                RemovalOutcome.Removed,
                "Entfernt. Die Einstellungen unter %APPDATA%\\SnapZones bleiben erhalten.")
            : new RemovalResult(
                RemovalOutcome.Removed,
                "Teilweise entfernt. " + string.Join(" ", problems));
    }

    /// <summary>
    /// Kopiert über eine möglicherweise laufende Programmdatei. Windows lässt sie nicht überschreiben,
    /// wohl aber umbenennen; der beiseitegeschobene Stand wird beim nächsten Start weggeräumt.
    /// </summary>
    private static void CopyOverwritingRunningFile(string sourcePath, string targetPath)
    {
        if (File.Exists(targetPath))
        {
            var supersededPath =
                $"{targetPath}.previous.{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            File.Move(targetPath, supersededPath);
        }

        File.Copy(sourcePath, targetPath, overwrite: false);
    }

    private static void RegisterUninstall(InstallationPlan plan, string version)
    {
        using var key = Registry.LocalMachine.CreateSubKey(UninstallKeyPath, writable: true);
        foreach (var (name, value) in plan.BuildUninstallEntry(version))
        {
            key.SetValue(name, value, RegistryValueKind.String);
        }
    }

    private static void TryCreateShortcut(InstallationPlan plan)
    {
        try
        {
            var directory = Path.GetDirectoryName(plan.ShortcutPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            ShellLink.Create(plan.ShortcutPath, plan.TargetPath, InstallationPlan.DisplayName);
        }
        catch (Exception)
        {
            // Eine fehlende Verknuepfung macht die Installation nicht unbrauchbar; das Programm liegt
            // trotzdem am Platz und laesst sich starten.
        }
    }

    private static bool TryDelete(string path, List<string> problems)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            problems.Add($"«{Path.GetFileName(path)}» liess sich nicht löschen: {exception.Message}");
            return false;
        }
    }

    private static bool TryScheduleDeletion(string path, List<string> problems)
    {
        try
        {
            var supersededPath = $"{path}.previous.{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}";
            File.Move(path, supersededPath);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            problems.Add($"Die Programmdatei liess sich nicht beiseiteschieben: {exception.Message}");
            return false;
        }
    }

    private static void TryDeleteDirectory(string path, List<string> problems)
    {
        try
        {
            Directory.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            problems.Add($"Das Verzeichnis blieb bestehen: {exception.Message}");
        }
    }
}
