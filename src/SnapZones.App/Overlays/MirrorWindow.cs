using System.Windows.Forms;
using SnapZones.Core.Geometry;

namespace SnapZones.App.Overlays;

/// <summary>
/// Das randlose Fenster in der Zone, in das der Spiegel zeichnet. Es nimmt nie den Fokus
/// (<c>WS_EX_NOACTIVATE</c>), erscheint nicht in der Taskleiste, liegt obenauf und malt selbst nichts:
/// die Swapchain des Spiegels haengt direkt an seinem Fensterhandle. Ein WinForms-Fenster statt WPF,
/// weil WPF sein Fenster selbst zeichnet und eine fremde Swapchain darauf nicht vertraegt.
/// </summary>
public sealed class MirrorWindow : Form
{
    private const int NoActivate = 0x08000000;
    private const int ToolWindow = 0x00000080;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpShowWindow = 0x0040;

    public MirrorWindow()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = System.Drawing.Color.Black;
        TopMost = true;
        Text = "Zone Manager – Vollbildzone";
        SetStyle(ControlStyles.Opaque | ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint, true);
    }

    /// <summary>Jede Fensternachricht, damit Tastenkuerzel und Rohdaten des Zeigers hier landen koennen.</summary>
    public event Action<Message>? MessageReceived;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= NoActivate | ToolWindow;
            return parameters;
        }
    }

    protected override bool ShowWithoutActivation => true;

    /// <summary>Zeigt das Fenster genau auf diesem Rechteck in physischen Pixeln, ohne es zu aktivieren.</summary>
    public void ShowAt(PixelRect bounds)
    {
        SetBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        if (!Visible)
        {
            Show();
        }

        SetWindowPos(Handle, 0, bounds.X, bounds.Y, bounds.Width, bounds.Height, SwpNoActivate | SwpNoZOrder | SwpShowWindow);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint window, nint insertAfter, int x, int y, int width, int height, uint flags);

    protected override void OnPaintBackground(PaintEventArgs e)
    {
    }

    protected override void OnPaint(PaintEventArgs e)
    {
    }

    protected override void WndProc(ref Message m)
    {
        MessageReceived?.Invoke(m);
        base.WndProc(ref m);
    }
}
