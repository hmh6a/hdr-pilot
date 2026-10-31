using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using HDRPilot.Core;

namespace HDRPilot.Windows;

// Bounded 2-second snapshots are scheduled by the coordinator. No WMI service dependency.
public sealed class ProcessMonitor : IProcessMonitor
{
    public IReadOnlyList<ProcessInfo> Snapshot()
    {
        var result = new List<ProcessInfo>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    string? path = null;
                    var handle = OpenProcess(0x1000, false, process.Id); // QUERY_LIMITED_INFORMATION
                    if (handle != 0)
                    {
                        try { var buffer = new StringBuilder(1024); uint size = 1024; if (QueryFullProcessImageName(handle, 0, buffer, ref size)) path = buffer.ToString(); }
                        finally { CloseHandle(handle); }
                    }
                    result.Add(new(process.Id, process.ProcessName, path));
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException) { }
            }
        }
        return result;
    }
    [DllImport("kernel32.dll")] private static extern nint OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, int id);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool QueryFullProcessImageName(nint process, uint flags, StringBuilder name, ref uint size);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(nint handle);
    public void Dispose() { }
}
