using System.Text.Json;
using System.Text.Json.Serialization;
using HDRPilot.Core;

namespace HDRPilot.Infrastructure;

public sealed class JsonSettingsService : ISettingsService, IProfileRepository, IDisposable
{
    public static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, Converters = { new JsonStringEnumConverter() }, MaxDepth = 32 };
    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HDRPilot", "config.json");
    private readonly string path;
    private readonly SemaphoreSlim gate = new(1, 1);
    private AppSettings current = new();
    private bool readOnly;
    public string? RecoveryMessage { get; private set; }
    public AppSettings Current => Clone(Volatile.Read(ref current));
    public JsonSettingsService(string? path = null) => this.path = path ?? DefaultPath;
    private static AppSettings Clone(AppSettings value) => JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(value, JsonOptions), JsonOptions)!;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        await gate.WaitAsync(ct);
        try
        {
            if (!File.Exists(path)) return;
            var settings = await ReadAsync(path, ct);
            if (settings.SchemaVersion > 1) { readOnly = true; RecoveryMessage = "Configuration belongs to a newer version. Running with defaults; saving is disabled to preserve it."; return; }
            if (settings.SchemaVersion == 0)
            {
                File.Copy(path, path + ".pre-v1.bak", true);
                settings.SchemaVersion = 1;
                SettingsValidation.Validate(settings);
                await WriteAsync(path, settings, ct);
            }
            SettingsValidation.Validate(settings);
            Volatile.Write(ref current, settings);
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            File.Copy(path, path + $".corrupt-{DateTime.UtcNow:yyyyMMddHHmmssfff}", false);
            RecoveryMessage = "Invalid configuration was preserved in a .corrupt backup. Defaults loaded.";
            Volatile.Write(ref current, new AppSettings());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            readOnly = true;
            RecoveryMessage = "Configuration could not be read. Saving is disabled to prevent data loss.";
        }
        finally { gate.Release(); }
    }
    private static async Task<AppSettings> ReadAsync(string file, CancellationToken ct)
    {
        if (new FileInfo(file).Length > 2 * 1024 * 1024) throw new InvalidDataException("Configuration exceeds the 2 MB limit.");
        await using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        return await JsonSerializer.DeserializeAsync<AppSettings>(stream, JsonOptions, ct) ?? throw new InvalidDataException("Empty configuration.");
    }
    private static async Task WriteAsync(string file, AppSettings value, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(file))!);
        var temp = file + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough | FileOptions.Asynchronous))
            { await JsonSerializer.SerializeAsync(stream, value, JsonOptions, ct); await stream.FlushAsync(ct); stream.Flush(true); }
            if (File.Exists(file)) File.Replace(temp, file, file + ".bak"); else File.Move(temp, file);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public async Task SaveAsync(AppSettings settings, CancellationToken ct = default)
    {
        SettingsValidation.Validate(settings); var copy = Clone(settings);
        await gate.WaitAsync(ct);
        try
        {
            if (readOnly) throw new InvalidOperationException(RecoveryMessage);
            await WriteAsync(path, copy, ct); Volatile.Write(ref current, copy);
        }
        finally { gate.Release(); }
    }
    public async Task ImportAsync(string file, CancellationToken ct = default) => await SaveAsync(await ReadAsync(file, ct), ct);
    public Task ExportAsync(string file, CancellationToken ct = default) => WriteAsync(file, Current, ct);
    public IReadOnlyList<GameProfile> GetProfiles() => Current.Profiles;
    public async Task SaveProfileAsync(GameProfile profile, CancellationToken ct = default)
    {
        await UpdateAsync(value =>
        {
            var previous = value.Profiles.FirstOrDefault(p => p.Id == profile.Id);
            if (previous?.LastPlayed is { } played && (profile.LastPlayed is null || played > profile.LastPlayed)) profile.LastPlayed = played;
            value.Profiles.RemoveAll(p => p.Id == profile.Id); value.Profiles.Add(profile);
        }, ct);
    }
    public async Task DeleteProfileAsync(Guid id, CancellationToken ct = default)
    {
        await UpdateAsync(value => value.Profiles.RemoveAll(p => p.Id == id), ct);
    }
    public Task RecordPlayedAsync(Guid id, DateTimeOffset when, CancellationToken ct = default) => UpdateAsync(value => { var p = value.Profiles.FirstOrDefault(p => p.Id == id); if (p is not null) p.LastPlayed = when; }, ct);
    public async Task UpdateAsync(Action<AppSettings> action, CancellationToken ct = default)
    {
        await gate.WaitAsync(ct);
        try
        {
            if (readOnly) throw new InvalidOperationException(RecoveryMessage);
            var copy = Current; action(copy); SettingsValidation.Validate(copy); copy = Clone(copy);
            await WriteAsync(path, copy, ct); Volatile.Write(ref current, copy);
        }
        finally { gate.Release(); }
    }
    public void Dispose() => gate.Dispose();
}
