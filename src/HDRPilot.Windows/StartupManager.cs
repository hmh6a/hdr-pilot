using Microsoft.Win32;
using HDRPilot.Core;

namespace HDRPilot.Windows;

public sealed class StartupManager : IStartupManager
{
    private const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public bool IsEnabled { get { using var key = Registry.CurrentUser.OpenSubKey(Key); return key?.GetValue("HDRPilot") is string; } }
    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(Key);
        if (enabled) key.SetValue("HDRPilot", $"\"{Environment.ProcessPath}\" --minimized", RegistryValueKind.String);
        else key.DeleteValue("HDRPilot", false);
    }
}
