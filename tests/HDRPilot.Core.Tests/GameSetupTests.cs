using HDRPilot.Core;
using HDRPilot.GameDetection;
using HDRPilot.Infrastructure;

namespace HDRPilot.Core.Tests;

public sealed class GameSetupTests
{
    [Fact]
    public void HelperExecutablesAndAdjacentDirectoriesAreNotSuggested()
    {
        var game = new DiscoveredGame("Game", @"C:\Games\Game", "Steam", [@"C:\Games\Game\game.exe", @"C:\Games\Game\gamelaunchhelper.exe", @"C:\Games\Game\EasyAntiCheat_Setup.exe", @"C:\Games\Game\CrashReportClient.exe", @"C:\Games\Game\unins000.exe", @"C:\Games\Game2\other.exe", @"C:\Games\Game\..\other.exe"]);
        var choices = GameSetup.Choices(game, []);
        Assert.Equal(@"C:\Games\Game\game.exe", Assert.Single(choices).Path);
        Assert.Equal(choices[0], GameSetup.Suggested(choices));
    }

    [Fact]
    public void LiveWindowFindsDeepExecutableAndDeduplicatesCase()
    {
        var path = @"C:\Games\Game\Content\Client\Binaries\Win64\game.exe";
        var game = new DiscoveredGame("Game", @"C:\Games\Game", "Xbox", [path.ToUpperInvariant(), @"C:\Games\Game\alternate.exe"]);
        var choices = GameSetup.Choices(game, [new(1, "game", path), new(2, "outside", @"C:\Games\Game2\other.exe")]);
        Assert.Equal(2, choices.Count);
        Assert.True(GameSetup.Suggested(choices)!.IsRunning);
        Assert.Equal(path.ToUpperInvariant(), GameSetup.Suggested(choices)!.Path);
    }

    [Fact]
    public void AmbiguousOrEmptyChoicesRequireUserSelection()
    {
        var choices = new GameExecutableChoice[] { new(@"C:\Game\a.exe", "a", false), new(@"C:\Game\b.exe", "b", false) };
        Assert.Null(GameSetup.Suggested(choices));
        Assert.Null(GameSetup.Suggested([]));
        Assert.Null(GameSetup.Suggested(choices.Select(c => c with { IsRunning = true }).ToArray()));
    }

    [Fact]
    public void NewProfileUsesWindowFollowingDefaultsWithoutInventingHdrCapability()
    {
        var profile = GameSetup.CreateProfile("Game", @"C:\Games\game.exe", "Steam", []);
        Assert.Equal(HdrMode.WhileRunning, profile.Mode);
        Assert.Null(profile.PreferredDisplayId);
        Assert.True(profile.IgnoreWhenMinimized);
        Assert.True(profile.Enabled);
        Assert.Equal(1500, profile.ActivationDelayMs);
        Assert.Equal(3000, profile.DeactivationDelayMs);
        SettingsValidation.Validate(new() { Profiles = [profile] });
    }

    [Fact]
    public void EditingCreatesDetachedDraftAndPreservesExistingPolicy()
    {
        var existing = new GameProfile { Name = "Custom", ExecutablePath = @"C:\Games\Game.exe", PreferredDisplayId = "monitor-b", Mode = HdrMode.FullscreenOnly, Priority = 222, Enabled = false, LastPlayed = DateTimeOffset.UtcNow };
        var draft = GameSetup.CreateProfile("Game", @"c:\games\game.exe", "Steam", [existing]);
        Assert.Equal(existing.Id, draft.Id);
        Assert.Equal(existing.Mode, draft.Mode);
        Assert.Equal(existing.PreferredDisplayId, draft.PreferredDisplayId);
        Assert.Equal(existing.Priority, draft.Priority);
        Assert.Equal(existing.LastPlayed, draft.LastPlayed);
        draft.Name = "Changed"; draft.Enabled = true;
        Assert.Equal("Custom", existing.Name);
        Assert.False(existing.Enabled);
    }

    [Theory]
    [InlineData(AutomationMode.Suspended, true, AutomationMode.Automatic)]
    [InlineData(AutomationMode.AlwaysSdr, true, AutomationMode.Automatic)]
    [InlineData(AutomationMode.AlwaysHdr, false, AutomationMode.AlwaysHdr)]
    [InlineData(AutomationMode.ProfilesOnly, true, AutomationMode.ProfilesOnly)]
    public async Task SavingIsAtomicAndRepeatedSetupDoesNotDuplicate(AutomationMode initial, bool resume, AutomationMode expected)
    {
        var folder = Path.Combine(Path.GetTempPath(), "HDR PilotGameSetupTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            using var service = new JsonSettingsService(Path.Combine(folder, "config.json"));
            await service.SaveAsync(new() { Mode = initial, Notifications = true, BrowserEnabled = true });
            var first = GameSetup.CreateProfile("Game", @"C:\Game\game.exe", "Steam", []);
            await service.UpdateAsync(s => GameSetup.SaveProfile(s, first, resume));
            var draft = GameSetup.CreateProfile("Game", @"c:\game\GAME.exe", "Steam", service.GetProfiles());
            var played = DateTimeOffset.UtcNow;
            await service.RecordPlayedAsync(first.Id, played);
            draft.PreferredDisplayId = "screen-two";
            await service.UpdateAsync(s => GameSetup.SaveProfile(s, draft, resume));
            using var reloaded = new JsonSettingsService(Path.Combine(folder, "config.json")); await reloaded.LoadAsync();
            var actual = Assert.Single(reloaded.Current.Profiles);
            Assert.Equal(first.Id, actual.Id); Assert.Equal(played, actual.LastPlayed); Assert.Equal("screen-two", actual.PreferredDisplayId);
            Assert.Equal(expected, reloaded.Current.Mode); Assert.True(reloaded.Current.Notifications); Assert.True(reloaded.Current.BrowserEnabled);
        }
        finally { Directory.Delete(folder, true); }
    }
}
