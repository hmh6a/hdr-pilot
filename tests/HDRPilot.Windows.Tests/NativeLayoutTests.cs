using System.Runtime.InteropServices;
using HDRPilot.Windows;

namespace HDRPilot.Windows.Tests;
public sealed class NativeLayoutTests
{
    [Fact] public void Native_layout_matches_Windows_SDK_ABI()
    {
        Assert.Equal(20, Marshal.SizeOf<DisplayNative.Header>()); Assert.Equal(72, Marshal.SizeOf<DisplayNative.PathInfo>());
        Assert.Equal(64, Marshal.SizeOf<DisplayNative.ModeInfo>()); Assert.Equal(36, Marshal.SizeOf<DisplayNative.ColorInfo2>());
        Assert.Equal(32, Marshal.SizeOf<DisplayNative.ColorInfo>()); Assert.Equal(24, Marshal.SizeOf<DisplayNative.SetColor>());
        Assert.Equal(420, Marshal.SizeOf<DisplayNative.TargetName>()); Assert.Equal(84, Marshal.SizeOf<DisplayNative.SourceName>());
        Assert.Equal(20, Marshal.OffsetOf<DisplayNative.ColorInfo2>(nameof(DisplayNative.ColorInfo2.Flags)).ToInt32());
    }
    [Fact] public void Header_contains_correct_size_type_and_identity()
    {
        var h = DisplayNative.MakeHeader<DisplayNative.SetColor>(16, new() { Low = 42, High = 7 }, 17);
        Assert.Equal(24u, h.Size); Assert.Equal(16u, h.Type); Assert.Equal(17u, h.Id); Assert.Equal(42u, h.Adapter.Low);
    }
}
