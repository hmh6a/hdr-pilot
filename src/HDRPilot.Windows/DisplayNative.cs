using System.Runtime.InteropServices;

namespace HDRPilot.Windows;

// ABI definitions validated against Microsoft's win32metadata/WinSDK wingdi.h.
// Win32 BOOL and UINT32 bit fields are 4 bytes on both x86 and x64.
public static class DisplayNative
{
    [StructLayout(LayoutKind.Sequential)] public struct Luid { public uint Low; public int High; }
    [StructLayout(LayoutKind.Sequential)] public struct Header { public uint Type, Size; public Luid Adapter; public uint Id; }
    [StructLayout(LayoutKind.Sequential)] public struct Rational { public uint Numerator, Denominator; }
    [StructLayout(LayoutKind.Sequential)] public struct Source { public Luid Adapter; public uint Id, ModeIndex, Status; }
    [StructLayout(LayoutKind.Sequential)] public struct Target
    {
        public Luid Adapter; public uint Id, ModeIndex, Technology, Rotation, Scaling;
        public Rational Refresh; public uint Scanline; public int Available; public uint Status;
    }
    [StructLayout(LayoutKind.Sequential)] public struct PathInfo { public Source Source; public Target Target; public uint Flags; }
    [StructLayout(LayoutKind.Explicit, Size = 64)] public struct ModeInfo
    {
        [FieldOffset(0)] public uint Type;
        [FieldOffset(4)] public uint Id;
        [FieldOffset(8)] public Luid Adapter;
        [FieldOffset(16)] public uint Width;
        [FieldOffset(20)] public uint Height;
        [FieldOffset(24)] public uint PixelFormat;
        [FieldOffset(28)] public int X;
        [FieldOffset(32)] public int Y;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] public struct TargetName
    {
        public Header Header; public uint Flags, Technology; public ushort Manufacturer, Product; public uint Connector;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string FriendlyName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string DevicePath;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] public struct SourceName
    {
        public Header Header;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] public struct AdapterName
    {
        public Header Header;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Name;
    }
    [StructLayout(LayoutKind.Sequential)] public struct ColorInfo { public Header Header; public uint Flags, Encoding, Bits; }
    [StructLayout(LayoutKind.Sequential)] public struct ColorInfo2 { public Header Header; public uint Flags, Encoding, Bits, ActiveMode; }
    [StructLayout(LayoutKind.Sequential)] public struct SetColor { public Header Header; public uint Enabled; }
    [DllImport("user32.dll")] public static extern int GetDisplayConfigBufferSizes(uint flags, out uint paths, out uint modes);
    [DllImport("user32.dll")] public static extern int QueryDisplayConfig(uint flags, ref uint paths, [Out] PathInfo[] pathArray, ref uint modes, [Out] ModeInfo[] modeArray, nint topology);
    [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")] public static extern int GetTargetName(ref TargetName value);
    [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")] public static extern int GetSourceName(ref SourceName value);
    [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")] public static extern int GetAdapterName(ref AdapterName value);
    [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")] public static extern int GetColor(ref ColorInfo value);
    [DllImport("user32.dll", EntryPoint = "DisplayConfigGetDeviceInfo")] public static extern int GetColor2(ref ColorInfo2 value);
    [DllImport("user32.dll", EntryPoint = "DisplayConfigSetDeviceInfo")] public static extern int SetColorState(ref SetColor value);
    public static Header MakeHeader<T>(uint type, Luid adapter, uint id) where T : struct => new() { Type = type, Size = (uint)Marshal.SizeOf<T>(), Adapter = adapter, Id = id };
}
