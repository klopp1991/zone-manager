using System.Diagnostics;
using SnapZones.App.Services;
using SnapZones.Windows.Security;

namespace SnapZones.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] arguments)
    {
        _ = System.Windows.Forms.Application.SetHighDpiMode(System.Windows.Forms.HighDpiMode.PerMonitorV2);
        var probe = WindowsElevationProbe.Inspect();
        var capability = ElevationCapability.Inspect(
            probe.IsElevated,
            probe.IsAdministratorMember,
            probe.IsUserAccountControlEnabled,
            probe.IsInteractiveSession);
        var elevationResult = ElevationStartupService.EnsureElevation(
            Environment.ProcessPath ?? throw new InvalidOperationException("Der Programmpfad fehlt."),
            arguments,
            capability,
            StartElevated);
        if (elevationResult.Status == ElevationStartupStatus.Relaunched)
        {
            return;
        }

        var application = new App
        {
            Elevation = new ElevationRuntimeState(capability, elevationResult.Notice)
        };
        application.InitializeComponent();
        application.Run();
    }

    internal static bool StartElevated(ProcessStartInfo startInfo)
    {
        using var process = Process.Start(startInfo);
        return process is not null;
    }
}
