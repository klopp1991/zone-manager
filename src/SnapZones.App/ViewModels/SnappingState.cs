namespace SnapZones.App.ViewModels;

/// <summary>Was die Snap-Funktion gerade tut, in drei Worten fuer Statuszeile und Infobereich.</summary>
public enum SnappingState
{
    /// <summary>Kein Layout ist aktiv; es gibt nichts, wohin ein Fenster einrasten koennte.</summary>
    NoActiveLayout,

    /// <summary>Mindestens ein Layout ist aktiv, Hooks und Overlays laufen.</summary>
    Active,

    /// <summary>Ein Not-Aus oder ein Sicherheitsstopp hat Hooks und Overlays stillgelegt.</summary>
    Paused
}
