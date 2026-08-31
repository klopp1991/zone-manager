using System.Runtime.InteropServices;

namespace SnapZones.Windows.Native;

internal static class Shell32
{
    private const ushort StringPointerVariant = 31;
    private static readonly Guid PropertyStoreInterfaceId = new("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");
    private static readonly PropertyKey AppUserModelIdKey = new(
        new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 5);

    internal static string? TryReadAppUserModelId(nint window)
    {
        IPropertyStore? store = null;
        var value = default(PropVariant);
        try
        {
            var iid = PropertyStoreInterfaceId;
            if (SHGetPropertyStoreForWindow(window, ref iid, out store) < 0 || store is null)
            {
                return null;
            }

            var key = AppUserModelIdKey;
            if (store.GetValue(ref key, out value) < 0 || value.VariantType != StringPointerVariant || value.PointerValue == 0)
            {
                return null;
            }

            return Marshal.PtrToStringUni(value.PointerValue);
        }
        catch
        {
            return null;
        }
        finally
        {
            _ = PropVariantClear(ref value);
            if (store is not null && Marshal.IsComObject(store))
            {
                _ = Marshal.ReleaseComObject(store);
            }
        }
    }

    [DllImport("shell32.dll")]
    private static extern int SHGetPropertyStoreForWindow(nint window, ref Guid iid,
        [MarshalAs(UnmanagedType.Interface)] out IPropertyStore? store);

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropVariant value);

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
        [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant value);
        [PreserveSig] int Commit();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey(Guid formatId, uint propertyId)
    {
        public Guid FormatId = formatId;
        public uint PropertyId = propertyId;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    private struct PropVariant
    {
        [FieldOffset(0)] public ushort VariantType;
        [FieldOffset(8)] public nint PointerValue;
    }
}
