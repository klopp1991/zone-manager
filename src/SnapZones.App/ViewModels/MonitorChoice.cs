using SnapZones.Core.Editor;
using SnapZones.Core.Models;
using SnapZones.Core.Monitors;

namespace SnapZones.App.ViewModels;

public sealed record MonitorChoice(
    LiveMonitor Live,
    MonitorLayout Layout,
    int DisplayNumber = 1,
    string? CustomName = null)
{
    public string FriendlyName => Live.Identity.FriendlyName;
    public string ResolutionText => $"{Live.WorkArea.Width} × {Live.WorkArea.Height}";
    public string UserFacingName => MonitorNaming.UserFacingName(CustomName, DisplayNumber);
    public string DetailsText => $"{FriendlyName} · {ResolutionText}";
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
