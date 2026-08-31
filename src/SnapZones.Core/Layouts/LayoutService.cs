using SnapZones.Core.Models;
using SnapZones.Core.Monitors;
using SnapZones.Core.AppRules;

namespace SnapZones.Core.Layouts;

public sealed class LayoutService
{
    public LayoutService(SnapConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        if (configuration.SchemaVersion != SnapConfiguration.CurrentSchemaVersion)
        {
            throw new ArgumentException("Die Konfiguration besitzt nicht die aktuelle Layoutstruktur.", nameof(configuration));
        }

        Configuration = configuration;
    }

    public SnapConfiguration Configuration { get; private set; }

    public IReadOnlyList<MonitorLayout> LayoutsFor(MonitorIdentity monitor) =>
        Configuration.Layouts.Where(layout => BelongsToMonitor(layout.Monitor, monitor)).ToArray();

    public MonitorLayout ActiveLayoutFor(MonitorIdentity monitor) =>
        LayoutsFor(monitor).Single(layout => layout.IsActive);

    public MonitorLayout EnsureMonitor(MonitorIdentity monitor, int savedWidth, int savedHeight)
    {
        var existing = LayoutsFor(monitor);
        if (existing.Count > 0)
        {
            return existing.Single(layout => layout.IsActive);
        }

        var layout = new MonitorLayout(
            monitor,
            savedWidth,
            savedHeight,
            [new ZoneDefinition(Guid.NewGuid(), "Voll", NormalizedRect.Full)])
        {
            Name = "Standard",
            IsActive = true
        };
        Configuration = Configuration with { Layouts = [.. Configuration.Layouts, layout] };
        return layout;
    }

    public MonitorLayout AddLayout(Guid sourceLayoutId, string name)
    {
        var source = Find(sourceLayoutId);
        var trimmedName = ValidateName(source.Monitor, sourceLayoutId, name);
        var added = source with
        {
            Id = Guid.NewGuid(),
            Name = trimmedName,
            IsActive = true,
            Zones = source.Zones.Select(zone => zone with { Id = Guid.NewGuid() }).ToArray()
        };
        var layouts = Configuration.Layouts
            .Select(layout => BelongsToMonitor(layout.Monitor, source.Monitor)
                ? layout with { IsActive = false }
                : layout)
            .Append(added)
            .ToArray();
        Configuration = Configuration with { Layouts = layouts };
        return added;
    }

    public void RenameLayout(Guid layoutId, string name)
    {
        var layout = Find(layoutId);
        var trimmedName = ValidateName(layout.Monitor, layoutId, name);
        Replace(layout with { Name = trimmedName });
    }

    public void RenameMonitor(MonitorIdentity monitor, string? name)
    {
        ArgumentNullException.ThrowIfNull(monitor);
        var trimmedName = name?.Trim() ?? string.Empty;
        if (trimmedName.Length > MonitorNaming.MaximumCustomNameLength)
        {
            throw new ArgumentException(
                $"Der Monitorname darf höchstens {MonitorNaming.MaximumCustomNameLength} Zeichen enthalten.",
                nameof(name));
        }

        var key = MonitorNaming.KeyFor(monitor);
        var monitorNames = new Dictionary<string, string>(Configuration.MonitorNames, StringComparer.OrdinalIgnoreCase);
        monitorNames.Remove(key);
        if (trimmedName.Length > 0)
        {
            monitorNames[key] = trimmedName;
        }

        Configuration = Configuration with { MonitorNames = monitorNames };
    }

    public string? CustomMonitorNameFor(MonitorIdentity monitor) =>
        MonitorNaming.CustomNameFor(Configuration, monitor);

    public void UpdateMonitorOrder(IEnumerable<MonitorIdentity> monitors)
    {
        ArgumentNullException.ThrowIfNull(monitors);
        var orderedKeys = monitors
            .Select(MonitorNaming.KeyFor)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        orderedKeys.AddRange(Configuration.MonitorOrder.Where(key =>
            !orderedKeys.Contains(key, StringComparer.OrdinalIgnoreCase)));
        Configuration = Configuration with { MonitorOrder = orderedKeys };
    }

    public void DeleteLayout(Guid layoutId)
    {
        var deleted = Find(layoutId);
        var monitorLayouts = LayoutsFor(deleted.Monitor);
        if (monitorLayouts.Count == 1)
        {
            throw new InvalidOperationException("Das letzte Layout dieses Monitors kann nicht gelöscht werden.");
        }

        var remaining = Configuration.Layouts.Where(layout => layout.Id != layoutId).ToArray();
        if (deleted.IsActive)
        {
            var replacementId = remaining.First(layout => BelongsToMonitor(layout.Monitor, deleted.Monitor)).Id;
            remaining = remaining.Select(layout => layout.Id == replacementId
                ? layout with { IsActive = true }
                : layout).ToArray();
        }

        Configuration = Configuration with { Layouts = remaining };
    }

    public MonitorLayout ActivateLayout(Guid layoutId)
    {
        var selected = Find(layoutId);
        var layouts = Configuration.Layouts.Select(layout =>
            BelongsToMonitor(layout.Monitor, selected.Monitor)
                ? layout with { IsActive = layout.Id == selected.Id }
                : layout).ToArray();
        Configuration = Configuration with { Layouts = layouts };
        return Configuration.Layouts.Single(layout => layout.Id == selected.Id);
    }

    public void UpdateLayout(MonitorLayout layout)
    {
        ArgumentNullException.ThrowIfNull(layout);
        _ = Find(layout.Id);
        Replace(layout);
    }

    public void UpdateSettings(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Configuration = Configuration with { Settings = settings };
    }

    public void UpdateAppRules(IReadOnlyList<AppRule> rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        Configuration = Configuration with { AppRules = rules.ToArray() };
    }

    public static bool BelongsToMonitor(MonitorIdentity first, MonitorIdentity second) =>
        EqualsIgnoreCase(first.StableId, second.StableId) ||
        EqualsIgnoreCase(first.DeviceName, second.DeviceName);

    private MonitorLayout Find(Guid layoutId) =>
        Configuration.Layouts.FirstOrDefault(layout => layout.Id == layoutId)
        ?? throw new KeyNotFoundException("Das Layout wurde nicht gefunden.");

    private string ValidateName(MonitorIdentity monitor, Guid layoutId, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var trimmedName = name.Trim();
        if (Configuration.Layouts.Any(layout =>
            layout.Id != layoutId &&
            BelongsToMonitor(layout.Monitor, monitor) &&
            string.Equals(layout.Name, trimmedName, StringComparison.CurrentCultureIgnoreCase)))
        {
            throw new InvalidOperationException("Für diesen Monitor ist bereits ein Layout mit diesem Namen vorhanden.");
        }

        return trimmedName;
    }

    private void Replace(MonitorLayout replacement)
    {
        var layouts = Configuration.Layouts.ToArray();
        var index = Array.FindIndex(layouts, layout => layout.Id == replacement.Id);
        if (index < 0)
        {
            throw new KeyNotFoundException("Das Layout wurde nicht gefunden.");
        }

        layouts[index] = replacement;
        Configuration = Configuration with { Layouts = layouts };
    }

    private static bool EqualsIgnoreCase(string first, string second) =>
        !string.IsNullOrWhiteSpace(first) &&
        string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
}
