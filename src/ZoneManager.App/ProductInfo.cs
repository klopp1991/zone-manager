namespace ZoneManager.App;

internal static class ProductInfo
{
    public const string Name = "Sascha’s Zone Manager";

    // Schlüssel der Einzelinstanz (Mutex und Aktivierungsereignis), nicht der Prozessname.
    // Eine gleichzeitig laufende Altversion mit dem Schlüssel "SaschaWindowZones" wird dadurch
    // nicht mehr als zweite Instanz erkannt.
    public const string InstanceKey = "ZoneManager";

    /// <summary>Ordnername unter %APPDATA% und %LOCALAPPDATA%.</summary>
    public const string DataFolderName = "ZoneManager";

    /// <summary>Bisheriger Ordnername; bleibt nach der Übernahme als Rückfallebene bestehen.</summary>
    public const string LegacyDataFolderName = "SnapZones";
}
