using System.Windows;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using HDRPilot.Core;
using HDRPilot.Infrastructure;
using HDRPilot.App.Services;
using HDRPilot.App.ViewModels;

namespace HDRPilot.App;
public partial class MainWindow : Window
{
    private readonly MainViewModel vm;
    private readonly AutomationRuntime runtime;
    private readonly ISettingsService settings;
    private readonly ActivityLog log;
    private HotkeyService? hotkeys;
    private HwndSource? source;
    public bool AllowClose { get; set; }
    public MainWindow(MainViewModel vm, AutomationRuntime runtime, ISettingsService settings, ActivityLog log)
    {
        InitializeComponent(); DataContext = this.vm = vm; this.runtime = runtime; this.settings = settings; this.log = log;
        SourceInitialized += OnSourceInitialized;
        Closing += (_, e) => { if (!AllowClose) { e.Cancel = true; Hide(); } };
        StateChanged += (_, _) => { if (WindowState == WindowState.Minimized) Hide(); };
        Closed += (_, _) => { vm.SettingsApplied -= RegisterKeys; hotkeys?.Dispose(); source?.RemoveHook(Messages); };
    }
    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle; source = HwndSource.FromHwnd(handle); source?.AddHook(Messages);
        hotkeys = new(handle, vm, log); RegisterKeys(); vm.SettingsApplied += RegisterKeys;
        int dark = 1; DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
    }
    private void RegisterKeys() => hotkeys?.Register(settings.Current);
    private nint Messages(nint hwnd, int message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == 0x7E || message == 0x219) runtime.Wake();
        if (message == 0x218 && (wParam == 4 || wParam == 7 || wParam == 18)) runtime.Invalidate();
        return hotkeys?.Handle(hwnd, message, wParam, lParam, ref handled) ?? 0;
    }
    public void Open(bool settingsPage = false) { Show(); WindowState = WindowState.Normal; if (settingsPage) Navigation.SelectedIndex = 7; Activate(); }
    private void GamesLoaded(object sender, RoutedEventArgs e) => vm.OpenGames();
    private async void ExitClicked(object sender, RoutedEventArgs e) => await ((App)Application.Current).ExitAsync();
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(nint hwnd, uint attribute, ref int value, int size);
}
