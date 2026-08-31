using System.IO;

namespace ZoneManager.App.Services;

public sealed class FileLog
{
    private const long MaximumBytes = 1_048_576;
    private readonly string filePath;
    private readonly object gate = new();

    public FileLog(string directoryPath)
    {
        Directory.CreateDirectory(directoryPath);
        filePath = Path.Combine(directoryPath, "zonemanager.log");
    }

    public void Write(string level, string message, Exception? exception = null)
    {
        lock (gate)
        {
            RotateIfNeeded();
            var detail = exception is null ? string.Empty : $" | {exception.GetType().Name}: {exception.Message}";
            File.AppendAllText(filePath, $"{DateTimeOffset.Now:O} [{level}] {message}{detail}{Environment.NewLine}");
        }
    }

    private void RotateIfNeeded()
    {
        if (!File.Exists(filePath) || new FileInfo(filePath).Length < MaximumBytes)
        {
            return;
        }

        var previous = filePath + ".1";
        if (File.Exists(previous))
        {
            File.Delete(previous);
        }

        File.Move(filePath, previous);
    }
}
