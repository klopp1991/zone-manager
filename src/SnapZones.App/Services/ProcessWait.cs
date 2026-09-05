using System.Diagnostics;

namespace SnapZones.App.Services;

/// <summary>Wartet auf das Ende eines fremden Prozesses, etwa des Vorgängers bei einem Neustart.</summary>
public static class ProcessWait
{
    /// <summary>
    /// Liefert <c>true</c>, wenn der Prozess innerhalb der Frist geendet hat oder gar nicht mehr läuft.
    /// Ein Prozess, der sich nicht öffnen lässt, gilt als beendet: dann kann er die Einzelinstanz auch
    /// nicht mehr halten.
    /// </summary>
    public static bool WaitForExit(int processId, TimeSpan timeout)
    {
        if (processId <= 0 || processId == Environment.ProcessId)
        {
            return true;
        }

        try
        {
            using var process = Process.GetProcessById(processId);
            return process.WaitForExit((int)Math.Clamp(timeout.TotalMilliseconds, 0, int.MaxValue));
        }
        catch (ArgumentException)
        {
            return true;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
        catch (System.ComponentModel.Win32Exception)
        {
            return true;
        }
    }
}
