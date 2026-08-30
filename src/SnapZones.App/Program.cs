namespace SnapZones.App;

internal static class Program
{
    [STAThread]
    public static void Main()
    {
        _ = System.Windows.Forms.Application.SetHighDpiMode(System.Windows.Forms.HighDpiMode.PerMonitorV2);
        var application = new App();
        application.InitializeComponent();
        application.Run();
    }
}
