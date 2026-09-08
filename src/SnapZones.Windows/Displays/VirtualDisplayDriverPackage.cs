using System.IO;
using System.Reflection;

namespace SnapZones.Windows.Displays;

/// <summary>
/// Das eingebettete Treiberpaket «Virtual Display Driver» (github.com/VirtualDrivers/Virtual-Display-Driver,
/// MIT-Lizenz, signiert von der SignPath Foundation). Die vier Dateien liegen als Ressourcen in dieser
/// Assembly; fuer die Installation werden sie in ein Verzeichnis entpackt, weil Windows die INF-Datei
/// von der Platte liest.
/// </summary>
public static class VirtualDisplayDriverPackage
{
    public const string Version = "25.7.23";
    public const string InfFileName = "MttVDD.inf";
    public const string LicenseFileName = "LICENSE";
    private const string ResourcePrefix = "VirtualDisplayDriver/";

    public static readonly IReadOnlyList<string> FileNames = [InfFileName, "MttVDD.dll", "mttvdd.cat", LicenseFileName];

    /// <summary>Oeffnet eine Datei des Pakets als Strom.</summary>
    public static Stream Open(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        return typeof(VirtualDisplayDriverPackage).Assembly.GetManifestResourceStream(ResourcePrefix + fileName)
            ?? throw new FileNotFoundException($"Die Treiberdatei «{fileName}» fehlt in der Programmdatei.");
    }

    /// <summary>Entpackt alle Dateien in das Verzeichnis und liefert den Pfad der INF-Datei.</summary>
    public static string Extract(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        Directory.CreateDirectory(directory);
        foreach (var fileName in FileNames)
        {
            using var source = Open(fileName);
            using var target = File.Create(Path.Combine(directory, fileName));
            source.CopyTo(target);
        }

        return Path.Combine(directory, InfFileName);
    }
}
