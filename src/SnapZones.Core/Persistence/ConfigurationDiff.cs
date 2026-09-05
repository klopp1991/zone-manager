using System.Globalization;
using SnapZones.Core.AppRules;
using SnapZones.Core.Layouts;
using SnapZones.Core.Models;
using SnapZones.Core.Monitors;

namespace SnapZones.Core.Persistence;

/// <summary>
/// Beschreibt in einem Satz, was einen aelteren Stand vom naechstjuengeren unterscheidet. Die Liste der
/// frueheren Staende auf der Seite «Programm» lebt davon: fuenf Zeitstempel allein sagen niemandem, welcher
/// Stand der gesuchte ist.
/// </summary>
public static class ConfigurationDiff
{
    public const string Unchanged = "Automatische Sicherung";

    /// <summary>
    /// Der erste erkennbare Unterschied von <paramref name="older"/> zu <paramref name="newer"/>, formuliert
    /// als das, was danach geschehen ist. Ohne juengeren Stand oder ohne Unterschied: <see cref="Unchanged"/>.
    /// </summary>
    public static string Summarize(SnapConfiguration older, SnapConfiguration? newer)
    {
        ArgumentNullException.ThrowIfNull(older);
        if (newer is null)
        {
            return Unchanged;
        }

        return DescribeLayouts(older, newer)
            ?? DescribeRules(older, newer)
            ?? DescribeExclusions(older, newer)
            ?? DescribeMonitors(older, newer)
            ?? DescribeSettings(older.Settings, newer.Settings)
            ?? Unchanged;
    }

    private static string? DescribeLayouts(SnapConfiguration older, SnapConfiguration newer)
    {
        var olderById = older.Layouts.ToDictionary(layout => layout.Id);
        var newerById = newer.Layouts.ToDictionary(layout => layout.Id);

        foreach (var added in newer.Layouts.Where(layout => !olderById.ContainsKey(layout.Id)))
        {
            var monitorKnown = older.Layouts.Any(layout => LayoutService.BelongsToMonitor(layout.Monitor, added.Monitor));
            return monitorKnown
                ? $"Layout «{added.Name}» angelegt"
                : $"Monitor «{MonitorName(newer, added.Monitor)}» angesteckt";
        }

        foreach (var removed in older.Layouts.Where(layout => !newerById.ContainsKey(layout.Id)))
        {
            return $"Layout «{removed.Name}» gelöscht";
        }

        foreach (var layout in older.Layouts)
        {
            var replacement = newerById[layout.Id];
            if (!string.Equals(layout.Name, replacement.Name, StringComparison.Ordinal))
            {
                return $"Layout «{layout.Name}» in «{replacement.Name}» umbenannt";
            }

            if (layout.Zones.Count != replacement.Zones.Count)
            {
                return layout.Zones.Count < replacement.Zones.Count
                    ? $"Layout «{layout.Name}»: Zone hinzugefügt"
                    : $"Layout «{layout.Name}»: Zone entfernt";
            }

            for (var index = 0; index < layout.Zones.Count; index++)
            {
                var before = layout.Zones[index];
                var after = replacement.Zones[index];
                if (!string.Equals(before.Name, after.Name, StringComparison.Ordinal))
                {
                    return $"Layout «{layout.Name}»: Zone «{before.Name}» in «{after.Name}» umbenannt";
                }

                if (before.Bounds != after.Bounds)
                {
                    var grew = after.Bounds.Width * after.Bounds.Height > before.Bounds.Width * before.Bounds.Height;
                    return $"Layout «{layout.Name}»: Zone {index + 1} {(grew ? "vergrössert" : "verkleinert")}";
                }
            }

            if (layout.MainZoneId != replacement.MainZoneId)
            {
                return replacement.MainZoneId is null
                    ? $"Layout «{layout.Name}»: Auffangzone aufgehoben"
                    : $"Layout «{layout.Name}»: Auffangzone festgelegt";
            }

            if (layout.IsActive != replacement.IsActive && replacement.IsActive)
            {
                return $"Layout «{replacement.Name}» aktiviert";
            }
        }

        return null;
    }

    private static string? DescribeRules(SnapConfiguration older, SnapConfiguration newer)
    {
        var olderById = older.AppRules.ToDictionary(rule => rule.Id);
        var newerById = newer.AppRules.ToDictionary(rule => rule.Id);
        foreach (var added in newer.AppRules.Where(rule => !olderById.ContainsKey(rule.Id)))
        {
            return $"Zuordnung {added.DisplayName} → {ZoneName(newer, added)} angelegt";
        }

        foreach (var removed in older.AppRules.Where(rule => !newerById.ContainsKey(rule.Id)))
        {
            return $"Zuordnung {removed.DisplayName} → {ZoneName(older, removed)} entfernt";
        }

        foreach (var rule in older.AppRules)
        {
            var replacement = newerById[rule.Id];
            if (rule != replacement)
            {
                return rule.IsEnabled != replacement.IsEnabled
                    ? $"Zuordnung {replacement.DisplayName} {(replacement.IsEnabled ? "eingeschaltet" : "ausgeschaltet")}"
                    : $"Zuordnung {replacement.DisplayName} geändert";
            }
        }

        return null;
    }

