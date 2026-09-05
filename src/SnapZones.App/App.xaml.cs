using System.Windows;
using System.Windows.Threading;
using System.IO;
using SnapZones.App.Services;
using SnapZones.App.ViewModels;
using SnapZones.App.Views;
using SnapZones.Core.Persistence;
using SnapZones.Core.Updates;
using SnapZones.Windows.Displays;
using SnapZones.Windows.Elevation;
using SnapZones.Windows.Setup;
using SnapZones.Windows.Startup;
using SnapZones.Core.Models;

namespace SnapZones.App;

public partial class App : System.Windows.Application
{
    /// <summary>Wie lange ein Nachfolgeprozess höchstens auf das Ende seines Vorgängers wartet.</summary>
    private static readonly TimeSpan PredecessorTimeout = TimeSpan.FromSeconds(60);

    /// <summary>Abstand, in dem die eigene Programmdatei geprüft wird.</summary>
    private static readonly TimeSpan IntegrityInterval = TimeSpan.FromSeconds(3);

    private SingleInstanceService? singleInstance;
    private ApplicationController? controller;
    private FileLog? log;
    private ThemeService? themeService;
    private ExecutableIntegrityWatch? integrityWatch;
    private IReadOnlyList<string> startupArguments = [];
    private int fatalHandling;
    private int suppressedFatalErrors;
    private int leaving;

    public void ApplyTheme(ThemeMode mode) => themeService?.Apply(mode);

    /// <summary>
    /// Das Verzeichnis, in das ein Update geladen wird, bevor es übernommen wird. Es liegt bewusst unter
    /// dem Produktnamen und nicht neben Einstellungen und Protokoll, die aus Kompatibilität unter
    /// <c>SnapZones</c> bleiben.
    /// </summary>
    public static string UpdateStagingDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZoneManager", "updates");

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
        // weiter, nicht aus dem Download-Ordner. Laeuft die Installation als erhoehter Hilfsprozess,
        // startet der Aufrufer das Programm selbst; sonst liefe es mit dessen Administratorrechten.
        if (startPath is { Length: > 0 } path && !StartupArguments.Contains(arguments, StartupArguments.NoLaunch))
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

    /// <summary>
    /// Der Modus <c>--install-certificate</c> / <c>--remove-certificate</c>: die Zertifikatsaktion in
    /// einem erhöhten Hilfsprozess, ohne Oberfläche. Ergebnis und Grund landen im Protokoll, der
    /// Beendigungscode beim Aufrufer.
    /// </summary>
    private void RunCertificateCommand(bool install)
    {
        var processPath = Environment.ProcessPath ?? throw new InvalidOperationException("Der Programmpfad fehlt.");
        var service = new SigningCertificateService();
        var result = install
            ? service.Install(HelperChannel.ResolvePath(processPath), TimeProvider.System.GetUtcNow())
            : service.Remove();
        log?.Write(
            result.Successful ? "INFO" : "ERROR",
            $"{(install ? "Zertifikat einrichten" : "Zertifikat entfernen")} im erhöhten Hilfsprozess: {result.Message}");
        Shutdown(result.Successful ? 0 : 1);
    }

