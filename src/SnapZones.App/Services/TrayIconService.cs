using Drawing = System.Drawing;
using Forms = System.Windows.Forms;
using SnapZones.Core.Models;
using SnapZones.App.Views;

namespace SnapZones.App.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly Forms.NotifyIcon icon;
    private readonly Drawing.Icon? applicationIcon;
    private readonly MainWindow window;
    private readonly Action<Guid> activateProfile;
    private readonly Action<bool> toggleSnapping;
    private readonly Action exit;

    public TrayIconService(
        MainWindow window,
        Action<Guid> activateProfile,
        Action<bool> toggleSnapping,
        Action exit)
    {
        this.window = window;
        this.activateProfile = activateProfile;
        this.toggleSnapping = toggleSnapping;
        this.exit = exit;
        applicationIcon = Environment.ProcessPath is { } processPath
            ? Drawing.Icon.ExtractAssociatedIcon(processPath)
            : null;
        icon = new Forms.NotifyIcon
        {
            Icon = applicationIcon ?? Drawing.SystemIcons.Application,
            Text = ProductInfo.Name,
            Visible = true
        };
        icon.DoubleClick += (_, _) => ShowWindow();
    }

    public void Update(SnapConfiguration configuration)
    {
        var menu = new Forms.ContextMenuStrip();
        var active = configuration.Profiles.Single(profile => profile.Id == configuration.Settings.ActiveProfileId);
        menu.Items.Add(new Forms.ToolStripMenuItem($"Profil: {active.Name}") { Enabled = false });
        menu.Items.Add(new Forms.ToolStripSeparator());
        foreach (var profile in configuration.Profiles)
        {
            var item = new Forms.ToolStripMenuItem(profile.Name)
            {
                Checked = profile.Id == active.Id
            };
            item.Click += (_, _) => activateProfile(profile.Id);
            menu.Items.Add(item);
        }

        menu.Items.Add(new Forms.ToolStripSeparator());
        var snapping = new Forms.ToolStripMenuItem("Snap-Funktion aktiv")
        {
            Checked = configuration.Settings.SnappingEnabled,
            CheckOnClick = true
        };
        snapping.Click += (_, _) => toggleSnapping(snapping.Checked);
        menu.Items.Add(snapping);
        menu.Items.Add("Editor öffnen", null, (_, _) => ShowWindow());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Beenden", null, (_, _) => exit());

        var previous = icon.ContextMenuStrip;
        icon.ContextMenuStrip = menu;
        previous?.Dispose();
        icon.Text = $"{ProductInfo.Name} · {active.Name}";
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
