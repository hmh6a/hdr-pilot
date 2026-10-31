using System.IO.Pipes;
using System.Text.Json;
using HDRPilot.BrowserBridge;

// No shell invocation. This executable accepts only framed video-state JSON.
try
{
    var manifestPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HDRPilot", "browser", "host.json");
    if (!File.Exists(manifestPath) || new FileInfo(manifestPath).Length > 8192) return 2;
    using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
    var origins = manifest.RootElement.GetProperty("allowed_origins").EnumerateArray().Select(p => p.GetString());
    if (args.Length == 0 || !origins.Contains(args[0])) return 3;
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(3));
    await using var pipe = new NamedPipeClientStream(".", VideoProtocol.PipeName, PipeDirection.Out, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
    await pipe.ConnectAsync(timeout.Token);
    using var input = Console.OpenStandardInput(); using var output = Console.OpenStandardOutput();
    var last = DateTimeOffset.MinValue;
    while (true)
    {
        using var readTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var data = await VideoProtocol.ReadFrameAsync(input, readTimeout.Token); if (data is null) break;
        var now = DateTimeOffset.UtcNow;
        if ((now - last).TotalMilliseconds < 400) return 4;
        VideoProtocol.Parse(data, "host", now); last = now;
        await VideoProtocol.WriteFrameAsync(pipe, data, CancellationToken.None);
        await VideoProtocol.WriteFrameAsync(output, "{\"ok\":true}"u8.ToArray(), CancellationToken.None);
    }
    return 0;
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException or OperationCanceledException) { return 1; }
