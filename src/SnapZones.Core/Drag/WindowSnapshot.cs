using SnapZones.Core.AppRules;

namespace SnapZones.Core.Drag;

public sealed record WindowSnapshot(
    bool IsVisible,
    bool IsChild,
    bool IsOwnProcess,
    bool IsToolWindow,
    bool IsCloaked,
    bool IsTitleBarDrag,
    /// <summary>
    /// Programm, Titel und Klasse des Fensters, sofern lesbar. Wird gebraucht, um Ausschlüsse schon
    /// beim Ziehstart auszuwerten; ohne Identität kann kein Ausschluss greifen.
    /// </summary>
    AppWindowIdentity? Identity = null);
