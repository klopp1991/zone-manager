namespace SnapZones.App.Services;

/// <summary>Ergebnis einer Speicherung beim Beenden.</summary>
public enum ShutdownSaveOutcome
{
    /// <summary>Alles wurde vollständig gespeichert.</summary>
    Saved,

    /// <summary>Das Speichern ist fehlgeschlagen.</summary>
    Failed,

    /// <summary>Das Speichern war innerhalb der Zeitgrenze nicht abgeschlossen.</summary>
    TimedOut
}

/// <param name="Outcome">Wie die Speicherung ausgegangen ist.</param>
/// <param name="Failure">Die Ursache bei <see cref="ShutdownSaveOutcome.Failed"/>, sonst <c>null</c>.</param>
/// <param name="Timeout">Die überschrittene Zeitgrenze bei <see cref="ShutdownSaveOutcome.TimedOut"/>.</param>
public sealed record ShutdownSaveResult(ShutdownSaveOutcome Outcome, Exception? Failure, TimeSpan Timeout)
{
    public static ShutdownSaveResult Saved { get; } = new(ShutdownSaveOutcome.Saved, null, TimeSpan.Zero);

    public static ShutdownSaveResult Failed(Exception failure) =>
        new(ShutdownSaveOutcome.Failed, failure ?? throw new ArgumentNullException(nameof(failure)), TimeSpan.Zero);

    public static ShutdownSaveResult TimedOut(TimeSpan timeout) =>
        new(ShutdownSaveOutcome.TimedOut, null, timeout);

    public bool IsSaved => Outcome == ShutdownSaveOutcome.Saved;

    /// <summary>Erklärt dem Anwender, was beim Beenden nicht geklappt hat.</summary>
    public string Describe() => Outcome switch
    {
        ShutdownSaveOutcome.Saved => "Alle Änderungen wurden gespeichert.",
        ShutdownSaveOutcome.TimedOut =>
            $"Das Speichern war nach {Timeout.TotalSeconds:0} Sekunden noch nicht abgeschlossen. " +
            "Die zuletzt geänderten Einstellungen gehen beim Beenden möglicherweise verloren.",
        _ =>
            "Die Einstellungen konnten nicht gespeichert werden: " +
            $"{Failure?.Message ?? "unbekannte Ursache"}. " +
            "Die zuletzt geänderten Einstellungen gehen beim Beenden verloren.",
    };
}

public sealed class ExitSaveCoordinator
{
    private readonly ConfigurationSaveCoordinator saveCoordinator;

    public ExitSaveCoordinator(ConfigurationSaveCoordinator saveCoordinator)
    {
        this.saveCoordinator = saveCoordinator ?? throw new ArgumentNullException(nameof(saveCoordinator));
    }

    public Task PrepareForShutdownAsync(Action saveConfiguration)
    {
        ArgumentNullException.ThrowIfNull(saveConfiguration);
        saveConfiguration();
        return saveCoordinator.FlushAsync(CancellationToken.None);
    }

    /// <summary>
    /// Speichert und wartet höchstens <paramref name="timeout"/> lang auf den Abschluss.
    /// Diese Überladung wirft nicht: das Beenden darf weder an einem Speicherfehler noch an einem
    /// Flush hängen bleiben, der nicht zur Ruhe kommt. Der Aufrufer entscheidet anhand des Ergebnisses.
    /// </summary>
    /// <param name="saveConfiguration">Übergibt den aktuellen Stand an den Speicherlauf.</param>
    /// <param name="timeout">Obergrenze für den gesamten Vorgang.</param>
    /// <param name="additionalFlush">Optionaler zweiter Flush, etwa für Fensterplatzierungen.</param>
    public async Task<ShutdownSaveResult> TryPrepareForShutdownAsync(
        Action saveConfiguration,
        TimeSpan timeout,
        Func<CancellationToken, Task>? additionalFlush = null)
    {
        ArgumentNullException.ThrowIfNull(saveConfiguration);
        if (timeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout));
        }

        using var cancellation = new CancellationTokenSource(timeout);
        try
        {
            saveConfiguration();
            await saveCoordinator.FlushAsync(cancellation.Token);
            if (additionalFlush is not null)
            {
                await additionalFlush(cancellation.Token);
            }

            return ShutdownSaveResult.Saved;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            return ShutdownSaveResult.TimedOut(timeout);
        }
        catch (TimeoutException)
        {
            return ShutdownSaveResult.TimedOut(timeout);
        }
        catch (Exception exception)
        {
            return ShutdownSaveResult.Failed(exception);
        }
    }
}
