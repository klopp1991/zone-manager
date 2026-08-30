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
    private static readonly TimeSpan RestartTakeoverTimeout = TimeSpan.FromSeconds(10);
    private SingleInstanceService? singleInstance;
    private ApplicationController? controller;
    private FileLog? log;
    private ThemeService? themeService;

    public void ApplyTheme(ThemeMode mode) => themeService?.Apply(mode);

    protected override async void OnStartup(StartupEventArgs eventArgs)
    {
        base.OnStartup(eventArgs);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var executablePath = Environment.ProcessPath ?? throw new InvalidOperationException("Der Programmpfad fehlt.");
        var paths = ApplicationDataPaths.Resolve(
            executablePath,
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        log = new FileLog(paths.LogDirectory);
        var startupService = new WindowsStartupService(executablePath);

        if (eventArgs.Args.Contains("--diagnostics", StringComparer.OrdinalIgnoreCase))
        {
            var exitCode = await DiagnosticRunner.RunAsync(paths.ConfigurationDirectory, startupService);
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
        var startupDisposition = StartupPolicy.Decide(eventArgs.Args, singleInstance.IsPrimary);
        if (startupDisposition == StartupDisposition.ExitDuplicate)
        {
            Shutdown();
            return;
        }

        if (startupDisposition == StartupDisposition.ReplaceRunningInstance)
        {
            if (!singleInstance.RequestRestartAndTakeOwnership(RestartTakeoverTimeout))
            {
                singleInstance.NotifyPrimary();
                log.Write("ERROR", "Die laufende Instanz hat die Neustartanforderung nicht rechtzeitig abgeschlossen.");
                System.Windows.MessageBox.Show(
                    "Die laufende Instanz konnte nicht innerhalb von 10 Sekunden beendet werden. Ihr Fenster wurde stattdessen angefordert.",
                    $"{ProductInfo.Name} konnte nicht neu gestartet werden",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                Shutdown(6);
                return;
            }

            startupDisposition = StartupDisposition.StartVisible;
        }

        try
        {
            var repository = new JsonConfigurationRepository(paths.ConfigurationDirectory);
            var placementRepository = new JsonWindowPlacementRepository(paths.ConfigurationDirectory);
            var placementLoadTask = placementRepository.LoadAsync(CancellationToken.None);
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
            controller = new ApplicationController(
                mainWindow,
                viewModel,
                repository,
                placementRepository,
                monitors,
                startupService,
                log);
            singleInstance.ActivationRequested += () =>
            {
                mainWindow.Show();
                if (mainWindow.WindowState == WindowState.Minimized)
                {
                    mainWindow.WindowState = WindowState.Normal;
                }

                mainWindow.Activate();
            };
            singleInstance.RestartRequested += controller.RequestExit;
            singleInstance.StartListening();
            DispatcherUnhandledException += async (_, exceptionArgs) =>
            {
                log.Write("FATAL", "Unbehandelter UI-Fehler.", exceptionArgs.Exception);
                exceptionArgs.Handled = true;
                if (controller is not null)
                {
                    controller.EmergencyStop("Sicherheitsstopp nach einem UI-Fehler");
                    try
                    {
                        await controller.FlushAsync(CancellationToken.None);
                    }
                    catch (Exception saveException)
                    {
                        log.Write("ERROR", "Die Notfallsicherung ist fehlgeschlagen.", saveException);
                    }
                }

                Shutdown(4);
            };

            MainWindow = mainWindow;
            if (startupDisposition == StartupDisposition.StartVisible)
            {
                mainWindow.Show();
            }

            var placementLoad = await placementLoadTask;
            controller.InitializeWindowPlacements(placementLoad);
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
