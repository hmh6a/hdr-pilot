using HDRPilot.Core;
using HDRPilot.GameDetection;

namespace HDRPilot.App.ViewModels;

public sealed class GameLibraryItem(DiscoveredGame game) : ObservableObject
{
    public DiscoveredGame Game { get; } = game;
    public string Name => Game.Name;
    public string Source => Game.Source;
    private string status = "لم تتم الإضافة بعد";
    private bool configured;
    public string ProfileStatus { get => status; private set => Set(ref status, value); }
    public string ActionText => configured ? "تعديل الإعداد" : "إعداد HDR";
    public void Refresh(IReadOnlyList<GameProfile> profiles)
    {
        var matches = profiles.Where(p => GameSetup.IsInside(p.ExecutablePath, Game.InstallDirectory)).ToArray();
        configured = matches.Length > 0; Raise(nameof(ActionText));
        ProfileStatus = matches.Any(p => p.Enabled) ? "✓ البروفايل مفعّل" : configured ? "البروفايل متوقف" : "لم تتم الإضافة بعد";
    }
}
