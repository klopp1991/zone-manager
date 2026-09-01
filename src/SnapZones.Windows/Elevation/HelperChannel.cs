using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using SnapZones.Core.Elevation;

namespace SnapZones.Windows.Elevation;

public enum HelperState
{
    /// <summary>Der Helfer wurde noch nicht gebraucht.</summary>
    Idle,

    /// <summary>Der Helfer läuft und antwortet.</summary>
    Ready,

    /// <summary>Die Datei fehlt — das Programm ist nicht installiert oder der Helfer wurde entfernt.</summary>
    Missing,

    /// <summary>
    /// Windows verweigert den Start. Das ist der Normalfall ohne gültige Signatur; ein Programm mit
    /// <c>uiAccess</c> startet ohne sie gar nicht.
    /// </summary>
    Rejected,

    /// <summary>Der Helfer startet zwar, antwortet aber nicht wie erwartet.</summary>
    Unresponsive
}

public sealed record HelperStatus(HelperState State, string Message);

/// <summary>
/// Die Verbindung zum Hilfsprogramm mit <c>uiAccess</c>.
///
/// Der Helfer wird bei Bedarf gestartet, bekommt einen für diesen Lauf zufälligen Pipenamen und bleibt
/// stehen, solange die Verbindung hält. Antwortet er nicht oder lässt Windows ihn nicht starten, wird das
/// gemeldet und der Aufrufer geht seinen bisherigen Weg — die Snap-Funktion darf daran nie scheitern.
/// </summary>
public sealed class HelperChannel : IDisposable
{
    public const string ExecutableName = "ZoneManager.Helper.exe";

    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ReplyTimeout = TimeSpan.FromSeconds(5);
    private readonly string helperPath;
    private readonly Action<string, string, Exception?> log;
    private readonly object gate = new();
    private Process? process;
    private NamedPipeClientStream? pipe;
    private StreamReader? reader;
    private StreamWriter? writer;
    private bool disposed;

    public HelperChannel(string helperPath, Action<string, string, Exception?> log)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(helperPath);
        this.helperPath = helperPath;
        this.log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public HelperStatus Status { get; private set; } = new(HelperState.Idle, "Noch nicht gestartet.");

    /// <summary>Der erwartete Ort des Helfers neben der Programmdatei.</summary>
    public static string ResolvePath(string executablePath) =>
        Path.Combine(Path.GetDirectoryName(executablePath) ?? string.Empty, ExecutableName);

    /// <summary>
    /// Verschiebt ein Fenster über den Helfer. Liefert <c>false</c>, wenn der Helfer nicht bereitsteht
    /// oder Windows die Platzierung ablehnt; der Aufrufer entscheidet dann selbst, was er tut.
    /// </summary>
    public bool TryPlace(nint windowHandle, int x, int y, int width, int height)
    {
        lock (gate)
        {
            if (disposed || !EnsureConnectedLocked())
            {
                return false;
            }

            var reply = ExchangeLocked(HelperProtocol.BuildPlace(windowHandle, x, y, width, height));
            if (reply is null)
            {
                Status = new HelperStatus(HelperState.Unresponsive, "Der Fensterhelfer antwortet nicht mehr.");
                DisconnectLocked();
                return false;
            }

            if (HelperProtocol.IsSuccess(reply))
            {
                return true;
            }

            log("DEBUG", $"Der Fensterhelfer lehnte ab: {HelperProtocol.ReadFailureReason(reply)}", null);
            return false;
        }
    }

    /// <summary>Startet den Helfer und prüft, ob er antwortet. Für die Anzeige in den Einstellungen.</summary>
    public HelperStatus Probe()
    {
        lock (gate)
        {
            if (disposed)
            {
                return new HelperStatus(HelperState.Missing, "Das Programm wird beendet.");
            }

            _ = EnsureConnectedLocked();
            return Status;
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            DisconnectLocked();
        }
    }

    private bool EnsureConnectedLocked()
    {
        if (pipe is { IsConnected: true } && process is { HasExited: false })
        {
            return true;
        }

        DisconnectLocked();

        if (!File.Exists(helperPath))
        {
            Status = new HelperStatus(
                HelperState.Missing,
                "Der Fensterhelfer ist nicht vorhanden. Er entsteht bei der Installation.");
            return false;
        }

        // Der Pipename ist je Lauf zufaellig. Ein fester Name liesse sich von einem anderen Programm
        // vorab belegen, das dann Befehle des Hauptprogramms entgegennaehme.
        var pipeName = "ZoneManagerHelper-" + Guid.NewGuid().ToString("N");
        try
        {
            process = Process.Start(new ProcessStartInfo
            {
                FileName = helperPath,
                WorkingDirectory = Path.GetDirectoryName(helperPath) ?? AppContext.BaseDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                ArgumentList = { pipeName }
            });
        }
        catch (Exception exception)
        {
            // Fehler 740 heisst: Windows verweigert den Start, weil Signatur oder Ort nicht stimmen.
            Status = new HelperStatus(
                HelperState.Rejected,
                "Windows hat den Start des Fensterhelfers abgelehnt. Ohne gültige Signatur an einem "
                    + $"geschützten Ort ist das erwartet. ({exception.Message})");
            process = null;
            return false;
        }

        if (process is null)
        {
            Status = new HelperStatus(HelperState.Rejected, "Windows hat den Fensterhelfer nicht gestartet.");
            return false;
        }

        try
        {
            pipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            pipe.Connect((int)StartTimeout.TotalMilliseconds);
            reader = new StreamReader(pipe, Encoding.UTF8, false, 4096, leaveOpen: true);
            writer = new StreamWriter(pipe, new UTF8Encoding(false), 4096, leaveOpen: true) { AutoFlush = true };
        }
        catch (Exception exception)
        {
            Status = new HelperStatus(
                HelperState.Unresponsive,
                $"Der Fensterhelfer war nicht erreichbar: {exception.Message}");
            DisconnectLocked();
            return false;
        }

        var reply = ExchangeLocked(HelperProtocol.BuildPing());
        if (!HelperProtocol.TryParsePong(reply, out var version) || version != HelperProtocol.Version)
        {
            Status = new HelperStatus(
                HelperState.Unresponsive,
                "Der Fensterhelfer meldete sich nicht mit der erwarteten Version.");
            DisconnectLocked();
            return false;
        }

        Status = new HelperStatus(
            HelperState.Ready,
            "Der Fensterhelfer läuft. Fenster höher berechtigter Programme lassen sich einrasten, "
                + "ohne dass das Programm selbst Administratorrechte besitzt.");
        return true;
    }

    private string? ExchangeLocked(string request)
    {
        if (writer is null || reader is null)
        {
            return null;
        }

        try
        {
            writer.WriteLine(request);
            var read = reader.ReadLineAsync();
            return read.Wait(ReplyTimeout) ? read.Result : null;
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException or AggregateException)
        {
            return null;
        }
    }

    private void DisconnectLocked()
    {
        reader?.Dispose();
        writer?.Dispose();
        pipe?.Dispose();
        reader = null;
        writer = null;
        pipe = null;

        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                // Der Helfer endet von selbst, sobald die Pipe schliesst; das hier ist der Notnagel.
                process.Kill(entireProcessTree: false);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
        }
        finally
        {
            process.Dispose();
            process = null;
        }
    }
}
