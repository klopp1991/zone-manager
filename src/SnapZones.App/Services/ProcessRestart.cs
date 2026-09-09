using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace SnapZones.App.Services;

/// <summary>
/// Startet einen Nachfolgeprozess der eigenen Programmdatei. Wird gebraucht, wenn die Datei unter dem
/// laufenden Prozess ersetzt wurde; dann darf nichts mehr nachgeladen werden, weshalb der Weg vorab
/// übersetzt wird (<see cref="Warmup"/>).
/// </summary>
public static class ProcessRestart
{
    /// <summary>
    /// Übersetzt <see cref="TryStart"/> und <see cref="BuildStartInfo"/> sofort und lädt damit alles, was
    /// der Aufruf braucht. Ohne diesen Schritt geschähe das erst beim ersten Aufruf — zu spät, wenn die
    /// Programmdatei dann nicht mehr am Platz liegt.
    /// </summary>
    public static void Warmup()
    {
        foreach (var name in new[] { nameof(TryStart), nameof(BuildStartInfo) })
        {
            var method = typeof(ProcessRestart).GetMethod(name);
            if (method is not null)
            {
                RuntimeHelpers.PrepareMethod(method.MethodHandle);
            }
        }
    }

    /// <summary>
    /// Der Startbefehl für die eigene Programmdatei — ohne den Umweg über die Shell.
    ///
    /// <para>
    /// <c>UseShellExecute = true</c> reicht den Start an die Shell weiter, und die prüft die Zone der
    /// Datei: liegt das Programm auf einem Netzlaufwerk oder trägt es die Herkunftsmarke eines Downloads,
    /// legt sie den Dialog «Datei öffnen - Sicherheitswarnung» vor. Der Start kehrt dann erst nach einem
    /// Klick zurück — bei einem Neustart nach dem Update wartet niemand darauf, und das Programm bliebe
    /// unsichtbar aus. Ohne Shell startet Windows die Datei unmittelbar; die Zonenprüfung entfällt.
    /// </para>
    /// </summary>
    public static ProcessStartInfo BuildStartInfo(string executablePath, IReadOnlyList<string> arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(arguments);
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory,
            UseShellExecute = false
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }

    public static bool TryStart(string executablePath, IReadOnlyList<string> arguments, Action<string, string, Exception?> log)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(log);

        var startInfo = BuildStartInfo(executablePath, arguments);
        try
        {
            using var process = Process.Start(startInfo);
            if (process is not null)
            {
                return true;
            }

            log("ERROR", $"Windows hat {executablePath} nicht gestartet.", null);
            return false;
        }
        catch (Exception exception)
        {
            log("ERROR", $"{executablePath} liess sich nicht starten.", exception);
            return false;
        }
    }
}
