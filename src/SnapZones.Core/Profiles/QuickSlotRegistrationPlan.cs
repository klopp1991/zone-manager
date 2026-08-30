using SnapZones.Core.Models;

namespace SnapZones.Core.Profiles;

public sealed record QuickSlotRegistration(Guid ProfileId, int Slot, string ProfileName);

public sealed record QuickSlotRegistrationError(int Slot, string Message);

public sealed record QuickSlotRegistrationPlanResult(
    IReadOnlyList<QuickSlotRegistration> Registrations,
    IReadOnlyList<QuickSlotRegistrationError> Errors);

public static class QuickSlotRegistrationPlan
{
    public static QuickSlotRegistrationPlanResult Build(SnapConfiguration configuration)
    {
        var registrations = new List<QuickSlotRegistration>();
        var errors = new List<QuickSlotRegistrationError>();
        foreach (var group in configuration.Profiles
                     .Where(profile => profile.QuickSlot.HasValue)
                     .GroupBy(profile => profile.QuickSlot!.Value)
                     .OrderBy(group => group.Key))
        {
            if (group.Key is < 1 or > 9)
            {
                errors.Add(new QuickSlotRegistrationError(group.Key, "Der Schnellwahlplatz liegt ausserhalb von 1 bis 9."));
            }
            else if (group.Count() > 1)
            {
                errors.Add(new QuickSlotRegistrationError(group.Key, "Der Schnellwahlplatz ist mehrfach belegt."));
            }
            else
            {
                var profile = group.Single();
                registrations.Add(new QuickSlotRegistration(profile.Id, group.Key, profile.Name));
            }
        }

        return new QuickSlotRegistrationPlanResult(registrations, errors);
    }
}
