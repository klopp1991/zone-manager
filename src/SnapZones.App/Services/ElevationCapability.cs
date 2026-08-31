namespace SnapZones.App.Services;

public enum ElevationCapabilityReason
{
    AlreadyElevated,
    SessionWithoutPrompt,
    NoAdministratorMembership,
    UserAccountControlDisabled,
    PromptAvailable
}

/// <summary>
/// Ergebnis der Vorprüfung, ob sich der Prozess überhaupt selbst erhöhen kann.
/// Die Prüfung arbeitet nur mit Token- und Registrywerten und startet keinen Prozess.
/// </summary>
public sealed record ElevationCapability(
    bool IsElevated,
    bool CanElevate,
    ElevationCapabilityReason Reason,
    string Description)
{
    public static ElevationCapability Inspect(
        bool isElevated,
        bool isAdministratorMember,
        bool isUserAccountControlEnabled,
        bool isInteractiveSession)
    {
        if (isElevated)
        {
            return new ElevationCapability(
                IsElevated: true,
                CanElevate: true,
                ElevationCapabilityReason.AlreadyElevated,
                "Der Prozess läuft bereits mit Administratorrechten.");
        }

        if (!isInteractiveSession)
        {
            return new ElevationCapability(
                IsElevated: false,
                CanElevate: false,
                ElevationCapabilityReason.SessionWithoutPrompt,
                "In dieser Sitzung kann keine Abfrage der Benutzerkontensteuerung angezeigt werden.");
        }

        if (!isAdministratorMember)
        {
            return new ElevationCapability(
                IsElevated: false,
                CanElevate: false,
                ElevationCapabilityReason.NoAdministratorMembership,
                "Das angemeldete Konto gehört nicht zur Gruppe der lokalen Administratoren.");
        }

        if (!isUserAccountControlEnabled)
        {
            return new ElevationCapability(
                IsElevated: false,
                CanElevate: false,
                ElevationCapabilityReason.UserAccountControlDisabled,
                "Die Benutzerkontensteuerung ist ausgeschaltet; ohne sie kann sich der Prozess nicht selbst erhöhen.");
        }

        return new ElevationCapability(
            IsElevated: false,
            CanElevate: true,
            ElevationCapabilityReason.PromptAvailable,
            "Eine Erhöhung über die Benutzerkontensteuerung ist möglich.");
    }
}
