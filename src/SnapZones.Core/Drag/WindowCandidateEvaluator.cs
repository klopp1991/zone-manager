namespace SnapZones.Core.Drag;

public static class WindowCandidateEvaluator
{
    public static bool IsEligible(WindowSnapshot snapshot) =>
        snapshot.IsVisible &&
        !snapshot.IsChild &&
        !snapshot.IsToolWindow &&
        !snapshot.IsCloaked &&
        snapshot.IsTitleBarDrag;
}
