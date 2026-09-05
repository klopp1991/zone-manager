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
    /// Übersetzt <see cref="TryStart"/> sofort und lädt damit alles, was der Aufruf braucht. Ohne diesen
    /// Schritt geschähe das erst beim ersten Aufruf — zu spät, wenn die Programmdatei dann nicht mehr
    /// am Platz liegt.
    /// </summary>
    public static void Warmup()
    {
        var method = typeof(ProcessRestart).GetMethod(nameof(TryStart));
        if (method is not null)
        {
            RuntimeHelpers.PrepareMethod(method.MethodHandle);
        }
    }

    public static bool TryStart(string executablePath, IReadOnlyList<string> arguments, Action<string, string, Exception?> log)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(log);

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory,
            UseShellExecute = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

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
