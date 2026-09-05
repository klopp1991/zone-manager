using System.Globalization;
using System.IO;
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
    private static readonly Dictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static ImageSource? For(string? processPath)
    {
        var path = processPath?.Trim().Trim('"') ?? string.Empty;
        if (path.Length == 0)
        {
            return null;
        }

        lock (Cache)
        {
            if (Cache.TryGetValue(path, out var cached))
            {
                return cached;
            }
        }

        var resolved = Resolve(path);
        var icon = resolved is null ? null : Extract(resolved);
        lock (Cache)
        {
            Cache[path] = icon;
        }

        return icon;
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

/// <summary>Bindet einen Programmpfad an sein Symbol; ohne Symbol liefert der Konverter null.</summary>
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
