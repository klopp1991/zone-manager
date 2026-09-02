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
        RegisterGlobalExceptionHandlers();
        try
        {
            await StartAsync(eventArgs);
        }
        catch (Exception exception)
        {
            log?.Write("FATAL", $"{ProductInfo.Name} konnte nicht gestartet werden.", exception);
            System.Windows.MessageBox.Show(
                $"{exception.Message}\n\nEinzelheiten stehen im Protokoll:\n{log?.FilePath ?? "(kein Protokoll)"}",
                $"{ProductInfo.Name} konnte nicht gestartet werden",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(5);
        }
    }

    private async Task StartAsync(StartupEventArgs eventArgs)
    {
        var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SnapZones");
        var localData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SnapZones", "logs");
        // DEBUG-Zeilen (jedes Fensterereignis, jeder Ziehvorgang) nur auf ausdruecklichen Wunsch: sonst
        // verdraengen sie innerhalb eines Tages jeden Fehler aus dem Protokoll.
        var verbose = eventArgs.Args.Contains("--verbose", StringComparer.OrdinalIgnoreCase);
        log = new FileLog(localData, verbose ? "DEBUG" : "INFO");
        var startupService = new StartupRegistration(
            Environment.ProcessPath ?? throw new InvalidOperationException("Der Programmpfad fehlt."),
            message => log.Write("WARN", message),
            () => ElevationPreference.Read(appData) == ElevationMode.Always);

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

        var repository = new JsonConfigurationRepository(appData);
        var loadResult = await repository.LoadAsync(CancellationToken.None);
        var placementRepository = new JsonWindowPlacementRepository(appData);
        var placementLoadResult = await WindowPlacementStartupLoad.Start(
            placementRepository,
            CancellationToken.None);
        themeService = new ThemeService();
        themeService.Apply(loadResult.Configuration.Settings.ThemeMode);
        var monitorService = new WindowsMonitorService();
        var monitors = monitorService.GetMonitors();
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
            monitorService,
            startupService,
            log);
        SessionEnding += (_, _) => controller?.PrepareForSessionEnd();
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

        MainWindow = mainWindow;
        if (startupDisposition == StartupDisposition.StartVisible)
        {
            mainWindow.Show();
        }
    }

    /// <summary>
    /// Faengt alle drei Fehlerkanaele ab: Dispatcher, fremde Threads und unbeobachtete Tasks. Frueher war
    /// nur der Dispatcher angeschlossen, und dessen Handler lief bei jedem Folgefehler erneut an: am
    /// 02.09.2026 fuenftausendmal in 42 Sekunden, ohne dass eine Ursache im Protokoll stand.
    /// </summary>
    private void RegisterGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += (_, exceptionArgs) =>
        {
            exceptionArgs.Handled = true;
            if (Interlocked.Exchange(ref fatalHandling, 1) != 0)
            {
                // Folgefehler waehrend der Notfallsicherung: nur zaehlen, nicht erneut behandeln.
                Interlocked.Increment(ref suppressedFatalErrors);
                return;
            }

            log?.Write("FATAL", "Unbehandelter UI-Fehler.", exceptionArgs.Exception);
            _ = ShutdownAfterFatalErrorAsync(exceptionArgs.Exception);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, unhandledArgs) =>
        {
            log?.Write(
                "FATAL",
                "Unbehandelter Fehler ausserhalb der Oberfläche; das Programm wird beendet.",
                unhandledArgs.ExceptionObject as Exception);
            TryEmergencyFlush();
        };
        TaskScheduler.UnobservedTaskException += (_, taskArgs) =>
        {
            log?.Write("ERROR", "Unbeobachteter Fehler in einer Hintergrundaufgabe.", taskArgs.Exception);
            taskArgs.SetObserved();
        };
    }

    private int fatalHandling;
    private int suppressedFatalErrors;

    private async Task ShutdownAfterFatalErrorAsync(Exception exception)
    {
        try
        {
            controller?.EmergencyStop("Sicherheitsstopp nach einem UI-Fehler");
            if (controller is not null)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try
                {
                    await controller.FlushAsync(timeout.Token);
                }
                catch (Exception saveException)
                {
                    log?.Write("ERROR", "Die Notfallsicherung ist fehlgeschlagen.", saveException);
                }
            }

            var suppressed = Volatile.Read(ref suppressedFatalErrors);
            if (suppressed > 0)
            {
                log?.Write("WARN", $"{suppressed} Folgefehler während der Notfallsicherung unterdrückt.");
            }

            System.Windows.MessageBox.Show(
                $"Ein interner Fehler hat {ProductInfo.Name} gestoppt:\n{exception.Message}\n\n"
                    + $"Die Einstellungen wurden gesichert. Einzelheiten stehen im Protokoll:\n{log?.FilePath}",
                $"{ProductInfo.Name} wurde beendet",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            Shutdown(4);
        }
    }

    private void TryEmergencyFlush()
    {
        try
        {
            controller?.EmergencyStop("Sicherheitsstopp nach einem Fehler ausserhalb der Oberfläche");
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            controller?.FlushAsync(timeout.Token).GetAwaiter().GetResult();
        }
        catch (Exception saveException)
        {
            log?.Write("ERROR", "Die Notfallsicherung ist fehlgeschlagen.", saveException);
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
