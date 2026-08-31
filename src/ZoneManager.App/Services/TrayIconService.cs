using Drawing = System.Drawing;
using Forms = System.Windows.Forms;
using ZoneManager.Core.Models;
using ZoneManager.App.Views;

namespace ZoneManager.App.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly Forms.NotifyIcon icon;
    private readonly Drawing.Icon? applicationIcon;
    private readonly MainWindow window;
    private readonly Action<Guid> activateLayout;
    private readonly Action exit;

    public TrayIconService(
        MainWindow window,
        Action<Guid> activateLayout,
        Action exit,
        bool elevationRestricted = false)
    {
        ElevationRestricted = elevationRestricted;
        this.window = window;
        this.activateLayout = activateLayout;
        this.exit = exit;
        applicationIcon = Environment.ProcessPath is { } processPath
            ? Drawing.Icon.ExtractAssociatedIcon(processPath)
            : null;
        icon = new Forms.NotifyIcon
        {
            Icon = applicationIcon ?? Drawing.SystemIcons.Application,
            Text = TrayTooltip.Build(ProductInfo.Name, 0, elevationRestricted),
            Visible = true
        };
        icon.DoubleClick += (_, _) => ShowWindow();
    }

    /// <summary>Weist im Tooltip aus, dass erhöhte Fremdfenster nicht positioniert werden können.</summary>
    public bool ElevationRestricted { get; }

    public void Update(SnapConfiguration configuration)
    {
        var menu = new Forms.ContextMenuStrip();
        var plan = TrayLayoutMenuPlan.Build(configuration);
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
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Beenden", null, (_, _) => exit());

        var previous = icon.ContextMenuStrip;
        icon.ContextMenuStrip = menu;
        previous?.Dispose();
        icon.Text = TrayTooltip.Build(ProductInfo.Name, plan.Monitors.Count, ElevationRestricted);
    }

    public void Dispose()
    {
        icon.Visible = false;
        icon.ContextMenuStrip?.Dispose();
        icon.Dispose();
        applicationIcon?.Dispose();
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