    private static string? DescribeExclusions(SnapConfiguration older, SnapConfiguration newer)
    {
        var olderById = older.AppExclusions.ToDictionary(exclusion => exclusion.Id);
        var newerById = newer.AppExclusions.ToDictionary(exclusion => exclusion.Id);
        foreach (var added in newer.AppExclusions.Where(exclusion => !olderById.ContainsKey(exclusion.Id)))
        {
            return $"«{added.DisplayName}» wird in Ruhe gelassen";
        }

        foreach (var removed in older.AppExclusions.Where(exclusion => !newerById.ContainsKey(exclusion.Id)))
        {
            return $"«{removed.DisplayName}» rastet wieder ein";
        }

        foreach (var exclusion in older.AppExclusions)
        {
            if (exclusion != newerById[exclusion.Id])
            {
                return $"«{exclusion.DisplayName}»: Eingrenzung geändert";
            }
        }

        return null;
    }

    private static string? DescribeMonitors(SnapConfiguration older, SnapConfiguration newer)
    {
        foreach (var entry in newer.MonitorNames)
        {
            if (!older.MonitorNames.TryGetValue(entry.Key, out var previous) ||
                !string.Equals(previous, entry.Value, StringComparison.Ordinal))
            {
                return $"Monitor in «{entry.Value}» umbenannt";
            }
        }

        foreach (var entry in older.MonitorNames)
        {
            if (!newer.MonitorNames.ContainsKey(entry.Key))
            {
                return $"Monitorname «{entry.Value}» entfernt";
            }
        }

        if (!older.MonitorOrder.SequenceEqual(newer.MonitorOrder, StringComparer.OrdinalIgnoreCase))
        {
            return "Monitorreihenfolge geändert";
        }

        return null;
    }

    private static string? DescribeSettings(AppSettings older, AppSettings newer)
    {
        if (older == newer)
        {
            return null;
        }

        if (Math.Abs(older.OverlayOpacity - newer.OverlayOpacity) > 0.0001)
        {
            return $"Deckkraft {Percent(older.OverlayOpacity)} → {Percent(newer.OverlayOpacity)}";
        }

        if (!string.Equals(older.OverlayColor, newer.OverlayColor, StringComparison.OrdinalIgnoreCase))
        {
            return $"Farbe der Zonen {older.OverlayColor} → {newer.OverlayColor}";
        }

        if (older.ZoneGap != newer.ZoneGap)
        {
            return $"Abstand zwischen Zonen {older.ZoneGap} px → {newer.ZoneGap} px";
        }

        if (older.EffectiveOuterMargins != newer.EffectiveOuterMargins)
        {
            return "Abstand zum Bildschirmrand geändert";
        }

        if (older.ThemeMode != newer.ThemeMode)
        {
            return "Erscheinungsbild geändert";
        }

        if (older.StartWithWindows != newer.StartWithWindows)
        {
            return newer.StartWithWindows ? "Autostart eingeschaltet" : "Autostart ausgeschaltet";
        }

        if (older.RememberWindowPositions != newer.RememberWindowPositions)
        {
            return newer.RememberWindowPositions ? "Fensterpositionen merken eingeschaltet" : "Fensterpositionen merken ausgeschaltet";
        }

        if (older.ZoneFullscreen != newer.ZoneFullscreen)
        {
            return newer.ZoneFullscreen ? "Vollbild in der Zone eingeschaltet" : "Vollbild in der Zone ausgeschaltet";
        }

        if (older.OverlayScope != newer.OverlayScope || older.TriggerMode != newer.TriggerMode || older.ShowZoneNames != newer.ShowZoneNames)
        {
            return "Verhalten beim Ziehen geändert";
        }

        if (older.ZoneHotkeysEnabled != newer.ZoneHotkeysEnabled || older.ZoneHotkeyModifiers != newer.ZoneHotkeyModifiers)
        {
            return "Tastenkürzel geändert";
        }

        if (older.ElevationMode != newer.ElevationMode)
        {
            return "Administratorrechte geändert";
        }

        if (older.CheckForUpdatesOnStart != newer.CheckForUpdatesOnStart)
        {
            return newer.CheckForUpdatesOnStart ? "Updatesuche beim Start eingeschaltet" : "Updatesuche beim Start ausgeschaltet";
        }

        if (older.MagnetThresholdPixels != newer.MagnetThresholdPixels)
        {
            return $"Andocken im Editor {older.MagnetThresholdPixels} px → {newer.MagnetThresholdPixels} px";
        }

        if (older.EditorValuePanelOpen != newer.EditorValuePanelOpen)
        {
            return null;
        }

        return "Feinabstimmung geändert";
    }

    private static string Percent(double value) =>
        string.Create(CultureInfo.CurrentCulture, $"{Math.Round(value * 100):0} %");

    private static string ZoneName(SnapConfiguration configuration, AppRule rule)
    {
        var layout = configuration.Layouts.FirstOrDefault(candidate => candidate.Id == rule.TargetLayoutId);
        var zone = layout?.Zones.FirstOrDefault(candidate => candidate.Id == rule.TargetZoneId);
        return zone?.Name ?? "Zone";
    }

    private static string MonitorName(SnapConfiguration configuration, MonitorIdentity monitor) =>
        MonitorNaming.UserFacingName(
            MonitorNaming.CustomNameFor(configuration, monitor),
            MonitorNaming.ResolveDisplayNumber(monitor, 1));
}
