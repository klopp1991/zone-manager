using SnapZones.Core.Editor;
using SnapZones.Core.Models;
using SnapZones.Core.Monitors;

namespace SnapZones.App.ViewModels;

/// <param name="IsConnected">
/// <c>false</c> für einen Monitor, der gerade nicht angeschlossen ist, für den aber noch mindestens ein
/// Layout gespeichert ist. Solche Monitore bleiben sichtbar, damit ihre Layouts gelöscht werden können
/// und nicht unerreichbar in der Konfiguration liegen bleiben.
/// </param>
public sealed record MonitorChoice(
    LiveMonitor Live,
    MonitorLayout Layout,
    int DisplayNumber = 1,
    string? CustomName = null,
    bool IsConnected = true)
{
    public string FriendlyName => Live.Identity.FriendlyName;
    public string ResolutionText => $"{Live.WorkArea.Width} × {Live.WorkArea.Height}";
    public string UserFacingName => MonitorNaming.UserFacingName(CustomName, DisplayNumber);

    public string DetailsText => IsConnected
        ? $"{FriendlyName} · {ResolutionText}"
        : $"{FriendlyName} · {ResolutionText} · nicht verbunden";

    /// <summary>Kurzer Hinweis für die Oberfläche, warum ein Monitor ohne Verbindung aufgeführt wird.</summary>
    public string? ConnectionNote => IsConnected
        ? null
        : "Nicht verbunden – wird nur noch angezeigt, weil Layouts dafür gespeichert sind.";

    public string DisplayName => $"{UserFacingName} · {DetailsText}";

    public IReadOnlyList<LayoutSuggestion> LayoutSuggestions => LayoutSuggestionSelector.Recommend(
        new LayoutSuggestionContext(
            Live.WorkArea.Width,
            Live.WorkArea.Height,
            Live.DpiX,
            Live.DpiY,
            Live.PhysicalWidthCentimeters,
            Live.PhysicalHeightCentimeters));
}
