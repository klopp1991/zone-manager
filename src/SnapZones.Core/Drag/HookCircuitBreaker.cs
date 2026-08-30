namespace SnapZones.Core.Drag;

public sealed class HookCircuitBreaker
{
    private readonly int maximumEvents;
    private readonly TimeSpan window;
    private readonly Queue<DateTimeOffset> events = new();

    public HookCircuitBreaker(int maximumEvents, TimeSpan window)
    {
        if (maximumEvents < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumEvents));
        }

        if (window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(window));
        }

        this.maximumEvents = maximumEvents;
        this.window = window;
    }

    public bool IsTripped { get; private set; }
    public string? Reason { get; private set; }

    public bool RecordEvent(DateTimeOffset timestamp)
    {
        if (IsTripped)
        {
            return true;
        }

        while (events.Count > 0 && timestamp - events.Peek() > window)
        {
            events.Dequeue();
        }

        events.Enqueue(timestamp);
        if (events.Count > maximumEvents)
        {
            Trip(null);
        }

        return IsTripped;
    }

    public void Trip(Exception? exception)
    {
        IsTripped = true;
        Reason = exception is null
            ? "Die Ereignisgrenze wurde überschritten."
            : $"Der Hook wurde nach einem Fehler deaktiviert: {exception.Message}";
    }
}
