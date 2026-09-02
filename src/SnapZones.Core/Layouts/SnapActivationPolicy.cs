using SnapZones.Core.Models;

namespace SnapZones.Core.Layouts;

public static class SnapActivationPolicy
{
    public static bool ShouldEnable(SnapConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return configuration.Layouts.Any(layout => layout.IsActive);
    }
}
