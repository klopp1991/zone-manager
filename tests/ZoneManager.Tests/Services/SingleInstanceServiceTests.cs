using System.Reflection;
using ZoneManager.App.Services;
using Xunit;

namespace ZoneManager.Tests.Services;

public sealed class SingleInstanceServiceTests
{
    [Fact]
    public void UsesThreadPoolWaitInsteadOfDedicatedListenerTask()
    {
        var dedicatedListener = typeof(SingleInstanceService).GetField(
            "listenerTask",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.Null(dedicatedListener);
    }

    [Fact]
    public void SecondaryInstanceActivatesPrimaryThroughSynchronizationContext()
    {
        var context = new RecordingSynchronizationContext();
        var instanceName = $"ZoneManager.Tests.{Guid.NewGuid():N}";
        using var primary = new SingleInstanceService(instanceName, context);
        using var secondary = new SingleInstanceService(instanceName, context);
        using var activated = new ManualResetEventSlim();
        primary.ActivationRequested += activated.Set;

        primary.StartListening();
        secondary.NotifyPrimary();

        Assert.True(activated.Wait(TimeSpan.FromSeconds(2)));
        Assert.Equal(1, context.PostCount);
    }

    private sealed class RecordingSynchronizationContext : SynchronizationContext
    {
        private int postCount;

        public int PostCount => Volatile.Read(ref postCount);

        public override void Post(SendOrPostCallback callback, object? state)
        {
            Interlocked.Increment(ref postCount);
            callback(state);
        }
    }
}
