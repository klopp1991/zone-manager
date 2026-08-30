using SnapZones.Core.Models;
using SnapZones.Core.Monitors;

namespace SnapZones.App.ViewModels;

public sealed record MonitorChoice(LiveMonitor Live, MonitorLayout Layout)
{
    public string FriendlyName => Live.Identity.FriendlyName;
    public int WindowsScalePercent => (int)Math.Round(Live.DpiX / 96d * 100);
    public string ResolutionText =>
        $"{Live.WorkArea.Width} × {Live.WorkArea.Height}  |  Windows-Skalierung {WindowsScalePercent} %";
    public string DisplayName => $"{FriendlyName} · {ResolutionText}";
}
