namespace SnapZones.Windows.Native;

internal interface IWinEventHookApi
{
    nint SetWinEventHook(
        uint eventMinimum,
        uint eventMaximum,
        nint module,
        User32.WinEventProc callback,
        uint processId,
        uint threadId,
        uint flags);

    bool UnhookWinEvent(nint hook);
}

internal sealed class User32WinEventHookApi : IWinEventHookApi
{
    public nint SetWinEventHook(
        uint eventMinimum,
        uint eventMaximum,
        nint module,
        User32.WinEventProc callback,
        uint processId,
        uint threadId,
        uint flags) =>
        User32.SetWinEventHook(eventMinimum, eventMaximum, module, callback, processId, threadId, flags);

    public bool UnhookWinEvent(nint hook) => User32.UnhookWinEvent(hook);
}
