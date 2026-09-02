using System.IO;
using System.Runtime.InteropServices;

namespace SnapZones.Windows.Setup;

/// <summary>
/// Schreibt eine Windows-Verknüpfung (<c>.lnk</c>).
///
/// Es gibt dafür keine verwaltete Schnittstelle; das Format wird ausschliesslich über die
/// COM-Schnittstellen <c>IShellLink</c> und <c>IPersistFile</c> erzeugt. Beide sind dokumentiert und
/// seit Windows 95 unverändert. Eine Verknüpfung von Hand zu schreiben käme nicht in Frage — das Format
/// ist binär und undokumentiert.
/// </summary>
public static class ShellLink
{
    public static void Create(string shortcutPath, string targetPath, string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(shortcutPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);

        var link = (IShellLinkW)new ShellLinkCoClass();
        try
        {
            link.SetPath(targetPath);
            link.SetWorkingDirectory(Path.GetDirectoryName(targetPath) ?? string.Empty);
            link.SetDescription(description ?? string.Empty);
            link.SetIconLocation(targetPath, 0);
            ((IPersistFile)link).Save(shortcutPath, fRemember: true);
        }
        finally
        {
            _ = Marshal.FinalReleaseComObject(link);
        }
    }

    [ComImport]
    [Guid("00021401-0000-0000-C000-000000000046")]
    [ClassInterface(ClassInterfaceType.None)]
    private sealed class ShellLinkCoClass : IShellLinkW
    {
        // Die Methoden liefert COM; die Deklaration nennt nur die Schnittstelle, damit der Cast erlaubt ist.
        public extern void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] char[] file, int maxPath, nint findData, uint flags);
        public extern void GetIDList(out nint idList);
        public extern void SetIDList(nint idList);
        public extern void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] char[] name, int maxName);
        public extern void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
        public extern void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] char[] directory, int maxPath);
        public extern void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);
        public extern void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] char[] arguments, int maxArguments);
        public extern void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
        public extern void GetHotkey(out short hotkey);
        public extern void SetHotkey(short hotkey);
        public extern void GetShowCmd(out int showCommand);
        public extern void SetShowCmd(int showCommand);
        public extern void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] char[] iconPath, int iconPathLength, out int iconIndex);
        public extern void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int iconIndex);
        public extern void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string relativePath, uint reserved);
        public extern void Resolve(nint window, uint flags);
        public extern void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] char[] file, int maxPath, nint findData, uint flags);
        void GetIDList(out nint idList);
        void SetIDList(nint idList);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] char[] name, int maxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] char[] directory, int maxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] char[] arguments, int maxArguments);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
        void GetHotkey(out short hotkey);
        void SetHotkey(short hotkey);
        void GetShowCmd(out int showCommand);
        void SetShowCmd(int showCommand);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] char[] iconPath, int iconPathLength, out int iconIndex);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int iconIndex);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string relativePath, uint reserved);
        void Resolve(nint window, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("0000010B-0000-0000-C000-000000000046")]
    private interface IPersistFile
    {
        void GetClassID(out Guid classId);
        [PreserveSig] int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string fileName, uint mode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string fileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string fileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string fileName);
    }
}
