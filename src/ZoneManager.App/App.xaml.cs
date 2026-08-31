using System.Windows;
using System.Windows.Threading;
using System.IO;
using ZoneManager.App.Services;
using ZoneManager.App.ViewModels;
using ZoneManager.App.Views;
using ZoneManager.Core.Persistence;
using ZoneManager.Windows.Displays;
using ZoneManager.Windows.Startup;
using ZoneManager.Core.Models;

namespace ZoneManager.App;

public partial class App : System.Windows.Application
{
    private SingleInstanceService? singleInstance;
    private ApplicationController? controller;
    private FileLog? log;
    private ThemeService? themeService;
    private MainWindow? mainWindow;
    private DispatcherSynchronizationContext? synchronizationContext;
    private string[] startupArguments = [];

    /// <summary>Ergebnis der Rechtevorprüfung aus <see cref="Program"/>.</summary>
    public ElevationRuntimeState Elevation { get; init; } = ElevationRuntimeState.Unknown;

    public void ApplyTheme(ThemeMode mode) => themeService?.Apply(mode);

    protected override async void OnStartup(StartupEventArgs eventArgs)
    {
        base.OnStartup(eventArgs);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        startupArguments = eventArgs.Args;
        var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ProductInfo.DataFolderName);
        var legacyAppData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ProductInfo.LegacyDataFolderName);
        var localData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ProductInfo.DataFolderName,
            "logs");
        log = new FileLog(localData);
        var migration = ConfigurationDirectoryMigration.Run(legacyAppData, appData);
        log.Write(
            migration.Status == ConfigurationMigrationStatus.Failed ? "ERROR" : "INFO",
            $"Konfigurationsübernahme: {migration.Status}. {migration.Message}");
        var startupService = new WindowsStartupService(Environment.ProcessPath ?? throw new InvalidOperationException("Der Programmpfad fehlt."));

        if (eventArgs.Args.Contains("--diagnostics", StringComparer.OrdinalIgnoreCase))
        {
            var exitCode = await DiagnosticRunner.RunAsync(appData, startupService, Elevation.Capability);
            Shutdown(exitCode);
            return;
        }

        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000))
        {
            System.Windows.MessageBox.Show($"{ProductInfo.Name} benötigt Windows 11.", "Nicht unterstütztes System", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(3);
            return;
        }

        synchronizationContext = new DispatcherSynchronizationContext(Dispatcher);
        singleInstance = new SingleInstanceService(ProductInfo.InstanceKey, synchronizationContext);
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
            var viewModel = new MainViewModel(loadResult.Configuration, monitors);
            if (loadResult.RecoveredFromError)
            {
                viewModel.StatusMessage = loadResult.ErrorMessage ?? "Die Konfiguration wurde zurückgesetzt.";
            }
            if (placementLoadResult.RecoveredFromError)
            {
                viewModel.StatusMessage = placementLoadResult.ErrorMessage
                    ?? "Die Fensterplatzierungen wurden aus der Sicherung wiederhergestellt.";
            }

            mainWindow = new MainWindow();
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
                log,
                Elevation);
            if (Elevation.IsRestricted)
            {
                var banner = Elevation.Banner;
                if (!string.IsNullOrWhiteSpace(banner))
                {
                    mainWindow.ShowElevationNotice(banner, Elevation.CanRetry);
                    log.Write("WARN", banner);
                }
            }

            mainWindow.RetryElevationRequested += RetryElevation;
            singleInstance.ActivationRequested += ActivateMainWindow;
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

    /// <summary>
    /// Startet die Anwendung auf Wunsch erneut mit Administratorrechten. Der Einzelinstanzschlüssel
    /// wird vorher freigegeben, damit sich der erhöhte Prozess nicht selbst als Zweitinstanz beendet.
    /// </summary>
    private async void RetryElevation()
    {
        if (mainWindow is null)
        {
            return;
        }

        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            mainWindow.ShowElevationNotice("Der Programmpfad fehlt; ein Neustart ist nicht möglich.", canRetry: false);
            return;
        }

        try
        {
            if (controller is not null)
            {
                await controller.FlushAsync(CancellationToken.None);
            }
        }
        catch (Exception exception)
        {
            log?.Write("ERROR", "Vor dem Neustart mit Administratorrechten konnte nicht gesichert werden.", exception);
            mainWindow.ShowElevationNotice(
                $"Der Neustart wurde abgebrochen, weil nicht gesichert werden konnte: {exception.Message}",
                Elevation.CanRetry);
            return;
        }

        singleInstance?.Dispose();
        singleInstance = null;
        var result = ElevationStartupService.RequestElevation(
            executablePath,
            startupArguments,
            Program.StartElevated);
        if (result.Status == ElevationStartupStatus.Relaunched)
        {
            log?.Write("INFO", "Neustart mit Administratorrechten ausgelöst.");
            Shutdown(0);
            return;
        }

        log?.Write("WARN", result.Notice ?? "Der Neustart mit Administratorrechten ist fehlgeschlagen.");
        RestoreSingleInstance();
        mainWindow.ShowElevationNotice(
            $"{result.Notice} {ElevationNotice.RestrictionSummary}",
            Elevation.CanRetry);
    }

    private void RestoreSingleInstance()
    {
        if (synchronizationContext is null || singleInstance is not null)
        {
            return;
        }

        singleInstance = new SingleInstanceService(ProductInfo.InstanceKey, synchronizationContext);
        if (!singleInstance.IsPrimary)
        {
            return;
        }

        singleInstance.ActivationRequested += ActivateMainWindow;
        singleInstance.StartListening();
    }

    private void ActivateMainWindow()
    {
        if (mainWindow is null)
        {
            return;
        }

        mainWindow.Show();
        if (mainWindow.WindowState == WindowState.Minimized)
        {
            mainWindow.WindowState = WindowState.Normal;
        }

        mainWindow.Activate();
    }

    protected override void OnExit(ExitEventArgs eventArgs)
    {
        controller?.Dispose();
        themeService?.Dispose();
        singleInstance?.Dispose();
        base.OnExit(eventArgs);
    }
}
