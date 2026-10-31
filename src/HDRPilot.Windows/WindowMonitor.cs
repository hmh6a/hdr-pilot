using System.Runtime.InteropServices;
using HDRPilot.Core;

namespace HDRPilot.Windows;

public sealed class WindowMonitor : IWindowMonitor
{
    private delegate void WinEventDelegate(nint hook, uint eventType, nint window, int objectId, int childId, uint thread, uint time);
    private delegate bool EnumDelegate(nint window, nint parameter);
    private readonly WinEventDelegate callback;
    private readonly nint foregroundHook;
    private readonly nint locationHook;
    private long lastEvent;
    public event EventHandler? Changed;
    public WindowMonitor()
    {
        // Construct/dispose on the WPF dispatcher, which supplies the required message loop.
        callback = (hook, eventType, window, objectId, childId, thread, time) =>
        {
            // Ignore child-control/caret/location noise. Only the foreground top-level
            // window can change targeting; the periodic snapshot covers background windows.
            if (eventType == 0x800B && (objectId != 0 || childId != 0 || window != GetForegroundWindow())) return;
            var now = Environment.TickCount64;
            if (now - Interlocked.Read(ref lastEvent) < 250) return;
            Interlocked.Exchange(ref lastEvent, now); Changed?.Invoke(this, EventArgs.Empty);
        };
        foregroundHook = SetWinEventHook(3, 3, 0, callback, 0, 0, 0);
        locationHook = SetWinEventHook(0x800B, 0x800B, 0, callback, 0, 0, 0);
    }
    public IReadOnlyList<WindowInfo> Snapshot()
    {
        var foreground = GetForegroundWindow(); var result = new List<WindowInfo>();
        EnumWindows((window, _) =>
        {
            if (!IsWindowVisible(window)) return true;
            GetWindowThreadProcessId(window, out var pid);
            var monitor = MonitorFromWindow(window, 2);
            var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
            if (!GetMonitorInfo(monitor, ref info)) return true;
            if (DwmGetWindowAttribute(window, 14, out int cloaked, sizeof(int)) == 0 && cloaked != 0) return true;
            if (DwmGetWindowAttributeRect(window, 9, out var rect, Marshal.SizeOf<Rect>()) != 0 && !GetWindowRect(window, out rect)) return true;
            var minimized = IsIconic(window);
            var full = !minimized && Math.Abs(rect.Left - info.Monitor.Left) <= 2 && Math.Abs(rect.Top - info.Monitor.Top) <= 2 && Math.Abs(rect.Right - info.Monitor.Right) <= 2 && Math.Abs(rect.Bottom - info.Monitor.Bottom) <= 2;
            result.Add(new((int)pid, info.Device, window == foreground, full, minimized)); return true;
        }, 0);
        return result;
    }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct MonitorInfo
    { public int Size; public Rect Monitor, Work; public uint Flags; [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device; }
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumDelegate callback, nint parameter);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] private static extern bool IsIconic(nint window);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint pid);
    [DllImport("user32.dll")] private static extern nint MonitorFromWindow(nint window, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(nint monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint window, out Rect rect);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint window, uint attribute, out int value, int size);
    [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")] private static extern int DwmGetWindowAttributeRect(nint window, uint attribute, out Rect value, int size);
    [DllImport("user32.dll")] private static extern nint SetWinEventHook(uint min, uint max, nint module, WinEventDelegate callback, uint process, uint thread, uint flags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(nint hook);
    public void Dispose() { if (foregroundHook != 0) UnhookWinEvent(foregroundHook); if (locationHook != 0) UnhookWinEvent(locationHook); }
}
