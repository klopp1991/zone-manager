using Microsoft.Win32;

namespace ZoneManager.Windows.Startup;

public sealed class WindowsStartupService(string executablePath) : IStartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ZoneManager";

    /// <summary>Wertname bis Version 2026.08; wird entfernt, damit kein doppelter Autostart entsteht.</summary>
    private const string LegacyValueName = "SnapZones";
    private readonly string executablePath = executablePath ?? throw new ArgumentNullException(nameof(executablePath));

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            var value = key?.GetValue(ValueName) as string;
            return string.Equals(value, BuildCommand(executablePath), StringComparison.OrdinalIgnoreCase);
        }
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        key.DeleteValue(LegacyValueName, throwOnMissingValue: false);
        if (enabled)
        {
            key.SetValue(ValueName, BuildCommand(executablePath), RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    public static string BuildCommand(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (path.Contains('"', StringComparison.Ordinal))
        {
            throw new ArgumentException("Der Programmpfad enthält ein ungültiges Anführungszeichen.", nameof(path));
        }

        return $"\"{path}\" --autostart";
    }
}
