using System.ComponentModel;
using System.Diagnostics;
using System.IO;

namespace SnapZones.App.Services;

public enum ElevationEscalationStatus
{
    /// <summary>Der Hinweis wurde in dieser Sitzung schon gezeigt; es wird nicht erneut gefragt.</summary>
    AlreadyOffered,

    /// <summary>Der Benutzer hat abgelehnt. In dieser Sitzung wird nicht mehr gefragt.</summary>
    Declined,

    /// <summary>Der erhöhte Neustart läuft; der eigene Prozess muss enden.</summary>
    Restarting,

    /// <summary>Der Neustart ist fehlgeschlagen oder wurde in der UAC-Abfrage abgebrochen.</summary>
    Failed
}

public sealed record ElevationEscalationResult(ElevationEscalationStatus Status, string? Message = null);

/// <summary>
/// Der Nachfragepfad für den Fall, dass ein Fenster nur mit Administratorrechten bewegt werden kann.
///
/// Gefragt wird höchstens <b>einmal je Sitzung</b>. Ein Hinweis, der bei jedem höher berechtigten
/// Fenster erneut aufspringt, wird weggeklickt statt gelesen und macht das Programm unbenutzbar —
/// gerade weil solche Fenster oft in Serie auftreten.
/// </summary>
public sealed class ElevationEscalation
{
    private readonly Func<string, bool> ask;
    private readonly Func<ProcessStartInfo, bool> startElevated;
    private bool offered;

    public ElevationEscalation(
        Func<string, bool> ask,
        Func<ProcessStartInfo, bool>? startElevated = null)
    {
        this.ask = ask ?? throw new ArgumentNullException(nameof(ask));
        this.startElevated = startElevated ?? DefaultStart;
    }

    /// <summary>Ob in dieser Sitzung bereits gefragt wurde.</summary>
    public bool HasOffered => offered;

    public ElevationEscalationResult Offer(string executablePath, IReadOnlyList<string> arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(arguments);

        if (offered)
        {
            return new ElevationEscalationResult(ElevationEscalationStatus.AlreadyOffered);
        }

        offered = true;
        if (!ask(BuildQuestion()))
        {
            return new ElevationEscalationResult(ElevationEscalationStatus.Declined);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = Path.GetDirectoryName(executablePath) ?? AppContext.BaseDirectory,
            UseShellExecute = true,
            Verb = "runas"
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        try
        {
            return startElevated(startInfo)
                ? new ElevationEscalationResult(ElevationEscalationStatus.Restarting)
                : new ElevationEscalationResult(
                    ElevationEscalationStatus.Failed,
                    "Windows hat keinen erhöhten Prozess gestartet.");
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
            return new ElevationEscalationResult(
                ElevationEscalationStatus.Failed,
                "Die Windows-Abfrage wurde abgebrochen. Das Programm läuft mit gewöhnlichen Rechten weiter.");
        }
        catch (Exception exception)
        {
            return new ElevationEscalationResult(ElevationEscalationStatus.Failed, exception.Message);
        }
    }

    /// <summary>
    /// Der Text der Nachfrage. Er nennt zuerst, was gerade nicht ging, dann warum, dann was ein Ja
    /// bedeutet — und ausdrücklich, dass ein Nein alles andere unberührt lässt.
    /// </summary>
    public static string BuildQuestion() =>
        "Dieses Fenster gehört einem Programm mit höheren Rechten — etwa dem Taskmanager oder einem " +
        "Programm, das «als Administrator» gestartet wurde. Windows erlaubt es nur einem ebenso " +
        "berechtigten Programm, ein solches Fenster zu verschieben.\n\n" +
        "Soll Zone Manager mit Administratorrechten neu starten? Windows fragt dann einmal " +
        "nach, und die Snap-Funktion greift anschliessend auch bei diesen Fenstern.\n\n" +
        "Bei «Nein» läuft alles wie bisher weiter; nur diese eine Sorte Fenster bleibt unberührt. " +
        "Diese Frage erscheint in dieser Sitzung nicht noch einmal.";

    private static bool DefaultStart(ProcessStartInfo startInfo)
    {
        using var process = Process.Start(startInfo);
        return process is not null;
    }
}
