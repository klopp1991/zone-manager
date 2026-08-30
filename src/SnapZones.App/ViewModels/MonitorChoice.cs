using SnapZones.Core.Models;
using SnapZones.Core.Monitors;

namespace SnapZones.App.ViewModels;

public sealed record MonitorChoice(LiveMonitor Live, MonitorLayout Layout)
{
    public string DisplayName => $"{Live.Identity.FriendlyName}  ·  {Live.WorkArea.Width} × {Live.WorkArea.Height}";
}
