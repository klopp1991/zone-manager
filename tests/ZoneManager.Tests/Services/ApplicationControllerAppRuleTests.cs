using ZoneManager.App.Services;
using ZoneManager.Tests.Support;
using Xunit;

namespace ZoneManager.Tests.Services;

public sealed class ApplicationControllerAppRuleTests
{
    [Fact]
    public void Newly_activated_layouts_are_detected_without_retriggering_unchanged_layouts()
    {
        var previous = ConfigurationSamples.TwoLayouts();
        var activatedId = previous.Layouts[1].Id;
        var current = previous with
        {
            Layouts = previous.Layouts
                .Select(layout => layout with { IsActive = layout.Id == activatedId })
                .ToArray()
        };

        var changed = ApplicationController.FindNewlyActivatedLayoutIds(previous, current);
        var unchanged = ApplicationController.FindNewlyActivatedLayoutIds(current, current);

        Assert.Equal([activatedId], changed);
        Assert.Empty(unchanged);
    }
}
