using SnapZones.App.Services;
using Xunit;

namespace SnapZones.Tests.Services;

public sealed class ElevationCapabilityTests
{
    [Fact]
    public void An_elevated_process_needs_no_further_elevation()
    {
        var capability = ElevationCapability.Inspect(
            isElevated: true,
            isAdministratorMember: true,
            isUserAccountControlEnabled: true,
            isInteractiveSession: true);

        Assert.True(capability.IsElevated);
        Assert.True(capability.CanElevate);
        Assert.Equal(ElevationCapabilityReason.AlreadyElevated, capability.Reason);
    }

    [Fact]
    public void An_elevated_process_stays_elevated_even_without_an_interactive_session()
    {
        var capability = ElevationCapability.Inspect(
            isElevated: true,
            isAdministratorMember: true,
            isUserAccountControlEnabled: true,
            isInteractiveSession: false);

        Assert.True(capability.IsElevated);
        Assert.Equal(ElevationCapabilityReason.AlreadyElevated, capability.Reason);
    }

    [Fact]
    public void A_session_without_a_prompt_cannot_elevate()
    {
        var capability = ElevationCapability.Inspect(
            isElevated: false,
            isAdministratorMember: true,
            isUserAccountControlEnabled: true,
            isInteractiveSession: false);

        Assert.False(capability.CanElevate);
        Assert.Equal(ElevationCapabilityReason.SessionWithoutPrompt, capability.Reason);
    }

    [Fact]
    public void A_standard_user_cannot_elevate()
    {
        var capability = ElevationCapability.Inspect(
            isElevated: false,
            isAdministratorMember: false,
            isUserAccountControlEnabled: true,
            isInteractiveSession: true);

        Assert.False(capability.CanElevate);
        Assert.Equal(ElevationCapabilityReason.NoAdministratorMembership, capability.Reason);
    }

    [Fact]
    public void A_disabled_user_account_control_prevents_self_elevation()
    {
        var capability = ElevationCapability.Inspect(
            isElevated: false,
            isAdministratorMember: true,
            isUserAccountControlEnabled: false,
            isInteractiveSession: true);

        Assert.False(capability.CanElevate);
        Assert.Equal(ElevationCapabilityReason.UserAccountControlDisabled, capability.Reason);
    }

    [Fact]
    public void An_administrator_with_an_active_prompt_can_elevate()
    {
        var capability = ElevationCapability.Inspect(
            isElevated: false,
            isAdministratorMember: true,
            isUserAccountControlEnabled: true,
            isInteractiveSession: true);

        Assert.False(capability.IsElevated);
        Assert.True(capability.CanElevate);
        Assert.Equal(ElevationCapabilityReason.PromptAvailable, capability.Reason);
    }

    [Fact]
    public void Every_branch_explains_itself()
    {
        foreach (var capability in new[]
        {
            ElevationCapability.Inspect(true, true, true, true),
            ElevationCapability.Inspect(false, true, true, false),
            ElevationCapability.Inspect(false, false, true, true),
            ElevationCapability.Inspect(false, true, false, true),
            ElevationCapability.Inspect(false, true, true, true)
        })
        {
            Assert.False(string.IsNullOrWhiteSpace(capability.Description));
        }
    }
}
