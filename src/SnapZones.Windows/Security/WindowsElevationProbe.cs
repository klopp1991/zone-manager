using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32;
using SnapZones.Windows.Native;

namespace SnapZones.Windows.Security;

/// <summary>
/// Ergebnis der Vorprüfung. Alle Werte stammen ausschliesslich aus Token- und Registryabfragen;
/// die Prüfung startet keinen Prozess und löst deshalb keine UAC-Abfrage aus.
/// </summary>
public sealed record ElevationProbeResult(
    bool IsElevated,
    bool IsAdministratorMember,
    bool IsUserAccountControlEnabled,
    bool IsInteractiveSession);

public static class WindowsElevationProbe
{
    private const int TokenElevationType = 18;
    private const int TokenLinkedToken = 19;
    private const int TokenElevationTypeLimited = 3;
    private const string SystemPolicyKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System";
    private const string EnableLimitedUserAccountValueName = "EnableLUA";

    public static ElevationProbeResult Inspect()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var isElevated = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        return new ElevationProbeResult(
            isElevated,
            isElevated || IsLinkedTokenAdministrator(identity),
            IsUserAccountControlEnabled(),
            IsInteractiveSession());
    }

    /// <summary>
    /// Prüft, ob das gefilterte Token des Benutzers ein vollständiges Administratortoken besitzt.
    /// </summary>
    private static bool IsLinkedTokenAdministrator(WindowsIdentity identity)
    {
        try
        {
            if (ReadElevationType(identity) != TokenElevationTypeLimited)
            {
                return false;
            }

            var buffer = Marshal.AllocHGlobal(nint.Size);
            try
            {
                if (!Advapi32.GetTokenInformation(
                        identity.AccessToken,
                        TokenLinkedToken,
                        buffer,
                        (uint)nint.Size,
                        out _))
                {
                    return false;
                }

                var linkedToken = Marshal.ReadIntPtr(buffer);
                if (linkedToken == 0)
                {
                    return false;
                }

                try
                {
                    using var linkedIdentity = new WindowsIdentity(linkedToken);
                    return new WindowsPrincipal(linkedIdentity).IsInRole(WindowsBuiltInRole.Administrator);
                }
                finally
                {
                    _ = Kernel32.CloseHandle(linkedToken);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static int ReadElevationType(WindowsIdentity identity)
    {
        var buffer = Marshal.AllocHGlobal(sizeof(int));
        try
        {
            return Advapi32.GetTokenInformation(
                identity.AccessToken,
                TokenElevationType,
                buffer,
                sizeof(int),
                out _)
                ? Marshal.ReadInt32(buffer)
                : 0;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static bool IsUserAccountControlEnabled()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(SystemPolicyKeyPath, writable: false);
            // Fehlt der Wert, gilt die Voreinstellung von Windows 11: Benutzerkontensteuerung aktiv.
            return key?.GetValue(EnableLimitedUserAccountValueName) is not int value || value != 0;
        }
        catch (Exception)
        {
            return true;
        }
    }

    private static bool IsInteractiveSession()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            return Environment.UserInteractive && process.SessionId != 0;
        }
        catch (Exception)
        {
            return Environment.UserInteractive;
        }
    }
}
