using System.Reflection;

namespace SnapZones.App;

internal static class ProductInfo
{
    public const string Name = "Sascha’s Zone Manager";

    /// <summary>
    /// Identitaet der Einzelinstanz fuer Mutex und Aktivierungsereignis. Der Wert bleibt auf dem
    /// historischen Namen, damit eine laufende aeltere Installation weiterhin erkannt wird; er ist
    /// bewusst nicht der Prozessname, der seit der Umbenennung <c>ZoneManager</c> lautet.
    /// </summary>
    public const string InstanceKey = "SaschaWindowZones";

    /// <summary>
    /// Produktversion im Schema YYYY.MMDD.NN, gesetzt von scripts/set-version.ps1 ueber
    /// Directory.Build.props. Nur die InformationalVersion traegt die fuehrenden Nullen; ein vom
    /// SDK angehaengtes Metadatensuffix wird entfernt.
    /// </summary>
    public static string Version { get; } = ResolveVersion();

    private static string ResolveVersion()
    {
        var informational = typeof(ProductInfo).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(informational))
        {
            return typeof(ProductInfo).Assembly.GetName().Version?.ToString() ?? "0.0.0";
        }

        var separatorIndex = informational.IndexOf('+');
        return separatorIndex < 0 ? informational : informational[..separatorIndex];
    }
}
