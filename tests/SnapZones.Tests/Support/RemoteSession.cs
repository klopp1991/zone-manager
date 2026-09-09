using System.Runtime.InteropServices;

namespace SnapZones.Tests.Support;

/// <summary>
/// In einer Fernsitzung zeigt Windows dem Prozess nur die Platzhalteranzeige der Sitzung
/// («Generic PnP Monitor» unter <c>Default_Monitor</c>); die echten Monitore und der virtuelle
/// Monitor gehoeren zur getrennten Konsolensitzung und sind nicht sichtbar. Tests, die einen
/// echten Bildschirmaufbau brauchen, koennen hier nichts pruefen und ueberspringen sich selbst.
/// </summary>
internal static class RemoteSession
{
    private const int SmRemoteSession = 0x1000;

    /// <summary>Ob der Testprozess in einer Fernsitzung laeuft.</summary>
    public static bool IsActive => OperatingSystem.IsWindows() && GetSystemMetrics(SmRemoteSession) != 0;

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);
}
