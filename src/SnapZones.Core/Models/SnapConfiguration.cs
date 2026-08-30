namespace SnapZones.Core.Models;

public sealed record SnapConfiguration(
    int SchemaVersion,
    AppSettings Settings,
    IReadOnlyList<LayoutProfile> Profiles)
{
    public const int CurrentSchemaVersion = 1;

    public static SnapConfiguration CreateDefault()
    {
        var profileId = Guid.NewGuid();
        var profile = new LayoutProfile(profileId, "Standard", 1, []);
        return new SnapConfiguration(
            CurrentSchemaVersion,
            AppSettings.Default(profileId),
            [profile]);
    }
}
