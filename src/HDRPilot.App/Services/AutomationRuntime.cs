using System.Diagnostics;
using System.IO;
using System.Threading.Channels;
using HDRPilot.Core;
using HDRPilot.Engine;
using HDRPilot.Infrastructure;

namespace HDRPilot.App.Services;

public sealed record RuntimeSnapshot(IReadOnlyList<DisplayInfo> Displays, IReadOnlyList<ProcessInfo> Processes, string ActiveApplication, string State, string Reason, int BrowserSessions, double CpuPercent, double MemoryMb)
{
    public IReadOnlyList<ProcessInfo> VisibleApplications { get; init; } = [];
}
public sealed class AutomationRuntime(IDisplayDiscoveryService discovery, IProcessMonitor processes, IWindowMonitor windows,
    ISettingsService settings, IProfileRepository profiles, AutomationEngine engine, IBrowserBridge browser, ActivityLog log) : IAsyncDisposable
{
    private readonly CancellationTokenSource cancellation = new();
    private readonly Channel<bool> signals = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });
    private Task? loop, timer, bridge;
    private int invalidated, disposed;
    public event Action<RuntimeSnapshot>? Updated;
    public RuntimeSnapshot Latest { get; private set; } = new([], [], "No foreground application", "Starting", "Waiting for displays", 0, 0, 0);
    public void Start()
    {
        windows.Changed += OnWindowChanged;
        loop = Task.Run(() => RunAsync(cancellation.Token));
        timer = Task.Run(async () =>
        {
            using var pulse = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (await pulse.WaitForNextTickAsync(cancellation.Token)) signals.Writer.TryWrite(true);
        });
        bridge = Task.Run(() => browser.RunAsync(cancellation.Token));
        Wake();
    }
    private void OnWindowChanged(object? sender, EventArgs e) => Wake();
    public void Wake() => signals.Writer.TryWrite(true);
    public void Invalidate() { Interlocked.Exchange(ref invalidated, 1); Wake(); }
    private async Task RunAsync(CancellationToken ct)
    {
        IReadOnlyList<ProcessInfo> processList = []; var lastProcesses = DateTimeOffset.MinValue;
        using var self = Process.GetCurrentProcess(); var priorCpu = self.TotalProcessorTime; var sample = Stopwatch.StartNew(); double cpu = 0, memory = 0;
        var seen = new HashSet<int>(); var lastError = "";
        await foreach (var pulse in signals.Reader.ReadAllAsync(ct))
        {
            await Task.Delay(200, ct); while (signals.Reader.TryRead(out _)) { }
            try
            {
                if (Interlocked.Exchange(ref invalidated, 0) != 0) await engine.InvalidateAsync();
                var now = DateTimeOffset.UtcNow;
                if ((now - lastProcesses).TotalSeconds >= 2) { processList = processes.Snapshot(); lastProcesses = now; }
                var displayList = await discovery.GetDisplaysAsync(ct);
                var windowList = windows.Snapshot(); var config = settings.Current; var videos = browser.Snapshot();
                foreach (var profile in config.Profiles.Where(p => p.Enabled))
                    foreach (var process in processList.Where(p => string.Equals(p.ExecutablePath, profile.ExecutablePath, StringComparison.OrdinalIgnoreCase)))
                        if (seen.Add(process.Id))
                        {
                            log.Add($"Application detected: {profile.Name}");
                            try { await profiles.RecordPlayedAsync(profile.Id, now, ct); }
                            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException) { log.Add("Could not persist recently played timestamp.", "Warning"); }
                        }
                seen.IntersectWith(processList.Select(p => p.Id));
                await engine.TickAsync(new(displayList, processList, windowList, config.Profiles, config, videos), now, ct);
                if (sample.Elapsed.TotalSeconds >= 10)
                {
                    self.Refresh(); var currentCpu = self.TotalProcessorTime;
                    cpu = (currentCpu - priorCpu).TotalSeconds / sample.Elapsed.TotalSeconds / Environment.ProcessorCount * 100;
                    memory = self.WorkingSet64 / 1048576d; priorCpu = currentCpu; sample.Restart();
                }
                var activeId = windowList.FirstOrDefault(w => w.IsForeground)?.ProcessId;
                var active = processList.FirstOrDefault(p => p.Id == activeId)?.Name ?? "No foreground application";
                var visibleIds = windowList.Select(w => w.ProcessId).ToHashSet();
                Latest = new(displayList, processList, active, engine.State.ToString(), engine.Reason, videos.Count, cpu, memory)
                { VisibleApplications = processList.Where(p => visibleIds.Contains(p.Id) && p.Id != Environment.ProcessId && p.ExecutablePath is not null).ToArray() };
                Updated?.Invoke(Latest); lastError = "";
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                await engine.InvalidateAsync();
                if (lastError != ex.Message) { log.Add($"Monitoring unavailable: {ex.Message}", "Error"); lastError = ex.Message; }
                Latest = Latest with { State = "Error", Reason = "Monitoring unavailable. Current display state is preserved." }; Updated?.Invoke(Latest);
                await Task.Delay(2000, ct);
            }
        }
    }
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        windows.Changed -= OnWindowChanged; cancellation.Cancel(); signals.Writer.TryComplete();
        foreach (var task in new[] { loop, timer, bridge })
            if (task is not null) { try { await task; } catch (OperationCanceledException) { } catch (Exception ex) { log.Add($"Background task ended: {ex.GetType().Name}", "Error"); } }
        try { await engine.RestoreAsync(await discovery.GetDisplaysAsync()); }
        catch (Exception ex) { log.Add($"Exit restoration unavailable: {ex.Message}", "Error"); }
        cancellation.Dispose();
    }
}
