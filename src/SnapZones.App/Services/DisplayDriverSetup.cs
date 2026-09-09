using System.IO;
using SnapZones.Core.Geometry;
using SnapZones.Core.Layouts;
using SnapZones.Core.Models;
using SnapZones.Core.Monitors;
using SnapZones.Core.Persistence;
using SnapZones.Windows.Displays;

namespace SnapZones.App.Services;

public enum DisplayDriverAction
{
    Install,
    Remove,
    Restart
}

/// <summary>
/// Installiert, entfernt und startet den Vollbildzonen-Treiber neu. Läuft im erhöhten
/// Prozess: als Schritt der Installation nach «Programme» (<c>--install</c>, <c>--uninstall</c>) oder
/// allein über <c>--install-display-driver</c>, <c>--remove-display-driver</c> und
/// <c>--restart-display-driver</c>.
/// </summary>
public static class DisplayDriverSetup
{
    /// <summary>
    /// Entpackt das eingebettete Treiberpaket in ein Temporärverzeichnis, installiert es mit der
    /// Modeliste aus den Vollbildzonen des Benutzers und räumt das Verzeichnis wieder weg; Windows
    /// hat die Dateien dann in seiner Treiberablage.
    /// </summary>
    public static DriverActionResult Install()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"ZoneManager-Vollbildzonen-Treiber-{Guid.NewGuid():N}");
        try
        {
            var infPath = VirtualDisplayDriverPackage.Extract(directory);
            return new VirtualDisplayDriverService().Install(infPath, VirtualDisplayModes.ToSettingsXml(ModesForConfiguredZones()));
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

    public static DriverActionResult Restart() => new VirtualDisplayDriverService().Restart();

    /// <summary>
    /// Die Modeliste aus allen Vollbildzonen der gespeicherten Layouts, damit die Installation gleich
    /// die richtigen Modi mitbringt und der Treiber selten neu gestartet werden muss. Der erhöhte
    /// Prozess läuft unter demselben Konto und liest dieselbe <c>settings.json</c>.
    /// </summary>
    public static IReadOnlyList<VirtualDisplayMode> ModesForConfiguredZones()
    {
        try
        {
            var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SnapZones");
            var configuration = new JsonConfigurationRepository(appData).LoadAsync(CancellationToken.None).GetAwaiter().GetResult().Configuration;
            var monitors = new WindowsMonitorService().GetMonitors();
            return VirtualDisplayModes.Build(VirtualZoneSizes(configuration, monitors));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or System.ComponentModel.Win32Exception)
        {
            return VirtualDisplayModes.Build([]);
        }
    }

    /// <summary>Die Pixelgroessen aller Vollbildzonen; fuer nicht verbundene Monitore zaehlt die gemerkte Groesse.</summary>
    public static IEnumerable<VirtualDisplayMode> VirtualZoneSizes(SnapConfiguration configuration, IReadOnlyList<LiveMonitor> monitors)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(monitors);
        var metrics = new LayoutMetrics(configuration.Settings.EffectiveOuterMargins, configuration.Settings.ZoneGap);
        foreach (var layout in configuration.Layouts)
        {
            var live = monitors.FirstOrDefault(monitor => LayoutService.BelongsToMonitor(layout.Monitor, monitor.Identity));
            var workArea = live?.WorkArea ?? new MonitorWorkArea(0, 0, layout.SavedWidth, layout.SavedHeight);
            foreach (var zone in layout.Zones.Where(candidate => candidate.IsFullscreenZone))
            {
                var bounds = ZoneGeometry.ToPixels(zone.Bounds, workArea, metrics);
                yield return VirtualDisplayModes.Normalize(bounds.Width, bounds.Height);
            }
        }
    }
}
