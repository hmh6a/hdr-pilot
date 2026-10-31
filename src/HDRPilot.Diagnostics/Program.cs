using System.Text.Json;
using System.Runtime.InteropServices;
using HDRPilot.Windows;
using HDRPilot.Core;
using HDRPilot.Engine;
using Microsoft.Extensions.Logging.Abstractions;

using var displays = new WindowsDisplayService();
if (args.Contains("--abi"))
{
    Console.WriteLine($"Header={Marshal.SizeOf<DisplayNative.Header>()} Path={Marshal.SizeOf<DisplayNative.PathInfo>()} Mode={Marshal.SizeOf<DisplayNative.ModeInfo>()} Color2={Marshal.SizeOf<DisplayNative.ColorInfo2>()} SetHdr={Marshal.SizeOf<DisplayNative.SetColor>()} TargetName={Marshal.SizeOf<DisplayNative.TargetName>()}");
    return;
}
try
{
    var initial = await displays.GetDisplaysAsync();
    Console.WriteLine(JsonSerializer.Serialize(initial, new JsonSerializerOptions { WriteIndented = true }));
    if (args.Length == 2 && args[0] == "--profile-roundtrip")
    {
        using var processes = new ProcessMonitor(); using var windows = new WindowMonitor();
        using var engine = new AutomationEngine(displays, new RulesEngine(), NullLogger<AutomationEngine>.Instance);
        var profile = new GameProfile { Name = "Hardware integration test", ExecutablePath = Path.GetFullPath(args[1]), Mode = HdrMode.WhileRunning, ActivationDelayMs = 0, DeactivationDelayMs = 0 };
        var context = new RuleContext(initial, processes.Snapshot(), windows.Snapshot(), [profile], new() { CooldownMs = 1000 }, []);
        if (!context.Processes.Any(p => string.Equals(p.ExecutablePath, profile.ExecutablePath, StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("The test application must be running.");
        try
        {
            await engine.TickAsync(context, DateTimeOffset.UtcNow);
            Console.WriteLine("Rule: " + engine.Reason);
            var active = await displays.GetDisplaysAsync();
            if (!active.Any(d => d.HdrEnabled && initial.Any(i => i.Id == d.Id && !i.HdrEnabled))) throw new InvalidOperationException("No OFF-to-ON transition observed. Use an initially SDR display and a visible test app.");
            Console.WriteLine("Profile activation: actual HDR ON verified.");
            await Task.Delay(1500);
            await engine.TickAsync(context with { Displays = active, Processes = [], Windows = [] }, DateTimeOffset.UtcNow);
            var restored = await displays.GetDisplaysAsync();
            if (initial.Any(i => restored.FirstOrDefault(d => d.Id == i.Id)?.HdrEnabled != i.HdrEnabled)) throw new InvalidOperationException("Original state was not restored.");
            Console.WriteLine("Profile exit: original display state verified.");
        }
        finally { await engine.RestoreAsync(await displays.GetDisplaysAsync()); }
    }
    if (args.Length == 2 && args[0] == "--roundtrip")
    {
        var display = initial.SingleOrDefault(d => d.Id == args[1]);
        if (display is null || !display.CanSetHdr) throw new ArgumentException("Select a writable display ID from the discovery output.");
        try
        {
            Console.WriteLine(JsonSerializer.Serialize(await displays.SetHdrAsync(display.Id, !display.HdrEnabled, display.HdrEnabled)));
            await Task.Delay(2500);
        }
        finally { Console.WriteLine(JsonSerializer.Serialize(await displays.SetHdrAsync(display.Id, display.HdrEnabled, !display.HdrEnabled))); }
    }
}
catch (Exception ex) { Console.Error.WriteLine(ex.Message); Environment.ExitCode = 1; }
