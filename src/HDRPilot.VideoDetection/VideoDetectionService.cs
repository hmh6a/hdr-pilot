using HDRPilot.Core;

namespace HDRPilot.VideoDetection;

public sealed class VideoDetectionService : IVideoDetectionService
{
    public IReadOnlyList<string> KnownPlayerNames { get; } = ["vlc", "mpc-hc", "mpc-hc64", "mpc-be", "mpc-be64", "mpv", "potplayermini", "potplayermini64", "wmplayer", "microsoft.media.player"];
    public bool IsPlayer(string processName) => KnownPlayerNames.Contains(processName, StringComparer.OrdinalIgnoreCase);
}
