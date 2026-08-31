namespace SnapZones.App;

internal static class ProductInfo
{
    public const string Name = "Sascha’s Zone Manager";

    // Schlüssel der Einzelinstanz (Mutex und Aktivierungsereignis), nicht der Prozessname.
    public const string InstanceKey = "SaschaWindowZones";
}
