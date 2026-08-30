using System.Globalization;
using System.IO;
using SnapZones.Core.Models;
using SnapZones.Core.Placement;

namespace SnapZones.App.ViewModels;

public sealed class WindowPlacementItemViewModel
{
    public WindowPlacementItemViewModel(
        WindowPlacementEntry entry,
        IReadOnlyList<WindowPlacementRule> rules,
        IReadOnlyList<LayoutProfile> profiles,
        IReadOnlyList<MonitorChoice> monitors)
    {
        Entry = entry ?? throw new ArgumentNullException(nameof(entry));
        Identity = entry.Identity;
        DisplayName = GetDisplayName(Identity.ApplicationKey);
        WindowKindText = WindowKindTextFor(Identity.Kind);
        PlacementText = BuildPlacementText(entry, profiles, monitors);
        LastUpdatedText = entry.LastUpdatedUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
        RuleStatusText = BuildRuleStatus(Identity, rules, profiles);
    }

    public WindowPlacementEntry Entry { get; }
    public WindowIdentity Identity { get; }
    public string DisplayName { get; }
    public string WindowKindText { get; }
    public string PlacementText { get; }
    public string LastUpdatedText { get; }
    public string RuleStatusText { get; }

    public static string WindowKindTextFor(WindowKind kind) => kind switch
    {
        WindowKind.MainWindow => "Hauptfenster",
        WindowKind.Dialog => "Dialog",
        _ => kind.ToString()
    };

    private static string GetDisplayName(string applicationKey)
    {
        var fileName = Path.GetFileNameWithoutExtension(applicationKey);
        return string.IsNullOrWhiteSpace(fileName) ? applicationKey : fileName;
    }

    private static string BuildPlacementText(
        WindowPlacementEntry entry,
        IReadOnlyList<LayoutProfile> profiles,
        IReadOnlyList<MonitorChoice> monitors)
    {
        var monitorName = monitors
            .FirstOrDefault(monitor => string.Equals(
                monitor.Live.Identity.StableId,
                entry.MonitorStableId,
                StringComparison.OrdinalIgnoreCase))
            ?.FriendlyName ?? entry.MonitorStableId;
        var zoneName = entry.ZoneId is { } zoneId
            ? profiles
                .SelectMany(profile => profile.Monitors)
                .Where(layout => string.Equals(
                    layout.Monitor.StableId,
                    entry.MonitorStableId,
                    StringComparison.OrdinalIgnoreCase))
                .SelectMany(layout => layout.Zones)
                .FirstOrDefault(zone => zone.Id == zoneId)
                ?.Name ?? "Zielzone fehlt"
            : "Letzte Position";
        var maximized = entry.WasMaximized ? " · maximiert" : string.Empty;
        return $"{monitorName} · {zoneName}{maximized}";
    }

    private static string BuildRuleStatus(
        WindowIdentity identity,
        IReadOnlyList<WindowPlacementRule> rules,
        IReadOnlyList<LayoutProfile> profiles)
    {
        var exact = rules.Where(rule => rule.IsEnabled && IsExactIdentityRule(rule, identity)).ToArray();
        if (exact.Length > 1)
        {
            return "Regelkonflikt: mehrere gleich spezifische Regeln";
        }

        var resolution = exact.Length == 1
            ? new RuleResolution(exact[0], false)
            : PlacementRuleResolver.Resolve(identity, string.Empty, rules);
        if (resolution.HasConflict)
        {
            return "Regelkonflikt: mehrere gleich spezifische Regeln";
        }

        if (resolution.Rule is not { } rule)
        {
            return "Globale Standardregel";
        }

        return rule.Action switch
        {
            WindowPlacementMode.Exclude => "Nicht verwalten",
            WindowPlacementMode.RememberLast => "Letzte Platzierung",
            WindowPlacementMode.FixedZone when !FixedTargetExists(rule, profiles) =>
                "Feste Zone: Ziel nicht verfügbar",
            WindowPlacementMode.FixedZone => "Feste Zone",
            _ => rule.Action.ToString()
        };
    }

    internal static bool IsExactIdentityRule(WindowPlacementRule rule, WindowIdentity identity) =>
        string.Equals(rule.ApplicationKey, identity.ApplicationKey, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(rule.WindowClass, identity.WindowClass, StringComparison.Ordinal) &&
        rule.WindowKind == identity.Kind;

    private static bool FixedTargetExists(WindowPlacementRule rule, IReadOnlyList<LayoutProfile> profiles)
    {
        if (rule.ProfileId is not { } profileId ||
            rule.MonitorStableId is not { } monitorId ||
            rule.ZoneId is not { } zoneId)
        {
            return false;
        }

        var profile = profiles.FirstOrDefault(candidate => candidate.Id == profileId);
        var layout = profile?.Monitors.FirstOrDefault(candidate => string.Equals(
            candidate.Monitor.StableId,
            monitorId,
            StringComparison.OrdinalIgnoreCase));
        return layout?.Zones.Any(zone => zone.Id == zoneId) == true;
    }
}
