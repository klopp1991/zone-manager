using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SnapZones.App.Controls;

/// <summary>
/// Das Symbol eines Programms fuer die Listen «Fenster zuordnen» und «In Ruhe lassen». Steht nur der
/// Dateiname in der Zuordnung, wird die Datei in den Windows-Verzeichnissen gesucht; findet sich kein Symbol,
/// zeigt die Liste stattdessen zwei Buchstaben.
/// </summary>
public static class ProgramIcons
{
    private static readonly Dictionary<string, ProgramIcon> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Das Symbol eines Programms. Der Aufrufer bekommt sofort einen Platzhalter zurueck; die Datei wird
    /// im Hintergrund gelesen und das Ergebnis nachgemeldet. Ohne das haenge die Liste an jedem Pfad, der
    /// auf einem getrennten Netzlaufwerk liegt.
    /// </summary>
    public static ProgramIcon For(string? processPath)
    {
        var path = processPath?.Trim().Trim('"') ?? string.Empty;
        if (path.Length == 0)
        {
            return ProgramIcon.Unknown;
        }

        lock (Cache)
        {
            if (Cache.TryGetValue(path, out var cached))
            {
                return cached;
            }

            var pending = new ProgramIcon();
            Cache[path] = pending;
            _ = Task.Run(() => Load(path, pending));
            return pending;
        }
    }

    /// <summary>Liest Datei und Symbol ausserhalb des Oberflaechen-Threads und meldet das Ergebnis.</summary>
    private static void Load(string path, ProgramIcon target)
    {
        var resolved = Resolve(path);
        var icon = resolved is null ? null : Extract(resolved);
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            target.Complete(icon, resolved is null);
            return;
        }

        _ = dispatcher.InvokeAsync(() => target.Complete(icon, resolved is null));
    }

    /// <summary>Zwei Buchstaben als Ersatz fuer ein fehlendes Symbol, etwa «EX» fuer Explorer.exe.</summary>
    public static string Initials(string? name)
    {
        var fileName = Path.GetFileNameWithoutExtension(name?.Trim().Trim('"') ?? string.Empty);
        if (fileName.Length == 0)
        {
            return "?";
        }

        var letters = new string(fileName.Where(char.IsLetterOrDigit).Take(2).ToArray());
        return (letters.Length == 0 ? fileName[..Math.Min(2, fileName.Length)] : letters).ToUpperInvariant();
    }

    private static string? Resolve(string path)
    {
        if (path.Contains('\\', StringComparison.Ordinal) || path.Contains('/', StringComparison.Ordinal))
        {
            return File.Exists(path) ? path : null;
        }

        foreach (var directory in new[]
                 {
                     Environment.SystemDirectory,
                     Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                     Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
                 })
        {
            if (directory.Length == 0)
            {
                continue;
            }

            var candidate = Path.Combine(directory, path);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static ImageSource? Extract(string path)
    {
        try
        {
            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(path);
            if (icon is null)
            {
                return null;
            }

            var source = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                System.Windows.Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }
}

/// <summary>
/// Das Symbol eines Programms, solange es noch geladen wird und danach. Die Oberflaeche bindet daran und
/// bekommt das Ergebnis nachgereicht, ohne auf die Datei zu warten.
/// </summary>
public sealed class ProgramIcon : INotifyPropertyChanged
{
    /// <summary>Fuer einen leeren Pfad: es gibt nichts zu laden und nichts zu bemaengeln.</summary>
    public static ProgramIcon Unknown { get; } = new() { IsLoaded = true };

    private ImageSource? image;
    private bool isMissing;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Das Symbol der Programmdatei, oder <c>null</c>.</summary>
    public ImageSource? Image => image;

    /// <summary>Ob die Programmdatei nicht gefunden wurde – das Programm ist wohl deinstalliert.</summary>
    public bool IsMissing => isMissing;

    /// <summary>Sichtbarkeit des grauen Platzhalters fuer eine fehlende Programmdatei.</summary>
    public Visibility MissingVisibility => isMissing ? Visibility.Visible : Visibility.Collapsed;

    private bool IsLoaded { get; init; }

    internal void Complete(ImageSource? loadedImage, bool missing)
    {
        image = loadedImage;
        isMissing = missing;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Image)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsMissing)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MissingVisibility)));
    }
}

/// <summary>Bindet einen Programmpfad an sein Symbol; das Symbol kommt nach, sobald es gelesen ist.</summary>
public sealed class ProgramIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        _ = targetType;
        _ = parameter;
        _ = culture;
        return ProgramIcons.For(value as string);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Bindet einen Programmnamen an seine zwei Ersatzbuchstaben.</summary>
public sealed class ProgramInitialsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        _ = targetType;
        _ = parameter;
        _ = culture;
        return ProgramIcons.Initials(value as string);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
