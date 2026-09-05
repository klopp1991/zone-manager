using System.Security.Principal;

namespace SnapZones.App.Services;

/// <summary>Ob der eigene Prozess mit Administratorrechten läuft.</summary>
public static class ElevationState
{
    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
}
