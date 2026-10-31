using HDRPilot.Core;

namespace HDRPilot.GameDetection;

public sealed record GameExecutableChoice(string Path, string Label, bool IsRunning);

public static class GameSetup
{
    public static bool IsInside(string path, string directory)
    {
        if (!Path.IsPathFullyQualified(path) || !Path.IsPathFullyQualified(directory)) return false;
        var root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return Path.GetFullPath(path).StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsGameExecutable(string path)
    {
        if (!Path.IsPathFullyQualified(path) || !path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return false;
        var name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
        return !new[] { "unins", "crashreport", "crashpad", "unitycrash", "easyanticheat", "start_protected_game", "battleye", "beservice", "vcredist", "vc_redist", "dxsetup", "dotnet", "ueprereq", "ue4prereq" }.Any(name.StartsWith)
            && name is not ("setup" or "install" or "uninstall" or "launcher" or "updater" or "gamelaunchhelper" or "ealauncher" or "ubisoftconnect" or "steam" or "epicgameslauncher");
    }

    public static IReadOnlyList<GameExecutableChoice> Choices(DiscoveredGame game, IEnumerable<ProcessInfo> visibleApplications)
    {
        var running = visibleApplications.Where(p => p.ExecutablePath is { } path && IsInside(path, game.InstallDirectory))
            .Select(p => Path.GetFullPath(p.ExecutablePath!)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return game.Executables.Concat(running).Where(p => IsGameExecutable(p) && IsInside(p, game.InstallDirectory))
            .Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(p => new GameExecutableChoice(p, (running.Contains(p) ? "مفتوحة الآن · " : "") + Path.GetRelativePath(game.InstallDirectory, p), running.Contains(p)))
            .OrderByDescending(p => p.IsRunning).ThenBy(p => p.Label, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    // Ambiguous installations require an explicit choice, rather than guessing a helper executable.
    public static GameExecutableChoice? Suggested(IReadOnlyList<GameExecutableChoice> choices)
    {
        var running = choices.Where(c => c.IsRunning).ToArray();
        return running.Length == 1 ? running[0] : choices.Count == 1 ? choices[0] : null;
    }

    public static GameProfile CreateProfile(string name, string path, string source, IEnumerable<GameProfile> existing)
    {
        path = Path.GetFullPath(path);
        var previous = existing.FirstOrDefault(p => string.Equals(Path.GetFullPath(p.ExecutablePath), path, StringComparison.OrdinalIgnoreCase));
        return previous is null ? new() { Name = name, ExecutablePath = path, Source = source } : new()
        {
            Id = previous.Id, Name = previous.Name, ExecutablePath = previous.ExecutablePath, Source = previous.Source,
            Mode = previous.Mode, PreferredDisplayId = previous.PreferredDisplayId, Enabled = previous.Enabled,
            ActivationDelayMs = previous.ActivationDelayMs, DeactivationDelayMs = previous.DeactivationDelayMs,
            Priority = previous.Priority, IgnoreWhenMinimized = previous.IgnoreWhenMinimized, LastPlayed = previous.LastPlayed
        };
    }

    public static void SaveProfile(AppSettings settings, GameProfile profile, bool resumeAutomation)
    {
        var previous = settings.Profiles.FirstOrDefault(p => p.Id == profile.Id);
        if (previous?.LastPlayed is { } played && (profile.LastPlayed is null || played > profile.LastPlayed)) profile.LastPlayed = played;
        settings.Profiles.RemoveAll(p => p.Id == profile.Id); settings.Profiles.Add(profile);
        if (resumeAutomation && settings.Mode is not (AutomationMode.Automatic or AutomationMode.ProfilesOnly)) settings.Mode = AutomationMode.Automatic;
    }
}
