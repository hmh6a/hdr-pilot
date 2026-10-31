using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using HDRPilot.Core;

namespace HDRPilot.BrowserBridge;

public static class VideoProtocol
{
    public const int MaxMessageBytes = 1024;
    private static readonly Lazy<string> Name = new(() =>
    {
        using var identity = WindowsIdentity.GetCurrent(); using var process = System.Diagnostics.Process.GetCurrentProcess();
        return "HDRPilot.Video.v1." + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity.User!.Value)))[..20] + "." + process.SessionId;
    });
    public static string PipeName => Name.Value;
    public static VideoSignal Parse(ReadOnlyMemory<byte> data, string session, DateTimeOffset now)
    {
        if (data.Length is 0 or > MaxMessageBytes) throw new InvalidDataException("Invalid message size.");
        using var doc = JsonDocument.Parse(data, new() { MaxDepth = 4 }); var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Expected object.");
        var fields = root.EnumerateObject().Select(p => p.Name).ToArray();
        if (fields.Length != 5 || fields.Distinct().Count() != 5 || fields.Except(["version", "playing", "fullscreen", "range", "browser"]).Any()) throw new InvalidDataException("Unexpected fields.");
        if (!root.GetProperty("version").TryGetInt32(out var version) || version != 1) throw new InvalidDataException("Unsupported protocol version.");
        var playing = root.GetProperty("playing"); var full = root.GetProperty("fullscreen");
        if (playing.ValueKind is not (JsonValueKind.True or JsonValueKind.False) || full.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw new InvalidDataException("Expected boolean state.");
        var range = root.GetProperty("range").GetString() switch { "hdr" => ContentRange.Hdr, "sdr" => ContentRange.Sdr, "unknown" => ContentRange.Unknown, _ => throw new InvalidDataException("Invalid range.") };
        var browser = root.GetProperty("browser").GetString();
        if (browser is not ("chrome" or "msedge")) throw new InvalidDataException("Unsupported browser.");
        return new(session, playing.GetBoolean(), full.GetBoolean(), range, now, browser);
    }
    public static async Task<byte[]?> ReadFrameAsync(Stream stream, CancellationToken ct)
    {
        var header = new byte[4]; var first = await stream.ReadAsync(header.AsMemory(0, 1), ct);
        if (first == 0) return null;
        await stream.ReadExactlyAsync(header.AsMemory(1), ct);
        var size = BinaryPrimitives.ReadUInt32LittleEndian(header);
        if (size is 0 or > MaxMessageBytes) throw new InvalidDataException("Frame exceeds limit.");
        var body = new byte[size]; await stream.ReadExactlyAsync(body, ct); return body;
    }
    public static async Task WriteFrameAsync(Stream stream, ReadOnlyMemory<byte> body, CancellationToken ct)
    {
        if (body.Length is 0 or > MaxMessageBytes) throw new InvalidDataException("Frame exceeds limit.");
        var header = new byte[4]; BinaryPrimitives.WriteUInt32LittleEndian(header, (uint)body.Length);
        await stream.WriteAsync(header, ct); await stream.WriteAsync(body, ct); await stream.FlushAsync(ct);
    }
}
