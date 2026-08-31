using SnapZones.Core.AppRules;

namespace SnapZones.Core.Drag;

public static class WindowCandidateEvaluator
{
    public static bool IsEligible(WindowSnapshot snapshot) => IsEligible(snapshot, null);

    /// <summary>
    /// Ein Fenster kommt zum Einrasten in Frage, wenn es sichtbar und eigenständig ist, an der
    /// Titelleiste gezogen wird und von keinem Ausschluss erfasst ist. Ein ausgeschlossenes Fenster
    /// bekommt schon kein Overlay zu sehen und bleibt damit vollständig frei beweglich.
    /// </summary>
    public static bool IsEligible(WindowSnapshot snapshot, IReadOnlyList<AppExclusion>? exclusions)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return snapshot.IsVisible &&
            !snapshot.IsChild &&
            !snapshot.IsToolWindow &&
            !snapshot.IsCloaked &&
            snapshot.IsTitleBarDrag &&
            !AppExclusionMatcher.IsExcluded(exclusions, snapshot.Identity);
    }
}
