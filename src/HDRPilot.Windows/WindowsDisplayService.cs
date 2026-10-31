using System.ComponentModel;
using System.Diagnostics;
using HDRPilot.Core;
using static HDRPilot.Windows.DisplayNative;

namespace HDRPilot.Windows;

public sealed class WindowsDisplayService : IDisplayDiscoveryService, IHdrController, IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly bool modern = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 26100);
    private sealed record TargetDisplay(DisplayInfo Info, Luid Adapter, uint TargetId);

    public async Task<IReadOnlyList<DisplayInfo>> GetDisplaysAsync(CancellationToken ct = default)
    {
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try { return await Task.Run(() => Enumerate().Select(x => x.Info).ToArray(), ct).ConfigureAwait(false); }
        finally { gate.Release(); }
    }

    private List<TargetDisplay> Enumerate()
    {
        // Active desktop paths only: disconnected/inactive outputs must not receive writes.
        const uint flags = 2;
        for (var retry = 0; retry < 4; retry++)
        {
            var error = GetDisplayConfigBufferSizes(flags, out var pathCount, out var modeCount);
            if (error != 0) throw new Win32Exception(error, "Cannot enumerate displays in this Windows session.");
            if (pathCount > 256 || modeCount > 4096) throw new InvalidOperationException("Unexpected display buffer size.");
            var paths = new PathInfo[pathCount]; var modes = new ModeInfo[modeCount];
            error = QueryDisplayConfig(flags, ref pathCount, paths, ref modeCount, modes, 0);
            if (error == 122) continue;
            if (error != 0) throw new Win32Exception(error);
            var result = new List<TargetDisplay>();
            foreach (var path in paths.Take((int)pathCount))
            {
                var target = new TargetName { Header = MakeHeader<TargetName>(2, path.Target.Adapter, path.Target.Id) };
                var source = new SourceName { Header = MakeHeader<SourceName>(1, path.Source.Adapter, path.Source.Id) };
                var adapter = new AdapterName { Header = MakeHeader<AdapterName>(4, path.Target.Adapter, 0) };
                var targetError = GetTargetName(ref target); var sourceError = GetSourceName(ref source);
                _ = GetAdapterName(ref adapter);
                var stable = targetError == 0 && !string.IsNullOrWhiteSpace(target.DevicePath);
                var id = stable ? target.DevicePath.ToUpperInvariant() : $"volatile:{path.Target.Adapter.High}:{path.Target.Adapter.Low}:{path.Target.Id}";
                bool supported = false, enabled = false, writable = false; string? limitation = null;
                if (modern)
                {
                    var color = new ColorInfo2 { Header = MakeHeader<ColorInfo2>(15, path.Target.Adapter, path.Target.Id) };
                    var err = GetColor2(ref color);
                    if (err == 0)
                    {
                        supported = (color.Flags & 16) != 0; enabled = color.ActiveMode == 2;
                        writable = supported && (color.Flags & 8) == 0;
                        if (!writable) limitation = supported ? "HDR is limited by Windows policy." : "This active display does not report HDR support.";
                    }
                    else limitation = $"HDR query failed ({err}). No legacy fallback on Windows 11 24H2+ to avoid confusing HDR with WCG.";
                }
                else
                {
                    var color = new ColorInfo { Header = MakeHeader<ColorInfo>(9, path.Target.Adapter, path.Target.Id) };
                    var err = GetColor(ref color);
                    if (err == 0)
                    {
                        supported = (color.Flags & 1) != 0 && (color.Flags & 4) == 0;
                        enabled = supported && (color.Flags & 2) != 0;
                        writable = supported && (color.Flags & 8) == 0;
                        limitation = "Legacy advanced-color API; HDR capability is inferred. WCG-enforced displays are excluded.";
                    }
                    else limitation = $"Advanced color unavailable ({err}).";
                }
                if (!stable) { writable = false; limitation = "No stable monitor identity; HDR writes are disabled."; }
                var mode = path.Source.ModeIndex < modeCount ? modes[path.Source.ModeIndex] : default;
                result.Add(new(new(id, string.IsNullOrWhiteSpace(target.FriendlyName) ? "Display" : target.FriendlyName,
                    sourceError == 0 ? source.Name : "", (int)mode.Width, (int)mode.Height,
                    path.Target.Refresh.Denominator == 0 ? 0 : (double)path.Target.Refresh.Numerator / path.Target.Refresh.Denominator,
                    mode.Type == 1 && mode.X == 0 && mode.Y == 0, supported, enabled, writable, adapter.Name ?? "", limitation), path.Target.Adapter, path.Target.Id));
            }
            return result.DistinctBy(x => x.Info.Id).ToList();
        }
        throw new InvalidOperationException("Display topology kept changing. Try again after it stabilizes.");
    }

    public async Task<HdrOperationResult> SetHdrAsync(string displayId, bool enabled, bool? expectedState = null, CancellationToken ct = default)
    {
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await Task.Run(async () =>
            {
                var display = Enumerate().FirstOrDefault(d => d.Info.Id == displayId);
                if (display is null) return new HdrOperationResult(false, displayId, enabled, null, "Display is disconnected.");
                if (!display.Info.CanSetHdr) return new(false, displayId, enabled, display.Info.HdrEnabled, display.Info.Limitation ?? "HDR is unavailable.");
                if (expectedState.HasValue && expectedState != display.Info.HdrEnabled) return new(false, displayId, enabled, display.Info.HdrEnabled, "Display changed externally; request cancelled.");
                if (display.Info.HdrEnabled == enabled) return new(true, displayId, enabled, enabled, null);
                ct.ThrowIfCancellationRequested();
                var state = new SetColor { Header = MakeHeader<SetColor>(modern ? 16u : 10u, display.Adapter, display.TargetId), Enabled = enabled ? 1u : 0u };
                var code = SetColorState(ref state);
                // Once a native write begins, verification must complete even if shutdown is requested.
                bool? actual = null;
                for (var i = 0; i < 8; i++)
                {
                    await Task.Delay(250).ConfigureAwait(false);
                    actual = Enumerate().FirstOrDefault(d => d.Info.Id == displayId)?.Info.HdrEnabled;
                    if (actual == enabled || actual is null || code != 0) break;
                }
                return new(code == 0 && actual == enabled, displayId, enabled, actual,
                    code != 0 ? $"Windows rejected HDR change ({code}: {new Win32Exception(code).Message})." : actual != enabled ? "HDR read-back did not match the request." : null);
            }, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException) { return new(false, displayId, enabled, null, ex.Message); }
        finally { gate.Release(); }
    }
    public void Dispose() => gate.Dispose();
}
