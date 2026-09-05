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
    /// <summary>Alle Layouts dieses Monitors, für die Auswahl «Aktives Layout» auf der Übersichtskarte.</summary>
    public IReadOnlyList<MonitorLayout> Layouts { get; init; } = [];

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

    /// <summary>Die Windows-Skalierung in Prozent, aus der gemeldeten DPI.</summary>
    public int ScalePercent => (int)Math.Round(Live.DpiX / 96d * 100);

    /// <summary>Kopfzeile der Übersichtskarte: «5120 × 2160 · 125 %», bei fehlender Verbindung mit Hinweis.</summary>
    public string OverviewDetailsText
    {
        get
        {
            var bounds = Live.MonitorBounds;
            var text = $"{bounds.Width} × {bounds.Height} · {ScalePercent} %";
            return IsConnected ? text : $"{text} · nicht verbunden";
        }
    }

    /// <summary>Seitenverhältnis der Arbeitsfläche für die Vorschauen.</summary>
    public double AspectRatio => Live.WorkArea.Height > 0
        ? (double)Live.WorkArea.Width / Live.WorkArea.Height
        : 16d / 9d;

    public string LayoutCountText => Layouts.Count == 1 ? "1 Layout" : $"{Layouts.Count} Layouts";

    public IReadOnlyList<LayoutSuggestion> LayoutSuggestions => LayoutSuggestionSelector.Recommend(
        new LayoutSuggestionContext(
            Live.WorkArea.Width,
            Live.WorkArea.Height,
            Live.DpiX,
            Live.DpiY,
            Live.PhysicalWidthCentimeters,
            Live.PhysicalHeightCentimeters));
}
