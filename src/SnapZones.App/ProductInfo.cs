using System.Reflection;

namespace SnapZones.App;

internal static class ProductInfo
{
    public const string Name = "Zone Manager";

    /// <summary>Urheber des Programms; erscheint in der Statuszeile und im Installationseintrag.</summary>
    public const string Author = "Sascha Krähenbühl";

    /// <summary>
    /// Identitaet der Einzelinstanz fuer Mutex und Aktivierungsereignis; zugleich der Bezeichner, mit
    /// dem sich die Updatesuche als User-Agent meldet. Bewusst nicht der Prozessname.
    /// </summary>
    public const string InstanceKey = "ZoneManager";

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
