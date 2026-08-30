using System.Windows;
using System.Windows.Threading;
using System.IO;
using SnapZones.App.Services;
using SnapZones.App.ViewModels;
using SnapZones.App.Views;
using SnapZones.Core.Persistence;
using SnapZones.Windows.Displays;
using SnapZones.Windows.Startup;
using SnapZones.Core.Models;

namespace SnapZones.App;

public partial class App : System.Windows.Application
{
    private SingleInstanceService? singleInstance;
    private ApplicationController? controller;
    private FileLog? log;
    private ThemeService? themeService;

    public void ApplyTheme(ThemeMode mode) => themeService?.Apply(mode);

    protected override async void OnStartup(StartupEventArgs eventArgs)
    {
        base.OnStartup(eventArgs);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SnapZones");
        var localData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SnapZones", "logs");
        log = new FileLog(localData);
        var startupService = new WindowsStartupService(Environment.ProcessPath ?? throw new InvalidOperationException("Der Programmpfad fehlt."));

        if (eventArgs.Args.Contains("--diagnostics", StringComparer.OrdinalIgnoreCase))
        {
            var exitCode = await DiagnosticRunner.RunAsync(appData, startupService);
            Shutdown(exitCode);
            return;
        }

        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            System.Windows.MessageBox.Show($"{ProductInfo.Name} benötigt Windows 11.", "Nicht unterstütztes System", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(3);
            return;
        }

        var context = new DispatcherSynchronizationContext(Dispatcher);
        singleInstance = new SingleInstanceService(ProductInfo.ProcessName, context);
        if (!singleInstance.IsPrimary)
        {
            singleInstance.NotifyPrimary();
            Shutdown();
            return;
        }

        try
        {
            var repository = new JsonConfigurationRepository(appData);
            var loadResult = await repository.LoadAsync(CancellationToken.None);
            themeService = new ThemeService();
            themeService.Apply(loadResult.Configuration.Settings.ThemeMode);
            var monitors = new WindowsMonitorService().GetMonitors();
            var viewModel = new MainViewModel(loadResult.Configuration, monitors);
            if (loadResult.RecoveredFromError)
            {
                viewModel.StatusMessage = loadResult.ErrorMessage ?? "Die Konfiguration wurde zurückgesetzt.";
            }

            var mainWindow = new MainWindow();
            themeService.Track(mainWindow);
            mainWindow.AttachViewModel(viewModel);
            controller = new ApplicationController(mainWindow, viewModel, repository, monitors, startupService, log);
            singleInstance.ActivationRequested += () =>
            {
                mainWindow.Show();
                mainWindow.Activate();
            };
            DispatcherUnhandledException += (_, exceptionArgs) =>
            {
                log.Write("FATAL", "Unbehandelter UI-Fehler.", exceptionArgs.Exception);
                controller?.EmergencyStop("Sicherheitsstopp nach einem UI-Fehler");
                exceptionArgs.Handled = true;
                Shutdown(4);
            };

            MainWindow = mainWindow;
            if (!eventArgs.Args.Contains("--autostart", StringComparer.OrdinalIgnoreCase))
            {
                mainWindow.Show();
            }
        }
        catch (Exception exception)
        {
            log.Write("FATAL", $"{ProductInfo.Name} konnte nicht gestartet werden.", exception);
            System.Windows.MessageBox.Show(exception.Message, $"{ProductInfo.Name} konnte nicht gestartet werden", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(5);
        }
    }

    protected override void OnExit(ExitEventArgs eventArgs)
    {
        controller?.Dispose();
        themeService?.Dispose();
        singleInstance?.Dispose();
        base.OnExit(eventArgs);
    }
}
