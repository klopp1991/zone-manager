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
}
