using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using SnapZones.Core.Monitors;
using SnapZones.Windows.Native;

namespace SnapZones.Windows.Displays;

/// <summary>Was vom Anzeigetreiber auf diesem Rechner vorhanden ist.</summary>
/// <param name="DevicePresent">Der Geraeteknoten <c>Root\MttVDD</c> ist angelegt und laeuft.</param>
/// <param name="DriverStored">Das Treiberpaket liegt in der Treiberablage von Windows.</param>
/// <param name="ConfigurationPresent">Die Datei <c>vdd_settings.xml</c> mit der Modeliste liegt vor.</param>
public sealed record VirtualDisplayDriverStatus(bool DevicePresent, bool DriverStored, bool ConfigurationPresent)
{
    /// <summary>Ob die Funktion benutzbar ist: Geraet da und Modeliste da.</summary>
    public bool Ready => DevicePresent && ConfigurationPresent;

    /// <summary>Ob irgendetwas vom Treiber zurueckgeblieben ist, das ein Entfernen wegraeumen muss.</summary>
    public bool AnythingPresent => DevicePresent || DriverStored || ConfigurationPresent;
}

public enum DriverActionOutcome
{
    Done,
    AlreadyDone,
    Failed
}

public sealed record DriverActionResult(DriverActionOutcome Outcome, string Message)
{
    public bool Successful => Outcome != DriverActionOutcome.Failed;
}

/// <summary>
/// Installiert den Anzeigetreiber «Virtual Display Driver», prueft sein Vorhandensein und entfernt ihn.
///
/// <para>
/// Installieren und Entfernen verlangen Administratorrechte; sie laufen im erhoehten Hilfsprozess der
/// Installation (<c>--install</c>, <c>--uninstall</c>) oder ueber die eigenen Schalter
/// <c>--install-display-driver</c> und <c>--remove-display-driver</c>. Das Pruefen kommt ohne Rechte aus.
/// </para>
/// <para>
/// Die Installation ersetzt devcon: sie legt den Geraeteknoten der Klasse «Display» mit der
/// Hardware-Kennung <c>Root\MttVDD</c> per SetupAPI an und laesst Windows den Treiber darauf einrichten.
/// Die Modeliste liegt unter <c>C:\VirtualDisplayDriver\vdd_settings.xml</c> — der Voreinstellung des
/// Treibers, der den Pfad zusaetzlich aus <c>HKLM\SOFTWARE\MikeTheTech\VirtualDisplayDriver</c> liest.
/// Das Verzeichnis bekommt Schreibrechte fuer Benutzer, damit das gewoehnlich berechtigte Programm die
/// Liste aus den Zonen nachfuehren kann.
/// </para>
/// </summary>
public sealed class VirtualDisplayDriverService
{
    public const string ConfigurationDirectory = @"C:\VirtualDisplayDriver";
    public const string SettingsFileName = "vdd_settings.xml";
    public const string RegistryKeyPath = @"SOFTWARE\MikeTheTech\VirtualDisplayDriver";
    private const string RegistryPathValue = "VDDPATH";
    private static Guid displayClass = new("4D36E968-E325-11CE-BFC1-08002BE10318");

    public static string SettingsPath => Path.Combine(ConfigurationDirectory, SettingsFileName);

    /// <summary>Liest ohne Rechte, was vom Treiber vorhanden ist.</summary>
    public static VirtualDisplayDriverStatus ReadStatus()
    {
        bool devicePresent;
        try
        {
            devicePresent = FindDevices(presentOnly: true).Count > 0;
        }
        catch (Win32Exception)
        {
            devicePresent = false;
        }

        return new VirtualDisplayDriverStatus(devicePresent, IsDriverStored(), File.Exists(SettingsPath));
    }

