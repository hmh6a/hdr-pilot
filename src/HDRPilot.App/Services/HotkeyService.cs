using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using HDRPilot.Core;
using HDRPilot.Infrastructure;
using HDRPilot.App.ViewModels;

namespace HDRPilot.App.Services;
public sealed class HotkeyService(nint window, MainViewModel vm, ActivityLog log) : IDisposable
{
    private readonly Dictionary<int, ICommand> commands = [];
    public void Register(AppSettings settings)
    {
        Dispose(); var id = 1;
        foreach (var pair in settings.Hotkeys)
        {
            if (!pair.Value.Enabled) continue;
            var command = pair.Key switch { "Toggle" => vm.ToggleCommand, "Automatic" => vm.AutomaticCommand, "Pause" => vm.PauseCommand, "Cycle" => vm.CycleCommand, _ => null };
            if (command is null) continue;
            if (RegisterHotKey(window, id, pair.Value.Modifiers | 0x4000, pair.Value.Key)) commands[id] = command;
            else log.Add($"Global shortcut '{pair.Key}' could not be registered (conflict or reserved combination; error {Marshal.GetLastWin32Error()}).", "Warning");
            id++;
        }
    }
    public nint Handle(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    { if (message == 0x312 && commands.TryGetValue((int)wParam, out var command)) { if (command.CanExecute(null)) command.Execute(null); handled = true; } return 0; }
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(nint hwnd, int id);
    public void Dispose() { foreach (var id in commands.Keys) UnregisterHotKey(window, id); commands.Clear(); }
}
