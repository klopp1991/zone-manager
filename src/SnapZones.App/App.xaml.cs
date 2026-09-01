using System.Windows;
using System.Windows.Threading;
using System.IO;
using SnapZones.App.Services;
using SnapZones.App.ViewModels;
using SnapZones.App.Views;
using SnapZones.Core.Persistence;
using SnapZones.Windows.Displays;
using SnapZones.Windows.Setup;
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

    private void RunSetup(SetupRunner.Mode mode, IReadOnlyList<string> arguments)
    {
        var (exitCode, message, startPath) = SetupRunner.Run(
            mode,
            Environment.ProcessPath ?? throw new InvalidOperationException("Der Programmpfad fehlt."),
            ProductInfo.Version,
            new InstallationService());
        log?.Write(exitCode == 0 ? "INFO" : "ERROR", message);

        if (!SetupRunner.IsSilent(arguments))
        {
            System.Windows.MessageBox.Show(
                message,
                mode == SetupRunner.Mode.Install ? "Installation" : "Deinstallation",
                MessageBoxButton.OK,
                exitCode == 0 ? MessageBoxImage.Information : MessageBoxImage.Error);
        }

        // Nach einer erfolgreichen Installation laeuft das Programm aus dem Installationsverzeichnis
        // weiter, nicht aus dem Download-Ordner.
        if (startPath is { Length: > 0 } path)
        {
            try
            {
                using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    WorkingDirectory = Path.GetDirectoryName(path) ?? AppContext.BaseDirectory,
                    UseShellExecute = true
                });
            }
            catch (Exception exception)
            {
                log?.Write("WARN", "Der Start aus dem Installationsverzeichnis ist fehlgeschlagen.", exception);
            }
        }

        Shutdown(exitCode);
    }

    protected override async void OnStartup(StartupEventArgs eventArgs)
    {
        base.OnStartup(eventArgs);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SnapZones");
        var localData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SnapZones", "logs");
        log = new FileLog(localData);
        var startupService = new StartupRegistration(
            Environment.ProcessPath ?? throw new InvalidOperationException("Der Programmpfad fehlt."),
            message => log.Write("WARN", message));

        if (eventArgs.Args.Contains("--diagnostics", StringComparer.OrdinalIgnoreCase))
        {
            var exitCode = await DiagnosticRunner.RunAsync(appData, startupService);
            Shutdown(exitCode);
            return;
        }

        // Installieren und Deinstallieren laufen ohne Hauptfenster und ohne Hook.
        var setupMode = SetupRunner.Decide(eventArgs.Args);
        if (setupMode != SetupRunner.Mode.None)
        {
            RunSetup(setupMode, eventArgs.Args);
            return;
        }

        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            System.Windows.MessageBox.Show($"{ProductInfo.Name} benötigt Windows 11.", "Nicht unterstütztes System", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(3);
            return;
        }

        var context = new DispatcherSynchronizationContext(Dispatcher);
        singleInstance = new SingleInstanceService(ProductInfo.InstanceKey, context);
        var startupDisposition = StartupPolicy.Decide(eventArgs.Args, singleInstance.IsPrimary);
        if (startupDisposition is StartupDisposition.ActivateRunningInstance or StartupDisposition.ExitDuplicate)
        {
            if (startupDisposition == StartupDisposition.ActivateRunningInstance)
            {
                singleInstance.NotifyPrimary();
            }

            Shutdown();
            return;
        }

        try
        {
            var repository = new JsonConfigurationRepository(appData);
            var loadResult = await repository.LoadAsync(CancellationToken.None);
            var placementRepository = new JsonWindowPlacementRepository(appData);
            var placementLoadResult = await WindowPlacementStartupLoad.Start(
                placementRepository,
                CancellationToken.None);
            themeService = new ThemeService();
            themeService.Apply(loadResult.Configuration.Settings.ThemeMode);
            var monitors = new WindowsMonitorService().GetMonitors();
            var viewModel = new MainViewModel(loadResult.Configuration, monitors)
            {
                ProductVersion = ProductInfo.Version
            };
            if (loadResult.RecoveredFromError)
            {
                viewModel.StatusMessage = loadResult.ErrorMessage ?? "Die Konfiguration wurde zurückgesetzt.";
            }
            if (placementLoadResult.RecoveredFromError)
            {
                viewModel.StatusMessage = placementLoadResult.ErrorMessage
                    ?? "Die Fensterplatzierungen wurden aus der Sicherung wiederhergestellt.";
            }

            var mainWindow = new MainWindow();
            themeService.Track(mainWindow);
            mainWindow.AttachViewModel(viewModel);
            controller = new ApplicationController(
                mainWindow,
                viewModel,
                repository,
                placementRepository,
                placementLoadResult.Catalog,
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
