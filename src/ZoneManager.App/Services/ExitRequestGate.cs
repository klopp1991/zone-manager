namespace ZoneManager.App.Services;

public sealed class ExitRequestGate
{
    private int requested;

    public void Request(Action exitAction)
    {
        ArgumentNullException.ThrowIfNull(exitAction);
        if (Interlocked.Exchange(ref requested, 1) == 0)
        {
            exitAction();
        }
    }

    public void Reset() => Interlocked.Exchange(ref requested, 0);
}
