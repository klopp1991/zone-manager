using System.Diagnostics;
using System.Security.Principal;
using SnapZones.App.Services;

namespace SnapZones.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] arguments)
    {
        _ = System.Windows.Forms.Application.SetHighDpiMode(System.Windows.Forms.HighDpiMode.PerMonitorV2);
        var elevationResult = ElevationStartupService.EnsureElevation(
            Environment.ProcessPath ?? throw new InvalidOperationException("Der Programmpfad fehlt."),
            arguments,
            IsAdministrator(),
            startInfo =>
            {
                using var process = Process.Start(startInfo);
                return process is not null;
            });
        if (elevationResult.Status != ElevationStartupStatus.Continue)
        {
            if (elevationResult.Status == ElevationStartupStatus.Cancelled)
            {
                System.Windows.MessageBox.Show(
                    $"{ProductInfo.Name} benötigt Administratorrechte und wurde nicht gestartet.",
                    "Administratorrechte erforderlich",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
            else if (elevationResult.Status == ElevationStartupStatus.Failed)
            {
                System.Windows.MessageBox.Show(
                    $"Der Neustart mit Administratorrechten ist fehlgeschlagen.\n\n{elevationResult.ErrorMessage}",
                    $"{ProductInfo.Name} konnte nicht gestartet werden",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }

            return;
        }

        var application = new App();
        application.InitializeComponent();
        application.Run();
    }

    private static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
}
