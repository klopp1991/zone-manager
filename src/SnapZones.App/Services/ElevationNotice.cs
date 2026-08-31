namespace SnapZones.App.Services;

/// <summary>
/// Erzeugt die Texte für den eingeschränkten Betrieb ohne Administratorrechte.
/// Reine Textlogik, damit der Hinweis ohne Oberfläche prüfbar bleibt.
/// </summary>
public static class ElevationNotice
{
    public const string RestrictionSummary =
        "Ohne Administratorrechte können Fenster von Programmen mit höheren Rechten nicht positioniert werden.";

    public const string TraySuffix = "eingeschränkt ohne Administratorrechte";

    /// <summary>
    /// Hinweis für das Banner der Oberfläche. <c>null</c>, solange der Prozess erhöht läuft.
    /// </summary>
    public static string? BuildBanner(ElevationCapability capability, string? startupNotice)
    {
        ArgumentNullException.ThrowIfNull(capability);
        if (capability.IsElevated)
        {
            return null;
        }

        var reason = string.IsNullOrWhiteSpace(startupNotice) ? capability.Description : startupNotice;
        var retryHint = capability.CanElevate
            ? "Ein erneuter Versuch mit Administratorrechten ist über die Schaltfläche möglich."
            : "Ein erneuter Versuch ist in dieser Sitzung nicht möglich.";
        return $"{RestrictionSummary} {reason} {retryHint}";
    }

    /// <summary>
    /// Erklärung für eine fehlgeschlagene Platzierung. <c>null</c>, wenn die fehlenden Rechte nicht die Ursache sein können.
    /// </summary>
    public static string? DescribePlacementFailure(ElevationCapability capability, string context)
    {
        ArgumentNullException.ThrowIfNull(capability);
        ArgumentException.ThrowIfNullOrWhiteSpace(context);
        return capability.IsElevated
            ? null
            : $"{context}: {RestrictionSummary}";
    }
}
