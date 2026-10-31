using System.Collections.Concurrent;
using System.IO.Pipes;
using System.Text.Json;
using HDRPilot.Core;

namespace HDRPilot.BrowserBridge;

public sealed class BrowserBridge(Func<bool> enabled, string? pipeName = null) : IBrowserBridge
{
    private readonly ConcurrentDictionary<string, VideoSignal> signals = new();
    public IReadOnlyList<VideoSignal> Snapshot()
    {
        if (!enabled()) { signals.Clear(); return []; }
        var cutoff = DateTimeOffset.UtcNow.AddSeconds(-12);
        foreach (var pair in signals) if (pair.Value.ReceivedAt < cutoff) signals.TryRemove(pair.Key, out _);
        return signals.Values.ToArray();
    }
    public Task RunAsync(CancellationToken ct) => Task.WhenAll(Enumerable.Range(0, 4).Select(_ => ListenAsync(ct)));
    private async Task ListenAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var session = Guid.NewGuid().ToString("N");
            try
            {
                await using var pipe = new NamedPipeServerStream(pipeName ?? VideoProtocol.PipeName, PipeDirection.In, 4, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(ct);
                var last = DateTimeOffset.MinValue;
                while (pipe.IsConnected && !ct.IsCancellationRequested)
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(15));
                    var body = await VideoProtocol.ReadFrameAsync(pipe, timeout.Token); if (body is null) break;
                    var now = DateTimeOffset.UtcNow;
                    if ((now - last).TotalMilliseconds < 400) throw new InvalidDataException("Message rate exceeded.");
                    last = now;
                    var signal = VideoProtocol.Parse(body, session, now);
                    if (enabled()) signals[session] = signal;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or OperationCanceledException) { }
            finally { signals.TryRemove(session, out _); }
            if (!ct.IsCancellationRequested) await Task.Delay(500, ct);
        }
    }
    public ValueTask DisposeAsync() { signals.Clear(); return ValueTask.CompletedTask; }
}
