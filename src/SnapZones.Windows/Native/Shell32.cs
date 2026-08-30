using System.Runtime.InteropServices;

namespace SnapZones.Windows.Native;

internal static class Shell32
{
    private const ushort StringPointerVariant = 31;
    private static readonly Guid PropertyStoreInterfaceId = new("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99");
    private static readonly PropertyKey AppUserModelIdKey = new(
        new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
        5);

    internal static string? TryReadAppUserModelId(nint window)
    {
        IPropertyStore? propertyStore = null;
        var value = default(PropVariant);
        try
        {
            var interfaceId = PropertyStoreInterfaceId;
            if (SHGetPropertyStoreForWindow(window, ref interfaceId, out propertyStore) < 0 || propertyStore is null)
            {
                return null;
            }

            var key = AppUserModelIdKey;
            if (propertyStore.GetValue(ref key, out value) < 0 ||
                value.VariantType != StringPointerVariant ||
                value.PointerValue == 0)
            {
                return null;
            }

            var applicationId = Marshal.PtrToStringUni(value.PointerValue);
            return string.IsNullOrWhiteSpace(applicationId) ? null : applicationId;
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            try
            {
                _ = PropVariantClear(ref value);
            }
            catch (Exception)
            {
            }

            try
            {
                if (propertyStore is not null && Marshal.IsComObject(propertyStore))
                {
                    _ = Marshal.ReleaseComObject(propertyStore);
                }
            }
            catch (Exception)
            {
            }
        }
    }

    [DllImport("shell32.dll", PreserveSig = true)]
    private static extern int SHGetPropertyStoreForWindow(
        nint window,
        ref Guid interfaceId,
        [MarshalAs(UnmanagedType.Interface)] out IPropertyStore? propertyStore);

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropVariant value);

    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig]
        int GetCount(out uint propertyCount);

        [PreserveSig]
        int GetAt(uint propertyIndex, out PropertyKey key);

        [PreserveSig]
        int GetValue(ref PropertyKey key, out PropVariant value);

        [PreserveSig]
        int SetValue(ref PropertyKey key, ref PropVariant value);

        [PreserveSig]
        int Commit();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey
    {
        internal PropertyKey(Guid formatId, uint propertyId)
        {
            FormatId = formatId;
            PropertyId = propertyId;
        }

        internal Guid FormatId;
        internal uint PropertyId;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    private struct PropVariant
    {
        [FieldOffset(0)]
        internal ushort VariantType;

        [FieldOffset(8)]
        internal nint PointerValue;
    }
}
