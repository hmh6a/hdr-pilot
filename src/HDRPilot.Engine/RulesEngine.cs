using HDRPilot.Core;

namespace HDRPilot.Engine;

public sealed class RulesEngine : IRulesEngine
{
    public IReadOnlyList<RuleDecision> Evaluate(RuleContext context)
    {
        var decisions = new List<RuleDecision>();
        foreach (var display in context.Displays)
        {
            var mode = context.Settings.Mode;
            if (mode == AutomationMode.Suspended) { decisions.Add(new(display.Id, null, "Automation paused; release owned changes")); continue; }
            if (mode is AutomationMode.AlwaysHdr or AutomationMode.AlwaysSdr)
            {
                decisions.Add(new(display.Id, mode == AutomationMode.AlwaysHdr, mode == AutomationMode.AlwaysHdr ? "Always HDR" : "Always SDR (owned changes only)", 1000)); continue;
            }
            var matches = context.Profiles.Where(p => p.Enabled).OrderByDescending(p => p.Priority).ThenBy(p => p.Id);
            GameProfile? chosen = null;
            foreach (var profile in matches)
            {
                var processes = context.Processes.Where(p => string.Equals(p.ExecutablePath, profile.ExecutablePath, StringComparison.OrdinalIgnoreCase));
                var eligible = processes.Any(p => context.Windows.Where(w => w.ProcessId == p.Id).Any(w =>
                    (!profile.IgnoreWhenMinimized || !w.IsMinimized) &&
                    (profile.Mode != HdrMode.WhileForeground || w.IsForeground) &&
                    (profile.Mode is not (HdrMode.FullscreenOnly or HdrMode.Automatic) || w.IsFullscreen && w.IsForeground) &&
                    (profile.PreferredDisplayId is { Length: > 0 } preferred ? preferred == display.Id : w.DeviceName == display.DeviceName)));
                // A configured monitor permits a running-only profile without a visible window.
                if (!eligible && profile.Mode == HdrMode.WhileRunning && profile.PreferredDisplayId == display.Id && !profile.IgnoreWhenMinimized)
                    eligible = processes.Any();
                if (eligible) { chosen = profile; break; }
            }
            if (chosen is not null)
            {
                decisions.Add(new(display.Id, chosen.Mode == HdrMode.NeverModify ? null : chosen.Mode != HdrMode.ForceSdr,
                    $"{chosen.Name} · {chosen.Mode} · priority {chosen.Priority}", chosen.ActivationDelayMs, chosen.DeactivationDelayMs, chosen.Mode == HdrMode.NeverModify));
                continue;
            }
            if (mode == AutomationMode.Automatic && context.Settings.BrowserEnabled)
            {
                var foreground = context.Windows.FirstOrDefault(w => w.IsForeground && w.DeviceName == display.DeviceName && !w.IsMinimized);
                var process = context.Processes.FirstOrDefault(p => p.Id == foreground?.ProcessId);
                var browser = process?.Name.ToLowerInvariant() is "chrome" or "msedge";
                var video = context.Videos.Any(v => v.BrowserProcessName.Equals(process?.Name, StringComparison.OrdinalIgnoreCase) && v.Playing && (!context.Settings.BrowserFullscreenOnly || v.Fullscreen) &&
                    (v.Range == ContentRange.Hdr || v.Range == ContentRange.Sdr && context.Settings.RequestHdrForSdrVideo));
                if (browser && video) { decisions.Add(new(display.Id, true, "YouTube companion · user-classified video", 2000, 5000)); continue; }
            }
            decisions.Add(new(display.Id, null, "No matching rule · preserve original state"));
        }
        return decisions;
    }
}