    /// <summary>
    /// Der Modus <c>--apply-update</c>: dieser Prozess läuft aus der bereitgestellten Programmdatei,
    /// legt sie an die Stelle der bisherigen und startet sie von dort.
    /// </summary>
    private void RunApplyUpdate(string targetExecutablePath, IReadOnlyList<string> arguments)
    {
        var processPath = Environment.ProcessPath ?? throw new InvalidOperationException("Der Programmpfad fehlt.");
        var outcome = UpdateApplyRunner.Run(
            Path.GetDirectoryName(processPath) ?? UpdateStagingDirectory,
            targetExecutablePath,
            arguments,
            ElevationState.IsAdministrator(),
            (level, message, exception) => log?.Write(level, message, exception));
        if (outcome.ExitCode != 0 && !outcome.Relaunched)
        {
            System.Windows.MessageBox.Show(
                $"{outcome.Message}\n\nEinzelheiten stehen im Protokoll:\n{log?.FilePath ?? "(kein Protokoll)"}",
                $"{ProductInfo.Name} – Update",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        Shutdown(outcome.ExitCode);
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
        startupArguments = eventArgs.Args;
        var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SnapZones");
        var localData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SnapZones", "logs");
        // DEBUG-Zeilen (jedes Fensterereignis, jeder Ziehvorgang) nur auf ausdruecklichen Wunsch: sonst
        // verdraengen sie innerhalb eines Tages jeden Fehler aus dem Protokoll.
        var verbose = StartupArguments.Contains(eventArgs.Args, StartupArguments.Verbose);
        log = new FileLog(localData, verbose ? "DEBUG" : "INFO");
        var processPath = Environment.ProcessPath ?? throw new InvalidOperationException("Der Programmpfad fehlt.");

        // Ein Nachfolger wartet zuerst auf das Ende seines Vorgaengers: sonst faende er dessen
        // Einzelinstanz noch vor, aktivierte sie und beendete sich selbst.
        if (StartupArguments.TryReadWaitForPid(eventArgs.Args, out var predecessor) &&
            !ProcessWait.WaitForExit(predecessor, PredecessorTimeout))
        {
            log.Write("WARN", $"Der Vorgängerprozess {predecessor} lief nach {PredecessorTimeout.TotalSeconds:0} Sekunden noch.");
        }

        if (StartupArguments.ReadValue(eventArgs.Args, StartupArguments.ApplyUpdate) is { Length: > 0 } updateTarget)
        {
            RunApplyUpdate(updateTarget, eventArgs.Args);
            return;
        }

        var startupService = new StartupRegistration(
            processPath,
            message => log.Write("WARN", message),
            () => ElevationPreference.Read(appData) == ElevationMode.Always);

        if (StartupArguments.Contains(eventArgs.Args, "--diagnostics"))
        {
            var exitCode = await DiagnosticRunner.RunAsync(appData, startupService);
            Shutdown(exitCode);
            return;
        }

        // Installieren, Deinstallieren und die Zertifikatsaktionen laufen ohne Hauptfenster und ohne Hook.
        var setupMode = SetupRunner.Decide(eventArgs.Args);
        if (setupMode != SetupRunner.Mode.None)
        {
            RunSetup(setupMode, eventArgs.Args);
            return;
        }

        if (StartupArguments.Contains(eventArgs.Args, StartupArguments.InstallCertificate))
        {
            RunCertificateCommand(install: true);
            return;
        }

        if (StartupArguments.Contains(eventArgs.Args, StartupArguments.RemoveCertificate))
        {
            RunCertificateCommand(install: false);
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
        switch (startupDisposition)
        {
            case StartupDisposition.ActivateRunningInstance:
                singleInstance.NotifyPrimary();
                Shutdown();
                return;
            case StartupDisposition.StopRunningInstance:
                singleInstance.NotifyPrimaryExit();
                Shutdown();
                return;
            case StartupDisposition.ExitDuplicate:
                Shutdown();
                return;
            default:
                break;
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
            log,
            UpdateStagingDirectory,
            eventArgs.Args);
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
        singleInstance.ExitRequested += () =>
        {
            log.Write("INFO", "Eine zweite Instanz hat um das Beenden gebeten.");
            controller?.RequestExit();
        };
        singleInstance.StartListening();

        // Reste einer frueheren Bereitstellung: die uebernommene Version laeuft laengst von ihrem Platz.
        if (!UpdateInstaller.CleanStagingDirectory(UpdateStagingDirectory))
        {
            log.Write("DEBUG", "Das Bereitstellungsverzeichnis liess sich noch nicht vollständig leeren.");
        }

        MainWindow = mainWindow;
        if (startupDisposition == StartupDisposition.StartVisible)
        {
            mainWindow.Show();
        }

        // Der Weg fuer den Fall, dass die Programmdatei ersetzt wird, wird jetzt uebersetzt, solange
        // sie noch am Platz liegt.
        ProcessRestart.Warmup();
        integrityWatch = new ExecutableIntegrityWatch(
            processPath,
            IntegrityInterval,
            change => Dispatcher.InvokeAsync(() => HandleExecutableChanged(change)));
        if (!integrityWatch.IsArmed)
        {
            log.Write("WARN", "Die Programmdatei liess sich beim Start nicht lesen; ein Austausch wird nicht erkannt.");
        }
    }

    /// <summary>
    /// Faengt alle vier Fehlerkanaele ab: Dispatcher, das Infobereichssymbol (Windows Forms), fremde
    /// Threads und unbeobachtete Tasks. Frueher war nur der Dispatcher angeschlossen, und dessen Handler
    /// lief bei jedem Folgefehler erneut an: am 02.09.2026 fuenftausendmal in 42 Sekunden, ohne dass eine
    /// Ursache im Protokoll stand. Ein Fehler im Infobereichssymbol lief bis zum 05.09.2026 in den
    /// Standarddialog von Windows Forms, der selbst scheiterte und den eigentlichen Fehler verdeckte.
    /// </summary>
    private void RegisterGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += (_, exceptionArgs) =>
        {
            exceptionArgs.Handled = true;
            HandleFatal("Unbehandelter UI-Fehler.", exceptionArgs.Exception);
        };
        System.Windows.Forms.Application.ThreadException += (_, threadArgs) =>
            HandleFatal("Unbehandelter Fehler im Infobereichssymbol.", threadArgs.Exception);
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

    private void HandleFatal(string headline, Exception exception)
    {
        if (Interlocked.Exchange(ref fatalHandling, 1) != 0)
        {
            // Folgefehler waehrend der Notfallsicherung: nur zaehlen, nicht erneut behandeln.
            Interlocked.Increment(ref suppressedFatalErrors);
            return;
        }

        log?.Write("FATAL", headline, exception);
        _ = ShutdownAfterFatalErrorAsync(exception);
    }

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
            ShutdownSafely(4);
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

    /// <summary>
    /// Beendet die Anwendung über den gewöhnlichen WPF-Weg und weicht auf den harten Ausstieg aus, wenn
    /// der scheitert. Das geschieht, wenn die Programmdatei nicht mehr am Platz liegt: WPF lädt beim
    /// Herunterfahren noch Bausteine nach, und am 02.09.2026 blieb das Programm deshalb nach einem
    /// fehlgeschlagenen Beenden im Infobereich hängen.
    /// </summary>
    public void ShutdownSafely(int exitCode = 0)
    {
        try
        {
            Shutdown(exitCode);
        }
        catch (Exception exception)
        {
            log?.Write("ERROR", "Das geordnete Beenden ist fehlgeschlagen; das Programm endet direkt.", exception);
            ExitWithoutWpfShutdown(exitCode);
        }
    }

    /// <summary>
    /// Räumt Hooks, Infobereichssymbol und Einzelinstanz auf und beendet den Prozess, ohne WPF
    /// herunterzufahren. Nur für die Fälle, in denen nichts mehr nachgeladen werden darf.
    /// </summary>
    private void ExitWithoutWpfShutdown(int exitCode)
    {
        if (Interlocked.Exchange(ref leaving, 1) != 0)
        {
            return;
        }

        try
        {
            integrityWatch?.Dispose();
            controller?.Dispose();
            themeService?.Dispose();
            singleInstance?.Dispose();
        }
        catch (Exception exception)
        {
            log?.Write("ERROR", "Das Aufräumen vor dem direkten Beenden ist fehlgeschlagen.", exception);
        }

        Environment.Exit(exitCode);
    }

    /// <summary>
    /// Die Programmdatei wurde unter dem laufenden Prozess ersetzt oder entfernt. Von jetzt an scheitert
    /// jedes Nachladen; darum wird gespeichert, alles stillgelegt und — liegt eine neue Datei am Platz —
    /// in sie hinübergestartet, solange die dafür nötigen Bausteine noch geladen sind.
    /// </summary>
    private async Task HandleExecutableChanged(ExecutableChange change)
    {
        if (Volatile.Read(ref leaving) != 0 || Volatile.Read(ref fatalHandling) != 0)
        {
            return;
        }

        var processPath = Environment.ProcessPath;
        var replaced = change == ExecutableChange.Replaced && processPath is { Length: > 0 };
        log?.Write(
            "WARN",
            replaced
                ? "Die Programmdatei wurde ersetzt. Das Programm speichert, beendet sich und startet die neue Datei."
                : "Die Programmdatei wurde entfernt. Das Programm speichert und beendet sich.");

        var hidden = MainWindow is not { IsVisible: true };
        try
        {
            if (controller is not null)
            {
                await controller.PrepareForExecutableChangeAsync();
            }
        }
        catch (Exception exception)
        {
            log?.Write("ERROR", "Das Speichern vor dem Neustart ist fehlgeschlagen.", exception);
        }

        if (replaced)
        {
            _ = ProcessRestart.TryStart(
                processPath!,
                StartupArguments.ForSuccessor(startupArguments, Environment.ProcessId, hidden),
                (level, message, exception) => log?.Write(level, message, exception));
        }

        ExitWithoutWpfShutdown(0);
    }

    protected override void OnExit(ExitEventArgs eventArgs)
    {
        integrityWatch?.Dispose();
        controller?.Dispose();
        themeService?.Dispose();
        singleInstance?.Dispose();
        base.OnExit(eventArgs);
    }
}
