namespace SnapZones.App.Services;

public sealed class DpiProbeLifetime
{
    private readonly TimeSpan delay;
    private readonly Action<TimeSpan, Action> schedule;
    private readonly Action shutdown;
    private bool started;
    private bool shutdownRequested;

    public DpiProbeLifetime(TimeSpan delay, Action<TimeSpan, Action> schedule, Action shutdown)
    {
        this.delay = delay;
        this.schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
        this.shutdown = shutdown ?? throw new ArgumentNullException(nameof(shutdown));
    }

    public void Start()
    {
        if (started)
        {
            return;
        }

        started = true;
        schedule(delay, RequestShutdown);
    }

    private void RequestShutdown()
    {
        if (shutdownRequested)
        {
            return;
        }

        shutdownRequested = true;
        shutdown();
    }
}
