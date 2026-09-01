namespace SnapZones.Core.Models;

/// <summary>
/// Wann sich das Programm Administratorrechte holt.
///
/// Windows lässt ein Programm nur solche Fenster verschieben, die derselben oder einer niedrigeren
/// Vertrauensstufe angehören. Alltägliche Fenster — Browser, Editor, Explorer — sind das; der
/// Taskmanager, die Registrierungs-Editor und alles «als Administrator» Gestartete nicht.
/// </summary>
public enum ElevationMode
{
    /// <summary>
    /// Voreinstellung. Das Programm startet mit gewöhnlichen Rechten und fragt erst dann nach, wenn es
    /// tatsächlich auf ein höher berechtigtes Fenster trifft. In den meisten Sitzungen erscheint gar
    /// keine Abfrage, und ein Fehler im Programm kann sich nicht zu Administratorrechten ausweiten.
    /// </summary>
    WhenNeeded,

    /// <summary>
    /// Das bisherige Verhalten: jeder Start geht über die Windows-UAC-Abfrage. Wer täglich Fenster
    /// höher berechtigter Programme einrastet, spart sich damit die Nachfrage im Betrieb.
    /// </summary>
    Always
}
