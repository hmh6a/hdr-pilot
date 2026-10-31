namespace HDRPilot.Core;

public interface IDisplayDiscoveryService { Task<IReadOnlyList<DisplayInfo>> GetDisplaysAsync(CancellationToken ct = default); }
public interface IHdrController
{
    // expectedState is a precondition, protecting against an observed external change before a write.
    Task<HdrOperationResult> SetHdrAsync(string displayId, bool enabled, bool? expectedState = null, CancellationToken ct = default);
}
public interface IProcessMonitor : IDisposable { IReadOnlyList<ProcessInfo> Snapshot(); }
public interface IWindowMonitor : IDisposable { event EventHandler? Changed; IReadOnlyList<WindowInfo> Snapshot(); }
public interface IGameDiscoveryService { Task<IReadOnlyList<DiscoveredGame>> DiscoverAsync(CancellationToken ct = default); }
public sealed record DiscoveredGame(string Name, string InstallDirectory, string Source, IReadOnlyList<string> Executables);
public interface IProfileRepository { IReadOnlyList<GameProfile> GetProfiles(); Task SaveProfileAsync(GameProfile profile, CancellationToken ct = default); Task DeleteProfileAsync(Guid id, CancellationToken ct = default); Task RecordPlayedAsync(Guid id, DateTimeOffset when, CancellationToken ct = default); }
public interface IRulesEngine { IReadOnlyList<RuleDecision> Evaluate(RuleContext context); }
public interface IVideoDetectionService { IReadOnlyList<string> KnownPlayerNames { get; } bool IsPlayer(string processName); }
public interface IBrowserBridge : IAsyncDisposable { IReadOnlyList<VideoSignal> Snapshot(); Task RunAsync(CancellationToken ct); }
public interface ISettingsService
{
    AppSettings Current { get; }
    string? RecoveryMessage { get; }
    Task LoadAsync(CancellationToken ct = default);
    Task SaveAsync(AppSettings settings, CancellationToken ct = default);
    Task UpdateAsync(Action<AppSettings> update, CancellationToken ct = default);
    Task ImportAsync(string path, CancellationToken ct = default);
    Task ExportAsync(string path, CancellationToken ct = default);
}
public interface INotificationService { void Notify(string title, string message); }
public interface IStartupManager { bool IsEnabled { get; } void SetEnabled(bool enabled); }
