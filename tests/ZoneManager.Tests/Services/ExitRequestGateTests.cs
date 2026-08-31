using ZoneManager.App.Services;
using Xunit;

namespace ZoneManager.Tests.Services;

public sealed class ExitRequestGateTests
{
    [Fact]
    public void Request_executes_the_exit_action_immediately_and_only_once()
    {
        var gate = new ExitRequestGate();
        var invocationCount = 0;

        gate.Request(() => invocationCount++);
        gate.Request(() => invocationCount++);

        Assert.Equal(1, invocationCount);
    }

    [Fact]
    public void Reset_allows_a_new_exit_request_after_a_failed_shutdown_preparation()
    {
        var gate = new ExitRequestGate();
        var invocationCount = 0;

        gate.Request(() => invocationCount++);
        gate.Reset();
        gate.Request(() => invocationCount++);

        Assert.Equal(2, invocationCount);
    }
}
