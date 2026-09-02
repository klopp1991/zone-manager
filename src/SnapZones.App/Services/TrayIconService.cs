using Drawing = System.Drawing;
using Forms = System.Windows.Forms;
using SnapZones.Core.Models;
using SnapZones.App.Views;

namespace SnapZones.App.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly Forms.NotifyIcon icon;
    private readonly Forms.ContextMenuStrip menu = new();
    private readonly Drawing.Icon? applicationIcon;
    private readonly MainWindow window;
    private readonly Action<Guid> activateLayout;
    private readonly Action exit;
    private readonly Action? resumeSnapping;
    private SnapConfiguration? deferredConfiguration;
    private SnapConfiguration? lastConfiguration;
    private string snappingStateLabel = string.Empty;
    private bool snappingPaused;

    public TrayIconService(
        MainWindow window,
        Action<Guid> activateLayout,
        Action exit,
        Action? resumeSnapping = null)
    {
        this.window = window;
        this.activateLayout = activateLayout;
        this.exit = exit;
        this.resumeSnapping = resumeSnapping;
        applicationIcon = Environment.ProcessPath is { } processPath
            ? Drawing.Icon.ExtractAssociatedIcon(processPath)
            : null;
        icon = new Forms.NotifyIcon
        {
            Icon = applicationIcon ?? Drawing.SystemIcons.Application,
            Text = ProductInfo.Name,
            Visible = true,
            ContextMenuStrip = menu
        };
        icon.DoubleClick += (_, _) => ShowWindow();
        menu.Closed += (_, _) => ApplyDeferredUpdate();
    }

    /// <summary>
    /// Das Kontextmenü des Infobereichssymbols. Es wird einmal erzeugt und nie ersetzt, damit ein
    /// geöffnetes Menü nicht unter dem Mauszeiger verworfen wird.
    /// </summary>
    public Forms.ContextMenuStrip Menu => menu;

    /// <summary>Zeigt an, dass eine Menüaktualisierung wartet, weil das Menü gerade geöffnet ist.</summary>
    public bool HasDeferredUpdate => deferredConfiguration is not null;

    /// <summary>
    /// Nennt den Zustand der Snap-Funktion im Menue und im Tooltip. Ist sie pausiert, erscheint
    /// zusaetzlich der Eintrag zum Wiedereinschalten; frueher fehlte beides und ein Not-Aus war im
    /// Infobereich nicht erkennbar.
    /// </summary>
    public void SetSnappingState(string label, bool paused)
    {
        snappingStateLabel = label ?? string.Empty;
        snappingPaused = paused;
        if (lastConfiguration is { } configuration)
        {
            Update(configuration);
        }
    }

    public void Update(SnapConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        lastConfiguration = configuration;

        // Das Kontextmenü darf nicht neu gebaut werden, solange es geöffnet ist. Früher ersetzte jede
        // Speicherung das ContextMenuStrip und verwarf das gerade sichtbare Menü; ein Klick auf
        // "Beenden" traf dann ein bereits verworfenes Menü und lief ins Leere.
        if (menu.Visible)
        {
            deferredConfiguration = configuration;
            return;
        }

        Rebuild(configuration);
    }

    public void Dispose()
    {
        icon.Visible = false;
        icon.ContextMenuStrip = null;
        menu.Dispose();
        icon.Dispose();
        applicationIcon?.Dispose();
    }

    private void ApplyDeferredUpdate()
    {
        if (deferredConfiguration is not { } configuration)
        {
            return;
        }

        deferredConfiguration = null;

        // Erst nachdem die ausstehende Klickmeldung zugestellt ist, dürfen die Menüeinträge freigegeben
        // werden. Sonst verschwindet der angeklickte Eintrag, bevor sein Click-Ereignis ausgeführt wurde.
        if (menu.IsHandleCreated)
        {
            _ = menu.BeginInvoke(new Action(() => Rebuild(configuration)));
            return;
        }

        Rebuild(configuration);
    }

    private void Rebuild(SnapConfiguration configuration)
    {
        var plan = TrayLayoutMenuPlan.Build(configuration);
        var retired = menu.Items.Cast<Forms.ToolStripItem>().ToArray();
        menu.Items.Clear();
        foreach (var item in retired)
        {
            item.Dispose();
        }

        if (snappingStateLabel.Length > 0)
        {
            menu.Items.Add(new Forms.ToolStripMenuItem(snappingStateLabel) { Enabled = false });
            if (snappingPaused && resumeSnapping is not null)
            {
                menu.Items.Add("Einrasten wieder aktivieren", null, (_, _) => resumeSnapping());
            }

            menu.Items.Add(new Forms.ToolStripSeparator());
        }

        menu.Items.Add(new Forms.ToolStripMenuItem("Layouts pro Monitor") { Enabled = false });
        menu.Items.Add(new Forms.ToolStripSeparator());
        foreach (var monitor in plan.Monitors)
        {
            var monitorItem = new Forms.ToolStripMenuItem(monitor.Name);
            foreach (var layout in monitor.Layouts)
            {
                var layoutItem = new Forms.ToolStripMenuItem(layout.Name)
                {
                    Checked = layout.IsActive
                };
                layoutItem.Click += (_, _) => activateLayout(layout.Id);
                monitorItem.DropDownItems.Add(layoutItem);
            }

            menu.Items.Add(monitorItem);
        }

        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Editor öffnen", null, (_, _) => ShowWindow());
        menu.Items.Add("Einstellungen öffnen", null, (_, _) => ShowSettings());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Beenden", null, (_, _) => exit());

        // NotifyIcon.Text ist auf 127 Zeichen begrenzt.
        var tooltip = snappingStateLabel.Length > 0
            ? $"{ProductInfo.Name} · {snappingStateLabel}"
            : $"{ProductInfo.Name} · {plan.Monitors.Count} Monitore";
        icon.Text = tooltip.Length > 127 ? tooltip[..127] : tooltip;
    }

    private void ShowSettings()
    {
        ShowWindow();
        window.ShowSettingsPage();
    }

    private void ShowWindow()
    {
        window.Show();
        if (window.WindowState == System.Windows.WindowState.Minimized)
        {
            window.WindowState = System.Windows.WindowState.Normal;
        }

        window.Activate();
    }
}
