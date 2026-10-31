using System.Text;
using HDRPilot.BrowserBridge;
using HDRPilot.Core;
using HDRPilot.GameDetection;

namespace HDRPilot.Core.Tests;
public sealed class ProtocolTests
{
    private const string Valid = "{\"version\":1,\"playing\":true,\"fullscreen\":false,\"range\":\"hdr\",\"browser\":\"chrome\"}";
    [Fact] public void Strict_video_message_parses() { var value = VideoProtocol.Parse(Encoding.UTF8.GetBytes(Valid), "s", DateTimeOffset.UtcNow); Assert.Equal(ContentRange.Hdr, value.Range); Assert.True(value.Playing); }
    [Theory]
    [InlineData("{\"command\":\"powershell\"}")]
    [InlineData("{\"version\":1,\"playing\":true,\"playing\":false,\"fullscreen\":false,\"range\":\"hdr\",\"browser\":\"chrome\"}")]
    [InlineData("{\"version\":2,\"playing\":true,\"fullscreen\":false,\"range\":\"hdr\",\"browser\":\"chrome\"}")]
    [InlineData("{\"version\":1,\"playing\":true,\"fullscreen\":false,\"range\":\"anything\",\"browser\":\"chrome\"}")]
    public void Invalid_messages_rejected(string json) => Assert.Throws<InvalidDataException>(() => VideoProtocol.Parse(Encoding.UTF8.GetBytes(json), "s", DateTimeOffset.UtcNow));
    [Fact] public async Task Oversized_frames_rejected_before_allocation()
    { using var stream = new MemoryStream([255, 255, 255, 127]); await Assert.ThrowsAsync<InvalidDataException>(() => VideoProtocol.ReadFrameAsync(stream, default)); }
    [Fact] public async Task Truncated_frames_are_not_accepted()
    { using var stream = new MemoryStream([20, 0, 0, 0, 123]); await Assert.ThrowsAsync<EndOfStreamException>(() => VideoProtocol.ReadFrameAsync(stream, default)); }
    [Fact] public async Task Frames_roundtrip()
    { using var stream = new MemoryStream(); var bytes = Encoding.UTF8.GetBytes(Valid); await VideoProtocol.WriteFrameAsync(stream, bytes, default); stream.Position = 0; Assert.Equal(bytes, await VideoProtocol.ReadFrameAsync(stream, default)); }
    [Fact] public void Vdf_reader_preserves_windows_paths_and_skips_comments()
    { var pairs = VdfReader.Pairs("// comment\n\"libraryfolders\" { \"0\" { \"path\" \"C:\\\\Games\\\\Steam\" } }"); Assert.Contains(("path", @"C:\Games\Steam"), pairs); }
    [Fact] public async Task Browser_pipe_disconnect_expires_signal()
    {
        var pipeName = "HDRPilot.Test." + Guid.NewGuid().ToString("N");
        await using var bridge = new BrowserBridge.BrowserBridge(() => true, pipeName); using var cancel = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var run = bridge.RunAsync(cancel.Token);
        await using (var client = new System.IO.Pipes.NamedPipeClientStream(".", pipeName, System.IO.Pipes.PipeDirection.Out, System.IO.Pipes.PipeOptions.Asynchronous | System.IO.Pipes.PipeOptions.CurrentUserOnly))
        {
            await client.ConnectAsync(cancel.Token); await VideoProtocol.WriteFrameAsync(client, Encoding.UTF8.GetBytes(Valid), cancel.Token);
            for (var i = 0; i < 20 && bridge.Snapshot().Count == 0; i++) await Task.Delay(25, cancel.Token);
            Assert.Single(bridge.Snapshot());
        }
        for (var i = 0; i < 20 && bridge.Snapshot().Count > 0; i++) await Task.Delay(25, cancel.Token);
        Assert.Empty(bridge.Snapshot()); cancel.Cancel(); try { await run; } catch (OperationCanceledException) { }
    }
}
