using HDRPilot.Core;
using HDRPilot.Infrastructure;

namespace HDRPilot.Core.Tests;
public sealed class SettingsTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "HDRPilot.Tests", Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(folder, "config.json");
    public SettingsTests() => Directory.CreateDirectory(folder);
    [Fact] public async Task Atomic_save_and_roundtrip()
    {
        using var service = new JsonSettingsService(FilePath);
        await service.SaveAsync(new() { CooldownMs = 8000 }); await service.SaveAsync(new() { CooldownMs = 9000 });
        using var read = new JsonSettingsService(FilePath); await read.LoadAsync();
        Assert.Equal(9000, read.Current.CooldownMs); Assert.True(File.Exists(FilePath + ".bak")); Assert.Empty(Directory.GetFiles(folder, "*.tmp"));
    }
    [Fact] public async Task Corrupt_file_is_preserved()
    {
        await File.WriteAllTextAsync(FilePath, "{ broken"); using var service = new JsonSettingsService(FilePath); await service.LoadAsync();
        Assert.NotNull(service.RecoveryMessage); Assert.Single(Directory.GetFiles(folder, "*.corrupt-*"));
    }
    [Fact] public async Task Future_schema_cannot_be_overwritten()
    {
        await File.WriteAllTextAsync(FilePath, "{\"SchemaVersion\":99}"); using var service = new JsonSettingsService(FilePath); await service.LoadAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(new())); Assert.Contains("99", await File.ReadAllTextAsync(FilePath));
    }
    [Fact] public async Task Migration_creates_backup()
    {
        await File.WriteAllTextAsync(FilePath, "{\"SchemaVersion\":0}"); using var service = new JsonSettingsService(FilePath); await service.LoadAsync();
        Assert.Equal(1, service.Current.SchemaVersion); Assert.True(File.Exists(FilePath + ".pre-v1.bak"));
    }
    [Fact] public void Invalid_timing_and_paths_are_rejected()
    {
        Assert.Throws<InvalidDataException>(() => SettingsValidation.Validate(new() { CooldownMs = -1 }));
        Assert.Throws<InvalidDataException>(() => SettingsValidation.Validate(new() { Profiles = [new() { Name = "bad", ExecutablePath = "cmd.exe" }] }));
    }
    [Fact] public async Task Failed_import_preserves_current_settings()
    {
        using var service = new JsonSettingsService(FilePath); await service.SaveAsync(new() { CooldownMs = 7000 });
        var bad = Path.Combine(folder, "bad.json"); await File.WriteAllTextAsync(bad, "{\"CooldownMs\":-1}");
        await Assert.ThrowsAsync<InvalidDataException>(() => service.ImportAsync(bad)); Assert.Equal(7000, service.Current.CooldownMs);
    }
    [Fact] public async Task Concurrent_profile_and_mode_updates_do_not_lose_data()
    {
        using var service = new JsonSettingsService(FilePath);
        var profiles = Enumerable.Range(0, 12).Select(i => new GameProfile { Name = "Game " + i, ExecutablePath = $@"C:\Games\{i}.exe" }).ToArray();
        var writes = profiles.Select(p => service.SaveProfileAsync(p)).Append(service.UpdateAsync(s => s.Mode = AutomationMode.Suspended));
        await Task.WhenAll(writes);
        Assert.Equal(12, service.Current.Profiles.Count); Assert.Equal(AutomationMode.Suspended, service.Current.Mode);
    }
    [Fact] public async Task Editing_older_profile_does_not_erase_recent_play_timestamp()
    {
        using var service = new JsonSettingsService(FilePath); var profile = new GameProfile { Name = "Game", ExecutablePath = @"C:\Games\game.exe" };
        await service.SaveProfileAsync(profile); var edit = service.GetProfiles()[0]; var played = DateTimeOffset.UtcNow;
        await service.RecordPlayedAsync(profile.Id, played); edit.Name = "Renamed"; await service.SaveProfileAsync(edit);
        Assert.Equal(played, service.GetProfiles()[0].LastPlayed); Assert.Equal("Renamed", service.GetProfiles()[0].Name);
    }
    [Fact] public async Task Saved_state_does_not_alias_mutable_editor_objects()
    {
        using var service = new JsonSettingsService(FilePath); var profile = new GameProfile { Name = "Saved", ExecutablePath = @"C:\Games\game.exe" };
        await service.SaveProfileAsync(profile); profile.Name = "Unsaved";
        Assert.Equal("Saved", service.GetProfiles()[0].Name);
    }
    public void Dispose() => Directory.Delete(folder, true);
}
