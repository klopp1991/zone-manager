using System.IO;
using SnapZones.Core.Monitors;
using SnapZones.Windows.Displays;

namespace SnapZones.App.Services;

/// <summary>
/// Installiert und entfernt den Anzeigetreiber für Vollbildzonen. Läuft im erhöhten Prozess: als
/// Schritt der Installation nach «Programme» (<c>--install</c>, <c>--uninstall</c>) oder allein über
/// <c>--install-display-driver</c> und <c>--remove-display-driver</c>.
/// </summary>
public static class DisplayDriverSetup
{
    /// <summary>
    /// Entpackt das eingebettete Treiberpaket in ein Temporärverzeichnis, installiert es und räumt das
    /// Verzeichnis wieder weg; Windows hat die Dateien dann in seiner Treiberablage.
    /// </summary>
    public static DriverActionResult Install()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"ZoneManager-Anzeigetreiber-{Guid.NewGuid():N}");
        try
        {
            var infPath = VirtualDisplayDriverPackage.Extract(directory);
            var initialModes = VirtualDisplayModes.ToSettingsXml(VirtualDisplayModes.Build([]));
            return new VirtualDisplayDriverService().Install(infPath, initialModes);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new DriverActionResult(DriverActionOutcome.Failed, $"Das Treiberpaket liess sich nicht entpacken: {exception.Message}");
        }
        finally
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Ein liegengebliebenes Temporaerverzeichnis stoert nicht; Windows raeumt es auf.
            }
        }
    }

    public static DriverActionResult Remove() => new VirtualDisplayDriverService().Uninstall();
}
