using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using SnapZones.Core.Elevation;

namespace SnapZones.Helper;

/// <summary>
/// Das Hilfsprogramm mit <c>uiAccess</c>. Es nimmt über eine benannte Pipe Platzierungsbefehle entgegen
/// und führt genau eine Sache aus: ein Fenster verschieben.
///
/// Warum es das überhaupt gibt: Windows lässt ein Programm nur Fenster derselben oder einer niedrigeren
/// Vertrauensstufe bewegen. Der übliche Ausweg wären Administratorrechte für das ganze Programm. Diese
/// Datei geht den anderen Weg — <c>uiAccess</c> hebt genau diese eine Schranke auf, ohne irgendein
/// Administratorrecht zu verleihen. Dafür verlangt Windows eine gültige Signatur und einen geschützten
/// Installationsort.
///
/// Weil sie mit diesem besonderen Recht läuft, ist sie absichtlich winzig und tut nichts weiter:
/// <list type="bullet">
///   <item>Sie öffnet keine Datei und schreibt keine.</item>
///   <item>Sie startet keinen Prozess und lädt nichts nach.</item>
///   <item>Sie sendet keine Fenstermeldungen und keine Tastatur- oder Mauseingaben.</item>
///   <item>Sie spricht mit niemandem ausser dem Hauptprogramm, und das muss sie sich beweisen.</item>
///   <item>Sie endet, sobald die Verbindung abreisst.</item>
/// </list>
/// </summary>
internal static class Program
{
    private const int Restore = 9;
    private const uint NoZOrder = 0x0004;
    private const uint NoActivate = 0x0010;
    private const uint NoOwnerZOrder = 0x0200;
    private static readonly TimeSpan ConnectionTimeout = TimeSpan.FromSeconds(30);

    [STAThread]
    public static int Main(string[] arguments)
    {
        if (arguments.Length != 1 || string.IsNullOrWhiteSpace(arguments[0]))
        {
            return 2;
        }

        var pipeName = arguments[0];
        if (pipeName.Length is < 8 or > 128 || pipeName.AsSpan().ContainsAny(['\\', '/', ':']))
        {
            return 2;
        }

        try
        {
            return Serve(pipeName);
        }
        catch (Exception)
        {
            return 1;
        }
    }

    private static int Serve(string pipeName)
    {
        // Nur der angemeldete Benutzer darf sich verbinden. Ohne diese Einschraenkung stuende die
        // Faehigkeit, hoeher berechtigte Fenster zu bewegen, jedem Konto auf dem Rechner offen.
        var security = new PipeSecurity();
        var user = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("Die Benutzerkennung ist nicht lesbar.");
        security.AddAccessRule(new PipeAccessRule(
            user,
            PipeAccessRights.ReadWrite,
            AccessControlType.Allow));

        using var server = NamedPipeServerStreamAcl.Create(
            pipeName,
            PipeDirection.InOut,
            maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            inBufferSize: 4096,
            outBufferSize: 4096,
            security);

        using var connection = new CancellationTokenSource(ConnectionTimeout);
        try
        {
            server.WaitForConnectionAsync(connection.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            // Niemand hat sich gemeldet. Ein Helfer ohne Gegenueber hat keinen Grund weiterzulaufen.
            return 3;
        }

        if (!IsExpectedClient(server))
        {
            return 4;
        }

        using var reader = new StreamReader(server, Encoding.UTF8, false, 4096, leaveOpen: true);
        using var writer = new StreamWriter(server, new UTF8Encoding(false), 4096, leaveOpen: true)
        {
            AutoFlush = true
        };

        while (server.IsConnected && reader.ReadLine() is { } line)
        {
            writer.WriteLine(Handle(line));
        }

        return 0;
    }

    /// <summary>
    /// Prüft, dass am anderen Ende wirklich das Hauptprogramm sitzt — dieselbe Programmdatei, im selben
    /// Verzeichnis wie der Helfer. Ohne diese Prüfung könnte jedes Programm des Benutzers die Pipe
    /// belegen und über den Helfer Fenster bewegen, die es selbst nicht anfassen dürfte.
    /// </summary>
    private static bool IsExpectedClient(NamedPipeServerStream server)
    {
        var clientPath = TryReadClientImagePath(server);
        if (clientPath is null)
        {
            return false;
        }

        var expected = Path.Combine(
            Path.GetDirectoryName(Environment.ProcessPath ?? string.Empty) ?? string.Empty,
            "ZoneManager.exe");
        return expected.Length > "ZoneManager.exe".Length &&
            string.Equals(
                Path.GetFullPath(clientPath),
                Path.GetFullPath(expected),
                StringComparison.OrdinalIgnoreCase);
    }

    private static string? TryReadClientImagePath(NamedPipeServerStream server)
    {
        if (!GetNamedPipeClientProcessId(server.SafePipeHandle.DangerousGetHandle(), out var processId) ||
            processId == 0)
        {
            return null;
        }

        using var process = OpenProcess(0x1000, false, processId);
        if (process.IsInvalid)
        {
            return null;
        }

        var capacity = 32768;
        var builder = new StringBuilder(capacity);
        return QueryFullProcessImageName(process, 0, builder, ref capacity) ? builder.ToString() : null;
    }

    private static string Handle(string line)
    {
        if (!HelperProtocol.TryParseRequest(line, out var request))
        {
            return HelperProtocol.BuildFailure("Unverständlicher Befehl");
        }

        if (request.Verb == HelperVerb.Ping)
        {
            return HelperProtocol.BuildPong();
        }

        if (!IsWindow(request.WindowHandle))
        {
            return HelperProtocol.BuildFailure("Das Fenster gibt es nicht mehr");
        }

        _ = ShowWindow(request.WindowHandle, Restore);
        return SetWindowPos(
                request.WindowHandle,
                0,
                request.X,
                request.Y,
                request.Width,
                request.Height,
                NoZOrder | NoActivate | NoOwnerZOrder)
            ? HelperProtocol.SuccessReply
            : HelperProtocol.BuildFailure("Windows hat die Platzierung abgelehnt");
    }

    // Bewusst klassisches DllImport statt LibraryImport: letzteres verlangt «unsafe» im ganzen Projekt.
    // In einem Programm, das mit uiAccess laeuft, ist der Verzicht darauf mehr wert als die etwas
    // schnellere Marshalling-Schicht, die hier ohnehin nie ins Gewicht faellt.
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint window);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint window, int command);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        nint window,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeClientProcessId(nint pipe, out uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern Microsoft.Win32.SafeHandles.SafeProcessHandle OpenProcess(
        uint desiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandle,
        uint processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(
        Microsoft.Win32.SafeHandles.SafeProcessHandle process,
        uint flags,
        StringBuilder exeName,
        ref int size);
}
