using System.Globalization;
using SnapZones.Core.Persistence;

namespace SnapZones.App.ViewModels;

/// <summary>Ein frueherer Stand, wie die Liste auf der Seite «Programm» ihn zeigt.</summary>
public sealed record BackupListItem(ConfigurationBackup Backup)
{
    /// <summary>«Heute 09:41», «Gestern 18:02» oder «02.09. 17:45».</summary>
    public string WhenText => Describe(Backup.SavedAt, DateTimeOffset.Now);

    public string Summary => Backup.Summary;

    public bool CanRestore => Backup.IsReadable;

    public static string Describe(DateTimeOffset savedAt, DateTimeOffset now)
    {
        var culture = CultureInfo.CurrentCulture;
        var time = savedAt.ToString("HH:mm", culture);
        var days = (now.Date - savedAt.Date).Days;
        return days switch
        {
            0 => $"Heute {time}",
            1 => $"Gestern {time}",
            _ => $"{savedAt.ToString("dd.MM.", culture)} {time}"
        };
    }
}
