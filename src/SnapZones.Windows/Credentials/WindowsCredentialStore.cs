using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace SnapZones.Windows.Credentials;

/// <summary>
/// Legt ein Geheimnis in den Anmeldeinformations-Speicher von Windows und liest es dort wieder. Das ist
/// die einzige Stelle, an der ein Zugangsschluessel liegt: nie in einer Einstellungsdatei, nie in einer
/// Sicherung und nie im Protokoll. Der Eintrag gehoert dem angemeldeten Benutzer und wandert mit seinem
/// Profil (<c>CRED_PERSIST_ENTERPRISE</c>).
/// </summary>
public static class WindowsCredentialStore
{
    private const int GenericType = 1;
    private const int PersistEnterprise = 3;
    private const int NotFound = 1168;

    /// <summary>Schreibt oder ersetzt das Geheimnis unter diesem Ziel.</summary>
    public static void Write(string target, string userName, string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        ArgumentNullException.ThrowIfNull(secret);
        var blob = Encoding.Unicode.GetBytes(secret);
        var blobHandle = Marshal.AllocHGlobal(blob.Length);
        try
        {
            Marshal.Copy(blob, 0, blobHandle, blob.Length);
            var credential = new Credential
            {
                Type = GenericType,
                TargetName = target,
                CredentialBlobSize = blob.Length,
                CredentialBlob = blobHandle,
                Persist = PersistEnterprise,
                UserName = string.IsNullOrWhiteSpace(userName) ? target : userName
            };
            if (!CredWrite(ref credential, 0))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Der Zugangsschlüssel liess sich nicht ablegen.");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(blobHandle);
        }
    }

    /// <summary>Liest das Geheimnis, oder <c>null</c>, wenn keines hinterlegt ist.</summary>
    public static string? Read(string target)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        if (!CredRead(target, GenericType, 0, out var handle))
        {
            var error = Marshal.GetLastWin32Error();
            return error == NotFound
                ? null
                : throw new Win32Exception(error, "Der Zugangsschlüssel liess sich nicht lesen.");
        }

        try
        {
            var credential = Marshal.PtrToStructure<Credential>(handle);
            return credential.CredentialBlobSize <= 0
                ? null
                : Marshal.PtrToStringUni(credential.CredentialBlob, credential.CredentialBlobSize / 2);
        }
        finally
        {
            _ = CredFree(handle);
        }
    }

    /// <summary>Der Name, unter dem das Geheimnis abgelegt ist; leer, wenn keines hinterlegt ist.</summary>
    public static string? ReadUserName(string target)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        if (!CredRead(target, GenericType, 0, out var handle))
        {
            return null;
        }

        try
        {
            return Marshal.PtrToStructure<Credential>(handle).UserName;
        }
        finally
        {
            _ = CredFree(handle);
        }
    }

    /// <summary>Entfernt das Geheimnis; ein fehlender Eintrag ist kein Fehler.</summary>
    public static void Delete(string target)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        if (CredDelete(target, GenericType, 0))
        {
            return;
        }

        var error = Marshal.GetLastWin32Error();
        if (error != NotFound)
        {
            throw new Win32Exception(error, "Der Zugangsschlüssel liess sich nicht entfernen.");
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credential
    {
        public int Flags;
        public int Type;
        [MarshalAs(UnmanagedType.LPWStr)] public string TargetName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Comment;
        public long LastWritten;
        public int CredentialBlobSize;
        public nint CredentialBlob;
        public int Persist;
        public int AttributeCount;
        public nint Attributes;
        [MarshalAs(UnmanagedType.LPWStr)] public string? TargetAlias;
        [MarshalAs(UnmanagedType.LPWStr)] public string UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref Credential credential, int flags);

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string target, int type, int flags, out nint credential);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target, int type, int flags);

    [DllImport("advapi32.dll", EntryPoint = "CredFree")]
    private static extern bool CredFree(nint buffer);
}
