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
        ResolveActive(LayoutsFor(monitor));

    public MonitorLayout EnsureMonitor(MonitorIdentity monitor, int savedWidth, int savedHeight)
    {
        var existing = LayoutsFor(monitor);
        if (existing.Count > 0)
        {
            return ResolveActive(existing);
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

    /// <summary>
    /// Die Monitore, für die überhaupt ein Layout gespeichert ist – auch solche, die gerade nicht
    /// angeschlossen sind. Ohne diese Liste blieben Layouts nicht mehr vorhandener Monitore als
    /// unerreichbare Leichen in der Konfiguration zurück.
    /// </summary>
    public IReadOnlyList<MonitorIdentity> MonitorsWithLayouts()
    {
        var seen = new List<MonitorIdentity>();
        foreach (var layout in Configuration.Layouts)
        {
            if (!seen.Any(known => BelongsToMonitor(known, layout.Monitor)))
            {
                seen.Add(layout.Monitor);
            }
        }

        return seen;
    }

    /// <param name="allowRemovingLastLayout">
    /// Erlaubt das Löschen auch des letzten Layouts eines Monitors. Notwendig für Monitore, die nicht
    /// mehr angeschlossen sind: erst wenn deren letztes Layout weg ist, verschwindet der Monitor aus
    /// der Oberfläche.
    /// </param>
    public void DeleteLayout(Guid layoutId, bool allowRemovingLastLayout = false)
    {
        var deleted = Find(layoutId);
        var monitorLayouts = LayoutsFor(deleted.Monitor);
        if (monitorLayouts.Count == 1 && !allowRemovingLastLayout)
        {
            throw new InvalidOperationException("Das letzte Layout dieses Monitors kann nicht gelöscht werden.");
        }

        var remaining = Configuration.Layouts.Where(layout => layout.Id != layoutId).ToArray();
        if (deleted.IsActive && remaining.Any(layout => BelongsToMonitor(layout.Monitor, deleted.Monitor)))
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
        return Configuration.Layouts.First(layout => layout.Id == selected.Id);
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

    public static bool BelongsToMonitor(MonitorIdentity first, MonitorIdentity second)
    {
        // Die StableId ist der verlässliche Schlüssel. Der GDI-Gerätename (\\.\DISPLAYn)
        // wird von Windows neu vergeben und darf nur als Notnagel dienen, wenn mindestens
        // eine Seite keine StableId besitzt – sonst greifen zwei verschiedene Monitore
        // auf dieselben Layouts zu.
        if (HasStableId(first) && HasStableId(second))
        {
            return EqualsIgnoreCase(first.StableId, second.StableId);
        }

        return EqualsIgnoreCase(first.DeviceName, second.DeviceName);
    }

    private static bool HasStableId(MonitorIdentity monitor) =>
        !string.IsNullOrWhiteSpace(monitor.StableId);

    /// <summary>
    /// Liefert das aktive Layout einer Monitorgruppe und repariert dabei Konfigurationen,
    /// in denen kein oder mehr als ein Layout als aktiv markiert ist.
    /// </summary>
    private MonitorLayout ResolveActive(IReadOnlyList<MonitorLayout> monitorLayouts)
    {
        var active = monitorLayouts.Where(layout => layout.IsActive).ToArray();
        if (active.Length == 1)
        {
            return active[0];
        }

        var chosen = active.Length > 0 ? active[0] : monitorLayouts[0];
        var repaired = Configuration.Layouts.Select(layout =>
            monitorLayouts.Any(candidate => candidate.Id == layout.Id)
                ? layout with { IsActive = layout.Id == chosen.Id }
                : layout).ToArray();
        Configuration = Configuration with { Layouts = repaired };
        return chosen with { IsActive = true };
    }

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
