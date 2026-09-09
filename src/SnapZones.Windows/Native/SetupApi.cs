using System.Runtime.InteropServices;
using System.Text;

namespace SnapZones.Windows.Native;

/// <summary>
/// SetupAPI fuer den Geraeteknoten des virtuellen Vollbildzonen-Treibers. Ersetzt devcon: Knoten anlegen,
/// Hardware-Kennung setzen, registrieren, aufzaehlen, entfernen.
/// </summary>
internal static class SetupApi
{
    internal const uint DigcfPresent = 0x00000002;
    internal const uint DicdGenerateId = 0x00000001;
    internal const uint DifRemove = 0x00000005;
    internal const uint DifRegisterDevice = 0x00000019;
    internal const uint SpdrpDeviceDesc = 0x00000000;
    internal const uint SpdrpHardwareId = 0x00000001;
    internal static readonly nint InvalidHandleValue = -1;

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern nint SetupDiGetClassDevsW(ref Guid classGuid, string? enumerator, nint parent, uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetupDiEnumDeviceInfo(nint deviceInfoSet, uint index, ref SpDevInfoData data);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetupDiGetDeviceRegistryPropertyW(
        nint deviceInfoSet,
        ref SpDevInfoData data,
        uint property,
        out uint registryType,
        byte[]? buffer,
        uint bufferSize,
        out uint requiredSize);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetupDiGetDeviceInstanceIdW(
        nint deviceInfoSet,
        ref SpDevInfoData data,
        StringBuilder? instanceId,
        uint bufferSize,
        out uint requiredSize);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern nint SetupDiCreateDeviceInfoList(ref Guid classGuid, nint parent);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetupDiCreateDeviceInfoW(
        nint deviceInfoSet,
        string deviceName,
        ref Guid classGuid,
        string? description,
        nint parent,
        uint creationFlags,
        ref SpDevInfoData data);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetupDiSetDeviceRegistryPropertyW(
        nint deviceInfoSet,
        ref SpDevInfoData data,
        uint property,
        byte[] buffer,
        uint bufferSize);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetupDiCallClassInstaller(uint installFunction, nint deviceInfoSet, ref SpDevInfoData data);

    [DllImport("setupapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetupDiDestroyDeviceInfoList(nint deviceInfoSet);
}

internal static class NewDev
{
    internal const uint InstallFlagForce = 0x00000001;

    [DllImport("newdev.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UpdateDriverForPlugAndPlayDevicesW(
        nint parent,
        string hardwareId,
        string infPath,
        uint flags,
        [MarshalAs(UnmanagedType.Bool)] out bool rebootRequired);
}
