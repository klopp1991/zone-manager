using SnapZones.Core.Models;
using SnapZones.Core.Profiles;
using Xunit;

namespace SnapZones.Tests.Profiles;

public sealed class QuickSlotRegistrationPlanTests
{
    [Fact]
    public void Build_registers_only_unique_valid_slots_and_reports_conflicts()
    {
        var firstId = Guid.NewGuid();
        var configuration = new SnapConfiguration(
            SnapConfiguration.CurrentSchemaVersion,
            AppSettings.Default(firstId),
            [
                new LayoutProfile(firstId, "Arbeit", 1, []),
                new LayoutProfile(Guid.NewGuid(), "Abend", 2, []),
                new LayoutProfile(Guid.NewGuid(), "Präsentation", 2, []),
                new LayoutProfile(Guid.NewGuid(), "Ohne", null, [])
            ]);

        var result = QuickSlotRegistrationPlan.Build(configuration);

        var registration = Assert.Single(result.Registrations);
        Assert.Equal(1, registration.Slot);
        var error = Assert.Single(result.Errors);
        Assert.Equal(2, error.Slot);
    }
}
