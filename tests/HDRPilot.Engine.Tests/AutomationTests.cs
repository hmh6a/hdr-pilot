using HDRPilot.Core;
using HDRPilot.Engine;
using Microsoft.Extensions.Logging.Abstractions;

namespace HDRPilot.Engine.Tests;

public sealed class AutomationTests
{
    private static DisplayInfo Display(string id = "a", bool on = false) => new(id, id, id, 2560, 1440, 240, id == "a", true, on, true, "gpu");
    private static GameProfile Profile(string name = "game", HdrMode mode = HdrMode.WhileRunning, int priority = 100) => new()
    { Name = name, ExecutablePath = $@"C:\Games\{name}.exe", Mode = mode, Priority = priority, ActivationDelayMs = 0, DeactivationDelayMs = 0 };
    private static RuleContext Context(DisplayInfo[] displays, GameProfile[] profiles, bool active = true, string target = "a", AppSettings? settings = null) => new(displays,
        active ? profiles.Select((p, i) => new ProcessInfo(i + 1, p.Name, p.ExecutablePath)).ToArray() : [],
        active ? profiles.Select((p, i) => new WindowInfo(i + 1, target, true, true, false)).ToArray() : [], profiles, settings ?? new(), []);
    private sealed class FakeHdr : IHdrController
    {
        public List<(string Id, bool State)> Calls { get; } = [];
        public bool Fail;
        public TaskCompletionSource? Started, Release;
        public async Task<HdrOperationResult> SetHdrAsync(string id, bool enabled, bool? expectedState = null, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested(); Calls.Add((id, enabled)); Started?.TrySetResult();
            if (Release is not null) await Release.Task;
            return new(!Fail, id, enabled, Fail ? expectedState : enabled, Fail ? "driver reset" : null);
        }
    }
    private static AutomationEngine Engine(FakeHdr hdr) => new(hdr, new RulesEngine(), NullLogger<AutomationEngine>.Instance);
    private static readonly DateTimeOffset T = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Theory] [InlineData(false, 2)] [InlineData(true, 0)]
    public async Task Game_lifecycle_preserves_initial_state(bool initial, int count)
    {
        var hdr = new FakeHdr(); using var engine = Engine(hdr); var p = Profile();
        await engine.TickAsync(Context([Display(on: initial)], [p]), T);
        await engine.TickAsync(Context([Display(on: true)], [p], false), T.AddSeconds(10));
        Assert.Equal(count, hdr.Calls.Count);
        if (!initial) Assert.Equal(new[] { ("a", true), ("a", false) }, hdr.Calls);
    }
    [Fact] public async Task Manual_change_yields_and_never_restores_blindly()
    {
        var hdr = new FakeHdr(); using var engine = Engine(hdr); var p = Profile();
        await engine.TickAsync(Context([Display()], [p]), T);
        await engine.TickAsync(Context([Display()], [p]), T.AddSeconds(10));
        await engine.TickAsync(Context([Display()], [p], false), T.AddSeconds(20));
        Assert.Single(hdr.Calls); Assert.Contains("manual override", engine.Reason);
    }
    [Fact] public async Task Two_games_keep_hdr_until_last_exits()
    {
        var hdr = new FakeHdr(); using var engine = Engine(hdr); var a = Profile("one"); var b = Profile("two");
        await engine.TickAsync(Context([Display()], [a, b]), T);
        await engine.TickAsync(Context([Display(on: true)], [b]), T.AddSeconds(10));
        Assert.Single(hdr.Calls);
        await engine.TickAsync(Context([Display(on: true)], [b], false), T.AddSeconds(20)); Assert.Equal(2, hdr.Calls.Count);
    }
    [Fact] public async Task Moving_game_targets_monitors_independently()
    {
        var hdr = new FakeHdr(); using var engine = Engine(hdr); var p = Profile();
        await engine.TickAsync(Context([Display(), Display("b")], [p]), T);
        await engine.TickAsync(Context([Display(on: true), Display("b")], [p], target: "b"), T.AddSeconds(10));
        Assert.Equal(new[] { ("a", true), ("a", false), ("b", true) }, hdr.Calls);
    }
    [Fact] public async Task Existing_hdr_on_second_monitor_is_preserved()
    {
        var hdr = new FakeHdr(); using var engine = Engine(hdr); var p = Profile();
        await engine.TickAsync(Context([Display(), Display("b", true)], [p]), T);
        await engine.TickAsync(Context([Display(on: true), Display("b", true)], [p], false), T.AddSeconds(10));
        Assert.All(hdr.Calls, call => Assert.Equal("a", call.Id));
    }
    [Fact] public async Task Rapid_foreground_changes_cancel_activation()
    {
        var hdr = new FakeHdr(); using var engine = Engine(hdr); var p = Profile(mode: HdrMode.WhileForeground); p.ActivationDelayMs = 2000;
        await engine.TickAsync(Context([Display()], [p]), T);
        await engine.TickAsync(Context([Display()], [p], false), T.AddMilliseconds(500));
        await engine.TickAsync(Context([Display()], [p]), T.AddSeconds(1));
        await engine.TickAsync(Context([Display()], [p], false), T.AddSeconds(2));
        Assert.Empty(hdr.Calls);
    }
    [Fact] public async Task New_instance_never_trusts_crashed_instance_ownership()
    {
        var hdr = new FakeHdr(); using var engine = Engine(hdr);
        await engine.TickAsync(Context([Display(on: true)], [], false), T); await engine.RestoreAsync([Display(on: true)]);
        Assert.Empty(hdr.Calls);
    }
    [Fact] public async Task Unplug_topology_change_discards_old_lease()
    {
        var hdr = new FakeHdr(); using var engine = Engine(hdr); var p = Profile();
        await engine.TickAsync(Context([Display()], [p]), T);
        await engine.TickAsync(Context([], [p], false), T.AddSeconds(10));
        await engine.TickAsync(Context([Display(on: true)], [p], false), T.AddSeconds(20));
        Assert.Single(hdr.Calls);
    }
    [Fact] public async Task Driver_failure_does_not_retry_until_rearmed()
    {
        var hdr = new FakeHdr { Fail = true }; using var engine = Engine(hdr); var p = Profile();
        await engine.TickAsync(Context([Display()], [p]), T);
        await engine.TickAsync(Context([Display()], [p]), T.AddSeconds(10));
        Assert.Single(hdr.Calls);
        hdr.Fail = false; await engine.RearmAsync([Display()]);
        await engine.TickAsync(Context([Display()], [p]), T.AddSeconds(20)); Assert.Equal(2, hdr.Calls.Count);
    }
    [Fact] public async Task Unsupported_display_never_receives_write()
    {
        var hdr = new FakeHdr(); using var engine = Engine(hdr);
        await engine.TickAsync(Context([Display() with { HdrSupported = false, CanSetHdr = false }], [Profile()]), T); Assert.Empty(hdr.Calls);
    }
    [Fact] public async Task Identical_requests_do_not_toggle_again()
    {
        var hdr = new FakeHdr(); using var engine = Engine(hdr); var p = Profile();
        await engine.TickAsync(Context([Display()], [p]), T);
        for (var i = 1; i < 20; i++) await engine.TickAsync(Context([Display(on: true)], [p]), T.AddSeconds(i));
        Assert.Single(hdr.Calls);
    }
    [Fact] public async Task Shutdown_waits_for_inflight_write_then_restores()
    {
        var hdr = new FakeHdr { Started = new(), Release = new() }; using var engine = Engine(hdr); using var ct = new CancellationTokenSource();
        var task = engine.TickAsync(Context([Display()], [Profile()]), T, ct.Token);
        await hdr.Started.Task; ct.Cancel(); hdr.Release.SetResult(); await task;
        await engine.RestoreAsync([Display(on: true)]);
        Assert.Equal(new[] { ("a", true), ("a", false) }, hdr.Calls);
    }
    [Fact] public async Task Cooldown_prevents_rapid_restoration()
    {
        var hdr = new FakeHdr(); using var engine = Engine(hdr); var p = Profile();
        await engine.TickAsync(Context([Display()], [p]), T);
        await engine.TickAsync(Context([Display(on: true)], [p], false), T.AddSeconds(1)); Assert.Single(hdr.Calls);
        await engine.TickAsync(Context([Display(on: true)], [p], false), T.AddSeconds(6)); Assert.Equal(2, hdr.Calls.Count);
    }
    [Fact] public void Conflict_priority_and_guid_tiebreak_are_deterministic()
    {
        var a = Profile("a", HdrMode.ForceHdr, 10); var b = Profile("b", HdrMode.ForceSdr, 100); var rules = new RulesEngine();
        Assert.False(rules.Evaluate(Context([Display()], [a, b]))[0].DesiredHdr);
        a.Priority = b.Priority; a.Id = Guid.Parse("00000000-0000-0000-0000-000000000001"); b.Id = Guid.Parse("00000000-0000-0000-0000-000000000002");
        Assert.True(rules.Evaluate(Context([Display()], [b, a]))[0].DesiredHdr);
    }
    [Fact] public void Broken_path_does_not_match_same_executable_name()
    {
        var context = Context([Display()], [Profile()]);
        context = context with { Processes = [new(1, "game", @"C:\Elsewhere\game.exe")] };
        Assert.Null(new RulesEngine().Evaluate(context)[0].DesiredHdr);
    }
    [Fact] public async Task Browser_loss_releases_only_owned_hdr()
    {
        var hdr = new FakeHdr(); using var engine = Engine(hdr); var settings = new AppSettings { BrowserEnabled = true };
        var c = new RuleContext([Display()], [new(1, "chrome", @"C:\chrome.exe")], [new(1, "a", true, true, false)], [], settings, [new("s", true, true, ContentRange.Hdr, T)]);
        await engine.TickAsync(c, T); await engine.TickAsync(c, T.AddSeconds(3));
        await engine.TickAsync(c with { Displays = [Display(on: true)], Videos = [] }, T.AddSeconds(10));
        await engine.TickAsync(c with { Displays = [Display(on: true)], Videos = [] }, T.AddSeconds(16));
        Assert.Equal(new[] { ("a", true), ("a", false) }, hdr.Calls);
    }
    [Fact] public void Unknown_video_is_never_assumed_hdr_or_sdr()
    {
        var c = new RuleContext([Display()], [new(1, "chrome", @"C:\chrome.exe")], [new(1, "a", true, true, false)], [], new() { BrowserEnabled = true, RequestHdrForSdrVideo = true }, [new("s", true, true, ContentRange.Unknown, T)]);
        Assert.Null(new RulesEngine().Evaluate(c)[0].DesiredHdr);
    }
    [Fact] public async Task Force_sdr_cannot_disable_preexisting_hdr()
    {
        var hdr = new FakeHdr(); using var engine = Engine(hdr);
        await engine.TickAsync(Context([Display(on: true)], [Profile(mode: HdrMode.ForceSdr)]), T);
        Assert.Empty(hdr.Calls); Assert.Contains("preserving HDR", engine.Reason);
    }
    [Fact] public async Task Never_modify_profile_holds_current_state_until_it_exits()
    {
        var hdr = new FakeHdr(); using var engine = Engine(hdr); var game = Profile(); var keep = Profile("keep", HdrMode.NeverModify, 200);
        await engine.TickAsync(Context([Display()], [game]), T);
        await engine.TickAsync(Context([Display(on: true)], [keep]), T.AddSeconds(10)); Assert.Single(hdr.Calls);
        await engine.TickAsync(Context([Display(on: true)], [], false), T.AddSeconds(20)); Assert.Equal(2, hdr.Calls.Count);
    }
    [Fact] public async Task Cancellation_before_transition_does_not_touch_hardware()
    {
        var hdr = new FakeHdr(); using var engine = Engine(hdr); using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => engine.TickAsync(Context([Display()], [Profile()]), T, cancel.Token)); Assert.Empty(hdr.Calls);
    }
    [Fact] public async Task Wake_or_uncertain_query_forgets_previous_ownership()
    {
        var hdr = new FakeHdr(); using var engine = Engine(hdr);
        await engine.TickAsync(Context([Display()], [Profile()]), T); await engine.InvalidateAsync();
        await engine.TickAsync(Context([Display(on: true)], [], false), T.AddSeconds(10)); Assert.Single(hdr.Calls);
    }
    [Fact] public void Minimized_profile_is_ignored_by_default()
    {
        var c = Context([Display()], [Profile()]) with { Windows = [new(1, "a", false, false, true)] };
        Assert.Null(new RulesEngine().Evaluate(c)[0].DesiredHdr);
    }
    [Fact] public void Explicit_display_preference_wins_over_window_location()
    {
        var p = Profile(); p.PreferredDisplayId = "b";
        var d = new RulesEngine().Evaluate(Context([Display(), Display("b")], [p]));
        Assert.Null(d[0].DesiredHdr); Assert.True(d[1].DesiredHdr);
    }
}
