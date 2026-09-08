using System.Runtime.InteropServices;

namespace SnapZones.Windows.Native;

[StructLayout(LayoutKind.Sequential)]
internal struct RectNative
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct MonitorInfoEx
{
    public uint Size;
    public RectNative Monitor;
    public RectNative Work;
    public uint Flags;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string DeviceName;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct DisplayDevice
{
    public int Size;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string DeviceName;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string DeviceString;

    public uint StateFlags;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string DeviceId;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string DeviceKey;
}

[StructLayout(LayoutKind.Sequential)]
internal struct PointNative
{
    public int X;
    public int Y;
}

[StructLayout(LayoutKind.Sequential)]
internal struct WindowPlacementNative
{
    public uint Length;
    public uint Flags;
    public uint ShowCommand;
    public PointNative MinPosition;
    public PointNative MaxPosition;
    public RectNative NormalPosition;
}

[StructLayout(LayoutKind.Sequential)]
internal struct LuidNative
{
    public uint LowPart;
    public int HighPart;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigPathSourceInfo
{
    public LuidNative AdapterId;
    public uint Id;
    public uint ModeInfoIndex;
    public uint StatusFlags;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigRational
{
    public uint Numerator;
    public uint Denominator;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigPathTargetInfo
{
    public LuidNative AdapterId;
    public uint Id;
    public uint ModeInfoIndex;
    public uint OutputTechnology;
    public uint Rotation;
    public uint Scaling;
    public DisplayConfigRational RefreshRate;
    public uint ScanLineOrdering;

    [MarshalAs(UnmanagedType.Bool)]
    public bool TargetAvailable;

    public uint StatusFlags;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigPathInfo
{
    public DisplayConfigPathSourceInfo SourceInfo;
    public DisplayConfigPathTargetInfo TargetInfo;
    public uint Flags;
}

[StructLayout(LayoutKind.Explicit, Size = 48)]
internal struct DisplayConfigModeInfoUnion
{
}

[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigModeInfo
{
    public uint InfoType;
    public uint Id;
    public LuidNative AdapterId;
    public DisplayConfigModeInfoUnion ModeInfo;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigDeviceInfoHeader
{
    public uint Type;
    public uint Size;
    public LuidNative AdapterId;
    public uint Id;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct DisplayConfigSourceDeviceName
{
    public DisplayConfigDeviceInfoHeader Header;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string ViewGdiDeviceName;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct DisplayConfigTargetDeviceName
{
    public DisplayConfigDeviceInfoHeader Header;
    public uint Flags;
    public uint OutputTechnology;
    public ushort EdidManufactureId;
    public ushort EdidProductCodeId;
    public uint ConnectorInstance;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
    public string MonitorFriendlyDeviceName;

    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
    public string MonitorDevicePath;
}

[StructLayout(LayoutKind.Sequential)]
internal struct SidAndAttributesNative
{
    public nint Sid;
    public uint Attributes;
}

[StructLayout(LayoutKind.Sequential)]
internal struct TokenMandatoryLabelNative
{
    public SidAndAttributesNative Label;
}

/// <summary>SP_DEVINFO_DATA: ein Geraet in einer SetupAPI-Geraeteliste.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct SpDevInfoData
{
    public uint Size;
    public Guid ClassGuid;
    public uint DevInst;
    public nint Reserved;
}

/// <summary>DEVMODEW, beschraenkt auf die Anzeigefelder; 220 Bytes wie in der Windows-Kopfdatei.</summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct DevModeNative
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string DeviceName;
    public ushort SpecVersion;
    public ushort DriverVersion;
    public ushort Size;
    public ushort DriverExtra;
    public uint Fields;
    public int PositionX;
    public int PositionY;
    public uint DisplayOrientation;
    public uint DisplayFixedOutput;
    public short Color;
    public short Duplex;
    public short YResolution;
    public short TtOption;
    public short Collate;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string FormName;
    public ushort LogPixels;
    public uint BitsPerPel;
    public uint PelsWidth;
    public uint PelsHeight;
    public uint DisplayFlags;
    public uint DisplayFrequency;
    public uint IcmMethod;
    public uint IcmIntent;
    public uint MediaType;
    public uint DitherType;
    public uint Reserved1;
    public uint Reserved2;
    public uint PanningWidth;
    public uint PanningHeight;
}

/// <summary>Antwort auf DISPLAYCONFIG_DEVICE_INFO_GET_DPI_SCALE: Stufen relativ zur empfohlenen.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigGetDpiScaleNative
{
    public DisplayConfigDeviceInfoHeader Header;
    public int MinimumScaleRelative;
    public int CurrentScaleRelative;
    public int MaximumScaleRelative;
}

/// <summary>Anfrage DISPLAYCONFIG_DEVICE_INFO_SET_DPI_SCALE: die gewuenschte Stufe relativ zur empfohlenen.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct DisplayConfigSetDpiScaleNative
{
    public DisplayConfigDeviceInfoHeader Header;
    public int ScaleRelative;
}

/// <summary>MSLLHOOKSTRUCT: die Zeigerposition einer Mausnachricht im Hook auf unterster Ebene, unbeschnitten.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct MouseLowLevelHookStruct
{
    public PointNative Point;
    public uint MouseData;
    public uint Flags;
    public uint Time;
    public nint ExtraInfo;
}
