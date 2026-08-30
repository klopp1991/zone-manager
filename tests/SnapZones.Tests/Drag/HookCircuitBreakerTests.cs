using SnapZones.Core.Drag;
using Xunit;

namespace SnapZones.Tests.Drag;

public sealed class HookCircuitBreakerTests
{
    [Fact]
    public void RecordEvent_trips_on_101st_event_inside_ten_seconds()
    {
        var breaker = new HookCircuitBreaker(100, TimeSpan.FromSeconds(10));
        var start = DateTimeOffset.Parse("2026-08-30T08:00:00Z");

        for (var index = 0; index < 100; index++)
        {
            Assert.False(breaker.RecordEvent(start.AddMilliseconds(index * 20)));
        }

        Assert.True(breaker.RecordEvent(start.AddSeconds(2)));
        Assert.True(breaker.IsTripped);
    }

    [Fact]
    public void Trip_marks_breaker_with_exception_reason()
    {
        var breaker = new HookCircuitBreaker(100, TimeSpan.FromSeconds(10));

        breaker.Trip(new InvalidOperationException("Callbackfehler"));

        Assert.True(breaker.IsTripped);
        Assert.Contains("Callbackfehler", breaker.Reason);
    }

    [Fact]
    public void RecordEvent_discards_events_outside_time_window()
    {
        var breaker = new HookCircuitBreaker(2, TimeSpan.FromSeconds(10));
        var start = DateTimeOffset.Parse("2026-08-30T08:00:00Z");
        Assert.False(breaker.RecordEvent(start));
        Assert.False(breaker.RecordEvent(start.AddSeconds(11)));

        Assert.False(breaker.RecordEvent(start.AddSeconds(12)));
    }
}
