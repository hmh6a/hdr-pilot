using System.Windows;
using HDRPilot.Core;
using HDRPilot.Windows;
using HDRPilot.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using HDRPilot.App.Services;
using HDRPilot.App.ViewModels;
using HDRPilot.Engine;
using HDRPilot.GameDetection;
using HDRPilot.VideoDetection;
using System.Windows.Threading;

namespace HDRPilot.App;

public partial class App : Application
{
    private ServiceProvider? services;
    private Mutex? instance;
    private EventWaitHandle? activation;
    private DispatcherTimer? activationTimer;
    private TrayService? tray;
    private AutomationRuntime? automationRuntime;
    private bool exiting;
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var identity = System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value;
        instance = new Mutex(true, "Local\\HDRPilot." + identity, out var first);
        activation = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\HDRPilot.Activate." + identity);
        if (!first) { activation.Set(); Shutdown(); return; }
        try
        {
        var collection = new ServiceCollection();
        collection.AddSingleton<ActivityLog>();
        collection.AddLogging(b => b.SetMinimumLevel(LogLevel.Information));
        collection.AddSingleton<ILoggerProvider>(sp => sp.GetRequiredService<ActivityLog>());
        collection.AddSingleton<JsonSettingsService>();
        collection.AddSingleton<ISettingsService>(sp => sp.GetRequiredService<JsonSettingsService>());
        collection.AddSingleton<IProfileRepository>(sp => sp.GetRequiredService<JsonSettingsService>());
        collection.AddSingleton<WindowsDisplayService>();
        collection.AddSingleton<IDisplayDiscoveryService>(sp => sp.GetRequiredService<WindowsDisplayService>());
        collection.AddSingleton<IHdrController>(sp => sp.GetRequiredService<WindowsDisplayService>());
        collection.AddSingleton<IProcessMonitor, ProcessMonitor>();
        collection.AddSingleton<IWindowMonitor, WindowMonitor>();
        collection.AddSingleton<IStartupManager, StartupManager>();
        collection.AddSingleton<IGameDiscoveryService, GameDiscoveryService>();
        collection.AddSingleton<IVideoDetectionService, VideoDetectionService>();
        collection.AddSingleton<IBrowserBridge>(sp => new BrowserBridge.BrowserBridge(() => sp.GetRequiredService<ISettingsService>().Current.BrowserEnabled));
        collection.AddSingleton<IRulesEngine, RulesEngine>();
        collection.AddSingleton<AutomationEngine>();
        collection.AddSingleton<AutomationRuntime>();
        collection.AddSingleton<MainViewModel>();
        collection.AddSingleton<MainWindow>();
        services = collection.BuildServiceProvider();
        await services.GetRequiredService<ISettingsService>().LoadAsync();
        var window = services.GetRequiredService<MainWindow>(); MainWindow = window;
        var settings = services.GetRequiredService<ISettingsService>(); var vm = services.GetRequiredService<MainViewModel>();
        tray = new(vm, settings, services.GetRequiredService<IStartupManager>(), () => window.Open(), () => window.Open(true), () => _ = ExitAsync());
        window.Show(); if (settings.Current.StartMinimized || e.Args.Contains("--minimized")) window.Hide();
        var runtime = automationRuntime = services.GetRequiredService<AutomationRuntime>();
        runtime.Updated += snapshot => Dispatcher.InvokeAsync(() => tray?.Update(snapshot)); runtime.Start();
        services.GetRequiredService<ActivityLog>().Add("HDR Pilot started. Ownership begins with the current display state.");
        activationTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => { if (activation?.WaitOne(0) == true) window.Open(); }, Dispatcher);
        if (e.Args.Contains("--smoke-test")) { await Task.Delay(TimeSpan.FromSeconds(20)); await ExitAsync(); }
        }
        catch (Exception ex) { MessageBox.Show(ex.ToString(), "HDR Pilot could not start"); await ExitAsync(); }
    }
    public async Task ExitAsync()
    {
        if (exiting) return; exiting = true; activationTimer?.Stop();
        if (automationRuntime is not null) await automationRuntime.DisposeAsync();
        tray?.Dispose(); tray = null;
        if (MainWindow is MainWindow window) { window.AllowClose = true; window.Close(); }
        if (services is not null) await services.DisposeAsync(); services = null; Shutdown();
    }
    protected override void OnExit(ExitEventArgs e) { activation?.Dispose(); instance?.Dispose(); base.OnExit(e); }
}
