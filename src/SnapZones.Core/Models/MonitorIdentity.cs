namespace SnapZones.Core.Models;

/// <param name="StableId">
/// Der Anzeigepfad von Windows. Er enthaelt Grafikkarte und Anschluss und aendert sich deshalb, wenn
/// derselbe Monitor an einem anderen Anschluss haengt.
/// </param>
/// <param name="DeviceName">Der GDI-Name (<c>\\.\DISPLAYn</c>); Windows vergibt ihn bei jedem Start neu.</param>
/// <param name="FriendlyName">Der Anzeigename aus dem Anzeigepfad oder dem Treiber.</param>
/// <param name="HardwareId">
/// Hersteller, Modell und, sofern vorhanden, Seriennummer aus der EDID des Monitors. Haengt nicht vom
/// Anschluss ab und erlaubt es, Layouts nach einem Umstecken wiederzuerkennen. Leer, wenn Windows keine
/// EDID liefert.
/// </param>
public sealed record MonitorIdentity(
    string StableId,
    string DeviceName,
    string FriendlyName,
    string HardwareId = "");
