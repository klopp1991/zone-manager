using SnapZones.Core.Drag;
using Xunit;

namespace SnapZones.Tests.Drag;

/// <summary>
/// Ein Sicherheitsstopp wegen der Ereignisgrenze hebt sich nach kurzer Ruhe von selbst auf; gehäufte
/// Stopps und Stopps nach einem Fehler bleiben stehen, bis jemand nachsieht.
/// </summary>
public sealed class HookRecoveryPolicyTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 5, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_circuit_breaker_names_the_rate_limit_distinctly()
    {
        var breaker = new HookCircuitBreaker(2, TimeSpan.FromSeconds(10));
        breaker.RecordEvent(Start);
        breaker.RecordEvent(Start);
        breaker.RecordEvent(Start);

        Assert.True(breaker.IsTripped);
        Assert.True(HookCircuitBreaker.IsRateLimit(breaker.Reason));

        breaker.Reset();
        breaker.Trip(new InvalidOperationException("kaputt"));
        Assert.False(HookCircuitBreaker.IsRateLimit(breaker.Reason));
        Assert.False(HookCircuitBreaker.IsRateLimit(null));
    }

    [Fact]
    public void A_rate_limit_stop_resumes_after_the_delay_but_repeated_stops_stay_put()
    {
        var policy = new HookRecoveryPolicy(2, TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(10));

        Assert.Equal(TimeSpan.FromSeconds(10), policy.Decide(HookCircuitBreaker.RateLimitReason, Start));
        Assert.Equal(TimeSpan.FromSeconds(10), policy.Decide(HookCircuitBreaker.RateLimitReason, Start.AddMinutes(1)));
        Assert.Null(policy.Decide(HookCircuitBreaker.RateLimitReason, Start.AddMinutes(2)));

        // Nach Ablauf des Zaehlfensters beginnt die Zaehlung von vorn.
        Assert.Equal(TimeSpan.FromSeconds(10), policy.Decide(HookCircuitBreaker.RateLimitReason, Start.AddMinutes(8)));
    }

    [Fact]
    public void A_stop_after_an_error_is_never_lifted_automatically()
    {
        var policy = HookRecoveryPolicy.Default;

        Assert.Null(policy.Decide("Der Hook wurde nach einem Fehler deaktiviert: kaputt", Start));
        Assert.Null(policy.Decide(null, Start));
    }

    [Fact]
    public void The_reason_may_be_wrapped_in_a_longer_message()
    {
        var policy = HookRecoveryPolicy.Default;

        Assert.NotNull(policy.Decide($"Fensterplatzierungs-Hook gestoppt: {HookCircuitBreaker.RateLimitReason}", Start));
    }
}
