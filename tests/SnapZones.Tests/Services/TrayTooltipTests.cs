using SnapZones.App.Services;
using Xunit;

namespace SnapZones.Tests.Services;

public sealed class TrayTooltipTests
{
    [Fact]
    public void The_unrestricted_tooltip_names_the_monitor_count()
    {
        Assert.Equal("Sascha’s Zone Manager · 2 Monitore", TrayTooltip.Build("Sascha’s Zone Manager", 2, false));
    }

    [Fact]
    public void The_restricted_tooltip_marks_the_limited_mode()
    {
        var tooltip = TrayTooltip.Build("Sascha’s Zone Manager", 2, true);

        Assert.Contains("eingeschränkt", tooltip, StringComparison.Ordinal);
        Assert.True(tooltip.Length <= TrayTooltip.MaximumLength);
    }

    [Fact]
    public void Long_names_stay_within_the_windows_limit()
    {
        var tooltip = TrayTooltip.Build(new string('N', 80), 3, true);

        Assert.Equal(TrayTooltip.MaximumLength, tooltip.Length);
        Assert.EndsWith("…", tooltip, StringComparison.Ordinal);
    }
}
