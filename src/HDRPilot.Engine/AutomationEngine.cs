using System.Diagnostics;
using HDRPilot.Core;
using Microsoft.Extensions.Logging;

namespace HDRPilot.Engine;

public sealed class AutomationEngine(IHdrController hdr, IRulesEngine rules, ILogger<AutomationEngine> log) : IDisposable
{
    private sealed class MonitorState(bool initial)
    {
        public HdrOwnership Ownership { get; } = new(initial);
        public bool? Pending;
        public DateTimeOffset Due;
        public DateTimeOffset LastTransition = DateTimeOffset.MinValue;
        public int ReleaseDelay = 3000;
    }
    private readonly Dictionary<string, MonitorState> monitors = [];
    private readonly SemaphoreSlim gate = new(1, 1);
    private string topology = "";
    public EngineState State { get; private set; } = EngineState.Idle;
    public string Reason { get; private set; } = "Waiting for the first evaluation";
    public event EventHandler? Changed;

    public async Task TickAsync(RuleContext context, DateTimeOffset now, CancellationToken ct = default)
    {
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var signature = string.Join('|', context.Displays.OrderBy(d => d.Id).Select(d => $"{d.Id}:{d.DeviceName}:{d.Adapter}"));
            if (topology != signature)
            {
                if (topology.Length != 0) log.LogWarning("Display topology changed. Ownership discarded; current states are preserved.");
                monitors.Clear(); topology = signature;
            }
            State = EngineState.Evaluating;
            var decisions = rules.Evaluate(context);
            var reasons = new List<string>();
            var states = new List<EngineState>();
            foreach (var display in context.Displays)
            {
                ct.ThrowIfCancellationRequested();
                if (!monitors.TryGetValue(display.Id, out var monitor)) monitors[display.Id] = monitor = new(display.HdrEnabled);
                if (monitor.Ownership.Observe(display.HdrEnabled))
                { monitor.Pending = null; log.LogInformation("External HDR change detected on {Display}. Automation yielded until explicitly resumed.", display.Name); }
                var decision = decisions.Single(d => d.DisplayId == display.Id);
                var target = decision.PreserveCurrent ? null : monitor.Ownership.Target(decision.DesiredHdr);
                if (monitor.Ownership.ManualOverride) reasons.Add($"{display.Name}: manual override · click Automatic to resume");
                else if (decision.DesiredHdr == false && monitor.Ownership.Baseline) reasons.Add($"{display.Name}: preserving HDR enabled outside HDR Pilot");
                else reasons.Add($"{display.Name}: {decision.Reason}");
                if (decision.DesiredHdr == true) monitor.ReleaseDelay = decision.DeactivationDelayMs;
                if (!display.CanSetHdr || target is null || target == display.HdrEnabled)
                { monitor.Pending = null; states.Add(display.HdrEnabled ? EngineState.HDRActive : EngineState.Idle); continue; }
                if (monitor.Pending != target)
                {
                    monitor.Pending = target;
                    monitor.Due = now.AddMilliseconds(target.Value ? decision.ActivationDelayMs : monitor.ReleaseDelay);
                    log.LogInformation("{Action} pending on {Display}. {Reason}", target.Value ? "HDR activation" : "HDR restoration", display.Name, decision.Reason);
                }
                if (now < monitor.Due || (now - monitor.LastTransition).TotalMilliseconds < context.Settings.CooldownMs)
                { states.Add(target.Value ? EngineState.PendingActivation : EngineState.PendingDeactivation); continue; }
                State = target.Value ? EngineState.Activating : EngineState.Deactivating; Changed?.Invoke(this, EventArgs.Empty);
                var timer = Stopwatch.StartNew();
                var result = await hdr.SetHdrAsync(display.Id, target.Value, display.HdrEnabled, ct).ConfigureAwait(false);
                monitor.LastTransition = now; monitor.Pending = null;
                if (result.Success && result.ActualState == target)
                {
                    monitor.Ownership.RecordSuccessfulWrite(target.Value);
                    states.Add(target.Value ? EngineState.HDRActive : EngineState.Idle);
                    log.LogInformation("HDR {Status} verified on {Display} in {Latency} ms.", target.Value ? "enabled" : "restored", display.Name, timer.ElapsedMilliseconds);
                }
                else
                {
                    // Failed or ambiguous writes require an explicit resume. No repeated driver hammering.
                    monitor.Ownership.Yield(result.ActualState ?? display.HdrEnabled);
                    states.Add(EngineState.Error);
                    reasons.Add(result.ErrorMessage ?? "HDR transition failed");
                    log.LogWarning("HDR operation failed on {Display}: {Error}", display.Name, result.ErrorMessage);
                }
            }
            State = states.Contains(EngineState.Error) ? EngineState.Error : context.Settings.Mode == AutomationMode.Suspended ? EngineState.Suspended :
                states.Contains(EngineState.PendingActivation) ? EngineState.PendingActivation : states.Contains(EngineState.PendingDeactivation) ? EngineState.PendingDeactivation :
                states.Contains(EngineState.HDRActive) ? EngineState.HDRActive : EngineState.Idle;
            Reason = reasons.Count == 0 ? "No active desktop displays" : string.Join("\n", reasons);
        }
        finally { gate.Release(); Changed?.Invoke(this, EventArgs.Empty); }
    }

    public async Task RearmAsync(IReadOnlyList<DisplayInfo> displays, CancellationToken ct = default)
    {
        await gate.WaitAsync(ct);
        try { foreach (var display in displays) if (monitors.TryGetValue(display.Id, out var m)) { m.Ownership.Rearm(display.HdrEnabled); m.Pending = null; } }
        finally { gate.Release(); }
    }
    public async Task<HdrOperationResult> ManualAsync(DisplayInfo display, bool enabled, CancellationToken ct = default)
    {
        await gate.WaitAsync(ct);
        try
        {
            var result = await hdr.SetHdrAsync(display.Id, enabled, display.HdrEnabled, ct);
            if (!monitors.TryGetValue(display.Id, out var m)) monitors[display.Id] = m = new(display.HdrEnabled);
            m.Ownership.Yield(result.ActualState ?? display.HdrEnabled); m.Pending = null;
            log.LogInformation("Manual HDR request on {Display}: {Result}", display.Name, result.Success ? "verified" : result.ErrorMessage);
            return result;
        }
        finally { gate.Release(); }
    }
    public async Task InvalidateAsync()
    {
        await gate.WaitAsync();
        try { monitors.Clear(); topology = ""; State = EngineState.Error; Reason = "Display state is uncertain; ownership discarded for safety"; }
        finally { gate.Release(); }
    }
    public async Task RestoreAsync(IReadOnlyList<DisplayInfo> displays)
    {
        await gate.WaitAsync();
        try
        {
            foreach (var display in displays)
            {
                if (!monitors.TryGetValue(display.Id, out var m)) continue;
                m.Ownership.Observe(display.HdrEnabled);
                if (m.Ownership.Owned && !m.Ownership.ManualOverride && display.CanSetHdr)
                {
                    var result = await hdr.SetHdrAsync(display.Id, m.Ownership.Baseline, display.HdrEnabled);
                    log.LogInformation("Exit restoration on {Display}: {Result}", display.Name, result.Success ? "verified" : result.ErrorMessage);
                }
            }
            monitors.Clear();
        }
        finally { gate.Release(); }
    }
    public void Dispose() => gate.Dispose();
}
