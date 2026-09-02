namespace SnapZones.Core.Placement;

/// <summary>
/// Anlass, aus dem das Platzierungs-Modul ein Fenster betrachtet. Zurueckgelegt wird ein Fenster nur
/// beim Erscheinen; der Fokuswechsel dient allein dazu, seine aktuelle Lage aufzunehmen.
/// </summary>
public enum WindowPlacementTrigger
{
    WindowCreated,
    WindowFocused
}