    /// <summary>
    /// Installiert den Treiber. <paramref name="infPath"/> zeigt auf die entpackte INF-Datei,
    /// <paramref name="initialSettingsXml"/> ist die Modeliste fuer den Fall, dass noch keine liegt.
    /// </summary>
    public DriverActionResult Install(string infPath, string initialSettingsXml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(infPath);
        ArgumentNullException.ThrowIfNull(initialSettingsXml);
        if (!File.Exists(infPath))
        {
            return new DriverActionResult(DriverActionOutcome.Failed, $"Die Treiberdatei fehlt: {infPath}");
        }

        try
        {
            PrepareConfiguration(initialSettingsXml);
            var created = false;
            if (FindDevices(presentOnly: false).Count == 0)
            {
                CreateDeviceNode();
                created = true;
            }

            var stopwatch = Stopwatch.StartNew();
            if (!NewDev.UpdateDriverForPlugAndPlayDevicesW(0, VirtualDisplay.AdapterHardwareId, infPath, NewDev.InstallFlagForce, out var rebootRequired))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Der Anzeigetreiber liess sich nicht einrichten.");
            }

            var message = created
                ? $"Der Anzeigetreiber ist installiert ({stopwatch.Elapsed.TotalSeconds:0.0} s)."
                : "Der Anzeigetreiber war schon angelegt und wurde erneuert.";
            if (rebootRequired)
            {
                message += " Windows verlangt einen Neustart.";
            }

            return new DriverActionResult(created ? DriverActionOutcome.Done : DriverActionOutcome.AlreadyDone, message);
        }
        catch (Exception exception) when (exception is Win32Exception or IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            return new DriverActionResult(DriverActionOutcome.Failed, $"Der Anzeigetreiber liess sich nicht installieren: {exception.Message}");
        }
    }

    /// <summary>Entfernt Geraeteknoten, Treiberpaket, Registrierungsschluessel und Modeliste.</summary>
    public DriverActionResult Uninstall()
    {
        var status = ReadStatus();
        var problems = new List<string>();
        var removedDevices = 0;
        try
        {
            removedDevices = RemoveDevices();
        }
        catch (Win32Exception exception)
        {
            problems.Add($"Der Geraeteknoten blieb bestehen: {exception.Message}");
        }

        foreach (var publishedName in PublishedInfNames())
        {
            var (exitCode, output) = RunPnpUtil($"/delete-driver {publishedName} /uninstall /force");
            if (exitCode != 0)
            {
                problems.Add($"Das Treiberpaket {publishedName} blieb in der Ablage: {output}");
            }
        }

        try
        {
            Registry.LocalMachine.DeleteSubKeyTree(RegistryKeyPath, throwOnMissingSubKey: false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            problems.Add($"Der Registrierungsschluessel blieb bestehen: {exception.Message}");
        }

        try
        {
            if (Directory.Exists(ConfigurationDirectory))
            {
                Directory.Delete(ConfigurationDirectory, recursive: true);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            problems.Add($"Das Verzeichnis {ConfigurationDirectory} blieb bestehen: {exception.Message}");
        }

        if (!status.AnythingPresent && removedDevices == 0)
        {
            return new DriverActionResult(DriverActionOutcome.AlreadyDone, "Es ist kein Anzeigetreiber installiert.");
        }

        return problems.Count == 0
            ? new DriverActionResult(DriverActionOutcome.Done, "Der Anzeigetreiber wurde entfernt.")
            : new DriverActionResult(DriverActionOutcome.Failed, "Der Anzeigetreiber wurde nur teilweise entfernt. " + string.Join(" ", problems));
    }

    /// <summary>
    /// Die veroeffentlichten Namen (<c>oemNN.inf</c>) aller Treiberpakete in der Ablage, deren
    /// Originalname der INF des Anzeigetreibers entspricht. Liest die Ausgabe von
    /// <c>pnputil /enum-drivers</c>, die je nach Sprache anders beschriftet ist.
    /// </summary>
    public static IReadOnlyList<string> PublishedInfNames()
    {
        var (exitCode, output) = RunPnpUtil("/enum-drivers");
        return exitCode == 0 ? ParsePublishedInfNames(output, VirtualDisplayDriverPackage.InfFileName) : [];
    }

    public static IReadOnlyList<string> ParsePublishedInfNames(string pnpUtilOutput, string originalInfName)
    {
        ArgumentNullException.ThrowIfNull(pnpUtilOutput);
        var result = new List<string>();
        foreach (var block in Regex.Split(pnpUtilOutput, @"\r?\n\s*\r?\n"))
        {
            if (!block.Contains(originalInfName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var match = Regex.Match(block, @"\boem\d+\.inf\b", RegexOptions.IgnoreCase);
            if (match.Success && !result.Contains(match.Value, StringComparer.OrdinalIgnoreCase))
            {
                result.Add(match.Value);
            }
        }

        return result;
    }

    private static bool IsDriverStored()
    {
        var repository = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "DriverStore", "FileRepository");
        try
        {
            return Directory.Exists(repository) &&
                Directory.EnumerateDirectories(repository, "mttvdd.inf_*").Any();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static void PrepareConfiguration(string initialSettingsXml)
    {
        Directory.CreateDirectory(ConfigurationDirectory);
        GrantUsersModifyAccess(ConfigurationDirectory);
        if (!File.Exists(SettingsPath))
        {
            File.WriteAllText(SettingsPath, initialSettingsXml, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }

        using var key = Registry.LocalMachine.CreateSubKey(RegistryKeyPath, writable: true);
        key.SetValue(RegistryPathValue, ConfigurationDirectory, RegistryValueKind.String);
    }

    private static void GrantUsersModifyAccess(string directory)
    {
        var info = new DirectoryInfo(directory);
        var security = info.GetAccessControl();
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
            FileSystemRights.Modify,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow));
        info.SetAccessControl(security);
    }

    private static void CreateDeviceNode()
    {
        var set = SetupApi.SetupDiCreateDeviceInfoList(ref displayClass, 0);
        if (set == SetupApi.InvalidHandleValue)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Die Geraeteliste liess sich nicht anlegen.");
        }

        try
        {
            var data = new SpDevInfoData { Size = (uint)Marshal.SizeOf<SpDevInfoData>() };
            if (!SetupApi.SetupDiCreateDeviceInfoW(set, "Display", ref displayClass, VirtualDisplay.AdapterDescription, 0, SetupApi.DicdGenerateId, ref data))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Der Geraeteknoten liess sich nicht anlegen.");
            }

            var hardwareId = Encoding.Unicode.GetBytes(VirtualDisplay.AdapterHardwareId + "\0\0");
            if (!SetupApi.SetupDiSetDeviceRegistryPropertyW(set, ref data, SetupApi.SpdrpHardwareId, hardwareId, (uint)hardwareId.Length))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Die Hardware-Kennung liess sich nicht setzen.");
            }

            if (!SetupApi.SetupDiCallClassInstaller(SetupApi.DifRegisterDevice, set, ref data))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Der Geraeteknoten liess sich nicht registrieren.");
            }
        }
        finally
        {
            SetupApi.SetupDiDestroyDeviceInfoList(set);
        }
    }

    private static int RemoveDevices()
    {
        var set = SetupApi.SetupDiGetClassDevsW(ref displayClass, null, 0, 0);
        if (set == SetupApi.InvalidHandleValue)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Die Anzeigegeraete liessen sich nicht aufzaehlen.");
        }

        var removed = 0;
        try
        {
            for (uint index = 0; ; index++)
            {
                var data = new SpDevInfoData { Size = (uint)Marshal.SizeOf<SpDevInfoData>() };
                if (!SetupApi.SetupDiEnumDeviceInfo(set, index, ref data))
                {
                    break;
                }

                if (!HasVirtualHardwareId(set, ref data))
                {
                    continue;
                }

                if (!SetupApi.SetupDiCallClassInstaller(SetupApi.DifRemove, set, ref data))
                {
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Der Geraeteknoten liess sich nicht entfernen.");
                }

                removed++;
            }
        }
        finally
        {
            SetupApi.SetupDiDestroyDeviceInfoList(set);
        }

        return removed;
    }

    private static IReadOnlyList<string> FindDevices(bool presentOnly)
    {
        var set = SetupApi.SetupDiGetClassDevsW(ref displayClass, null, 0, presentOnly ? SetupApi.DigcfPresent : 0);
        if (set == SetupApi.InvalidHandleValue)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Die Anzeigegeraete liessen sich nicht aufzaehlen.");
        }

        var result = new List<string>();
        try
        {
            for (uint index = 0; ; index++)
            {
                var data = new SpDevInfoData { Size = (uint)Marshal.SizeOf<SpDevInfoData>() };
                if (!SetupApi.SetupDiEnumDeviceInfo(set, index, ref data))
                {
                    break;
                }

                if (!HasVirtualHardwareId(set, ref data))
                {
                    continue;
                }

                var builder = new StringBuilder(512);
                SetupApi.SetupDiGetDeviceInstanceIdW(set, ref data, builder, (uint)builder.Capacity, out _);
                result.Add(builder.ToString());
            }
        }
        finally
        {
            SetupApi.SetupDiDestroyDeviceInfoList(set);
        }

        return result;
    }

    private static bool HasVirtualHardwareId(nint set, ref SpDevInfoData data)
    {
        var buffer = new byte[4096];
        if (!SetupApi.SetupDiGetDeviceRegistryPropertyW(set, ref data, SetupApi.SpdrpHardwareId, out _, buffer, (uint)buffer.Length, out var required))
        {
            return false;
        }

        var ids = Encoding.Unicode.GetString(buffer, 0, (int)Math.Min(required, buffer.Length)).Split('\0', StringSplitOptions.RemoveEmptyEntries);
        return ids.Any(id => string.Equals(id, VirtualDisplay.AdapterHardwareId, StringComparison.OrdinalIgnoreCase));
    }

    private static (int ExitCode, string Output) RunPnpUtil(string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "pnputil.exe"),
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });
            if (process is null)
            {
                return (-1, "pnputil liess sich nicht starten.");
            }

            var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit();
            return (process.ExitCode, output.Trim());
        }
        catch (Exception exception) when (exception is Win32Exception or IOException or InvalidOperationException)
        {
            return (-1, exception.Message);
        }
    }
}
