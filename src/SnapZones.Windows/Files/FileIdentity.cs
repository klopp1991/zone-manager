using System.IO;
using SnapZones.Windows.Native;

namespace SnapZones.Windows.Files;

/// <summary>
/// Die Kennung einer Datei auf ihrem Datenträger: Datenträgernummer und Dateiindex. Sie überlebt ein
/// Umbenennen und unterscheidet zwei Dateien auch dann, wenn Grösse und Zeitstempel übereinstimmen —
/// NTFS gibt einer neuen Datei gleichen Namens bis zu 15 Sekunden lang die Erstellzeit der gerade
/// weggeschobenen («Tunneling»), und ein Kopieren übernimmt die Änderungszeit.
/// </summary>
public readonly record struct FileIdentity(ulong VolumeSerialNumber, ulong FileIndex)
{
    /// <summary>Ob die Kennung überhaupt etwas aussagt; manche Netzlaufwerke liefern keine.</summary>
    public bool IsKnown => FileIndex != 0;

    /// <summary>
    /// Liest die Kennung, ohne die Datei zu sperren. Liefert <c>null</c>, wenn die Datei fehlt, nicht
    /// lesbar ist oder der Datenträger keine Kennung führt.
    /// </summary>
    public static FileIdentity? TryRead(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 1,
                FileOptions.None);
            if (!Kernel32.GetFileInformationByHandle(stream.SafeFileHandle, out var information))
            {
                return null;
            }

            var index = ((ulong)information.FileIndexHigh << 32) | information.FileIndexLow;
            var identity = new FileIdentity(information.VolumeSerialNumber, index);
            return identity.IsKnown ? identity : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return null;
        }
    }
}
