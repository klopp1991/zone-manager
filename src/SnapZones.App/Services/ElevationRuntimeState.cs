namespace SnapZones.App.Services;

/// <summary>
/// Zustand der Rechteprüfung für die laufende Sitzung: Ergebnis der Vorprüfung und der Grund,
/// weshalb die Anwendung gegebenenfalls unerhöht läuft.
/// </summary>
public sealed record ElevationRuntimeState(ElevationCapability Capability, string? StartupNotice)
{
    public bool IsRestricted => !Capability.IsElevated;

    public bool CanRetry => !Capability.IsElevated && Capability.CanElevate;

    public string? Banner => ElevationNotice.BuildBanner(Capability, StartupNotice);

    public static ElevationRuntimeState Unknown { get; } = new(
        ElevationCapability.Inspect(
            isElevated: false,
            isAdministratorMember: false,
            isUserAccountControlEnabled: true,
            isInteractiveSession: false),
        StartupNotice: null);
}
