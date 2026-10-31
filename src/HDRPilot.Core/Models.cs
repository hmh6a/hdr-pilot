namespace HDRPilot.Core;

public enum HdrMode { Automatic, ForceHdr, ForceSdr, NeverModify, FullscreenOnly, WhileRunning, WhileForeground }
public enum AutomationMode { Automatic, AlwaysHdr, AlwaysSdr, ProfilesOnly, Suspended }
public enum EngineState { Idle, Evaluating, PendingActivation, Activating, HDRActive, PendingDeactivation, Deactivating, Suspended, Error }
public enum ContentRange { Unknown, Sdr, Hdr }

public sealed record DisplayInfo(string Id, string Name, string DeviceName, int Width, int Height,
    double RefreshRate, bool IsPrimary, bool HdrSupported, bool HdrEnabled, bool CanSetHdr,
    string Adapter, string? Limitation = null)
{
    public string Summary => $"{Width} × {Height} · {RefreshRate:0.##} Hz · {(IsPrimary ? "Primary · " : "")}{(HdrSupported ? "HDR capable" : "SDR display")}";
    public string Status => HdrEnabled ? "HDR ON" : "SDR";
    public string Label => $"{Name} ({DeviceName})";
}
public sealed record HdrOperationResult(bool Success, string DisplayId, bool RequestedState, bool? ActualState, string? ErrorMessage);
public sealed class GameProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string ExecutablePath { get; set; } = "";
    public HdrMode Mode { get; set; } = HdrMode.WhileRunning;
    public string? PreferredDisplayId { get; set; }
    public int ActivationDelayMs { get; set; } = 1500;
    public int DeactivationDelayMs { get; set; } = 3000;
    public bool Enabled { get; set; } = true;
    public int Priority { get; set; } = 100;
    public bool IgnoreWhenMinimized { get; set; } = true;
    public string Source { get; set; } = "Manual";
    public DateTimeOffset? LastPlayed { get; set; }
}
public sealed record ProcessInfo(int Id, string Name, string? ExecutablePath);
public sealed record WindowInfo(int ProcessId, string DeviceName, bool IsForeground, bool IsFullscreen, bool IsMinimized);
public sealed record ActivityEntry(DateTimeOffset Time, string Level, string Message)
{
    public string Text => $"{Time:HH:mm:ss}  {Level,-7}  {Message}";
}
public sealed record VideoSignal(string SessionId, bool Playing, bool Fullscreen, ContentRange Range, DateTimeOffset ReceivedAt, string BrowserProcessName = "chrome");
public sealed record RuleDecision(string DisplayId, bool? DesiredHdr, string Reason, int ActivationDelayMs = 0, int DeactivationDelayMs = 3000, bool PreserveCurrent = false);
public sealed record RuleContext(IReadOnlyList<DisplayInfo> Displays, IReadOnlyList<ProcessInfo> Processes,
    IReadOnlyList<WindowInfo> Windows, IReadOnlyList<GameProfile> Profiles, AppSettings Settings, IReadOnlyList<VideoSignal> Videos);
public sealed class HotkeySettings
{
    public bool Enabled { get; set; } = true;
    public uint Modifiers { get; set; } = 9; // Alt + Windows
    public uint Key { get; set; } = 0x48;
}
public sealed class AppSettings
{
    public int SchemaVersion { get; set; } = 1;
    public AutomationMode Mode { get; set; } = AutomationMode.Automatic;
    public int CooldownMs { get; set; } = 5000;
    public bool StartMinimized { get; set; }
    public bool Notifications { get; set; }
    public bool BrowserEnabled { get; set; }
    public bool RequestHdrForSdrVideo { get; set; }
    public bool BrowserFullscreenOnly { get; set; } = true;
    public List<GameProfile> Profiles { get; set; } = [];
    public Dictionary<string, HotkeySettings> Hotkeys { get; set; } = new()
    {
        ["Toggle"] = new(), ["Automatic"] = new() { Key = 0x41 },
        ["Pause"] = new() { Key = 0x50 }, ["Cycle"] = new() { Key = 0x4F }
    };
}
