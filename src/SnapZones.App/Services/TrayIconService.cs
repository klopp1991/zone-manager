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
    private readonly Drawing.Image? headerImage;
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
        headerImage = applicationIcon is null ? null : new Drawing.Bitmap(applicationIcon.ToBitmap(), 20, 20);
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
    /// Nennt den Zustand der Snap-Funktion. Nur ein angehaltenes Einrasten erscheint im Menue und im
    /// Tooltip, zusammen mit dem Eintrag zum Wiedereinschalten; ein laufendes Einrasten braucht keinen
    /// Eintrag.
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
        headerImage?.Dispose();
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

        // Kopfzeile: Symbol, Produktname und rechts der Zustand als Punkt mit Wort. Doppelklick auf das
        // Symbol im Infobereich oeffnet das Fenster; einen eigenen Menuepunkt dafuer gibt es nicht.
        var header = new Forms.ToolStripMenuItem(ProductInfo.Name)
        {
            Enabled = false,
            Font = new Drawing.Font(menu.Font, Drawing.FontStyle.Bold),
            Image = headerImage,
            ShowShortcutKeys = true,
            ShortcutKeyDisplayString = snappingStateLabel.Length > 0 ? snappingStateLabel : "● läuft"
        };
        menu.Items.Add(header);

        // Ein angehaltenes Einrasten braucht den Weg zurueck; der Not-Aus selbst steht nicht im Menue.
        if (snappingPaused && resumeSnapping is not null)
        {
            menu.Items.Add("Einrasten wieder aktivieren", null, (_, _) => resumeSnapping());
        }

        menu.Items.Add(new Forms.ToolStripSeparator());

        // Pro Monitor eine Gruppenueberschrift in Grossbuchstaben, darunter die Layouts eingerueckt,
        // das aktive mit Haekchen und halbfett. Keine Untermenues: ein Layoutwechsel ist ein Klick.
        // Die Reihenfolge ist die globale aus der Konfiguration.
        foreach (var monitor in plan.Monitors)
        {
            menu.Items.Add(new Forms.ToolStripMenuItem(monitor.Name.ToUpperInvariant()) { Enabled = false });
            foreach (var layout in monitor.Layouts)
            {
                var layoutItem = new Forms.ToolStripMenuItem($"    {layout.Name}")
                {
                    Checked = layout.IsActive,
                    Tag = layout.Id
                };
                if (layout.IsActive)
                {
                    layoutItem.Font = new Drawing.Font(menu.Font, Drawing.FontStyle.Bold);
                }

                layoutItem.Click += (_, _) => activateLayout(layout.Id);
                menu.Items.Add(layoutItem);
            }
        }

        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Fenster zuordnen …", null, (_, _) => ShowPage(ViewModels.NavigationPage.Rules));
        menu.Items.Add("Zonen zeichnen …", null, (_, _) => ShowPage(ViewModels.NavigationPage.Layouts));
        menu.Items.Add("Einstellungen …", null, (_, _) => ShowPage(ViewModels.NavigationPage.Startup));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(new Forms.ToolStripMenuItem("Beenden", null, (_, _) => exit())
        {
            ForeColor = Drawing.Color.FromArgb(0xB5, 0x24, 0x24)
        });

        // NotifyIcon.Text ist auf 127 Zeichen begrenzt.
        var tooltip = snappingPaused && snappingStateLabel.Length > 0
            ? $"{ProductInfo.Name} · {snappingStateLabel}"
            : $"{ProductInfo.Name} · {plan.Monitors.Count} Monitore";
        icon.Text = tooltip.Length > 127 ? tooltip[..127] : tooltip;
    }

    private void ShowPage(ViewModels.NavigationPage page)
    {
        ShowWindow();
        window.ShowPage(page);
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
