using System.Text.Json;
using Microsoft.Win32;
using HDRPilot.Core;

namespace HDRPilot.GameDetection;

public sealed class GameDiscoveryService : IGameDiscoveryService
{
    public Task<IReadOnlyList<DiscoveredGame>> DiscoverAsync(CancellationToken ct = default) => Task.Run<IReadOnlyList<DiscoveredGame>>(() =>
    {
        var entries = new List<(string Name, string Path, string Source)>();
        ReadSteam(entries); ReadEpic(entries); ReadRegistry(entries); ReadXbox(entries);
        var result = new List<DiscoveredGame>();
        foreach (var item in entries.Where(e => Path.IsPathFullyQualified(e.Path)).Select(e => (e.Name, Path: Path.GetFullPath(e.Path).TrimEnd(Path.DirectorySeparatorChar), e.Source)).DistinctBy(e => e.Path, StringComparer.OrdinalIgnoreCase).Take(1000))
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(item.Path) || !Directory.Exists(item.Path)) continue;
            result.Add(new(item.Name, item.Path, item.Source, FindExecutables(item.Path, ct)));
        }
        return result.OrderBy(r => r.Name).ToArray();
    }, ct);

    private static void ReadSteam(List<(string, string, string)> entries)
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        var root = key?.GetValue("SteamPath") as string;
        if (root is null) return;
        var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { root };
        TryRead(() =>
        {
            foreach (var pair in VdfReader.Pairs(ReadSmall(Path.Combine(root, "steamapps", "libraryfolders.vdf"))))
                if (pair.Key == "path" && Path.IsPathFullyQualified(pair.Value)) libraries.Add(pair.Value);
        });
        foreach (var library in libraries.Take(64))
            TryRead(() =>
            {
                foreach (var file in Directory.EnumerateFiles(Path.Combine(library, "steamapps"), "appmanifest_*.acf").Take(2000))
                    TryRead(() =>
                    {
                        var pairs = VdfReader.Pairs(ReadSmall(file));
                        var name = pairs.FirstOrDefault(p => p.Key == "name").Value;
                        var dir = pairs.FirstOrDefault(p => p.Key == "installdir").Value;
                        if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(dir) && dir.IndexOfAny(['/', '\\', ':']) < 0 && dir != "..")
                            entries.Add((name, Path.Combine(library, "steamapps", "common", dir), "Steam"));
                    });
            });
    }
    private static void ReadEpic(List<(string, string, string)> entries) => TryRead(() =>
    {
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "EpicGamesLauncher", "Data", "Manifests");
        foreach (var file in Directory.EnumerateFiles(directory, "*.item").Take(2000))
            TryRead(() =>
            {
                using var json = JsonDocument.Parse(ReadSmall(file)); var root = json.RootElement;
                if (root.TryGetProperty("DisplayName", out var name) && root.TryGetProperty("InstallLocation", out var path) && Path.IsPathFullyQualified(path.GetString() ?? ""))
                    entries.Add((name.GetString() ?? "Epic game", path.GetString()!, "Epic Games"));
            });
    });
    private static void ReadRegistry(List<(string, string, string)> entries)
    {
        foreach (var hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            using var root = RegistryKey.OpenBaseKey(hive, view);
            using var uninstall = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall");
            foreach (var name in uninstall?.GetSubKeyNames() ?? [])
                TryRead(() =>
                {
                    using var item = uninstall!.OpenSubKey(name);
                    var title = item?.GetValue("DisplayName") as string; var path = item?.GetValue("InstallLocation") as string;
                    var publisher = item?.GetValue("Publisher") as string ?? "";
                    if (title is null || path is null || !Path.IsPathFullyQualified(path)) return;
                    var combined = publisher + " " + name;
                    var source = combined.Contains("GOG", StringComparison.OrdinalIgnoreCase) ? "GOG Galaxy" :
                        combined.Contains("Ubisoft", StringComparison.OrdinalIgnoreCase) ? "Ubisoft Connect" :
                        combined.Contains("Electronic Arts", StringComparison.OrdinalIgnoreCase) || combined.Contains("EA Games", StringComparison.OrdinalIgnoreCase) ? "EA App" :
                        combined.Contains("Blizzard", StringComparison.OrdinalIgnoreCase) || combined.Contains("Battle.net", StringComparison.OrdinalIgnoreCase) ? "Battle.net" : null;
                    if (source is not null) entries.Add((title, path.Trim('"'), source));
                });
            using var gog = root.OpenSubKey(@"Software\GOG.com\Games");
            foreach (var name in gog?.GetSubKeyNames() ?? []) TryRead(() =>
            {
                using var item = gog!.OpenSubKey(name);
                if (item?.GetValue("path") is string path && item.GetValue("gameName") is string title) entries.Add((title, path, "GOG Galaxy"));
            });
        }
    }
    private static void ReadXbox(List<(string, string, string)> entries)
    {
        // Accessible Xbox PC game directories only. Never traverse protected WindowsApps.
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed)) TryRead(() =>
        {
            foreach (var folder in Directory.EnumerateDirectories(Path.Combine(drive.RootDirectory.FullName, "XboxGames")).Take(500))
                if (File.Exists(Path.Combine(folder, "Content", "MicrosoftGame.config"))) entries.Add((Path.GetFileName(folder), Path.Combine(folder, "Content"), "Xbox"));
        });
    }
    private static IReadOnlyList<string> FindExecutables(string root, CancellationToken ct)
    {
        var queue = new Queue<(string Path, int Depth)>(); queue.Enqueue((root, 0)); var found = new List<string>(); var visited = 0;
        while (queue.Count > 0 && visited++ < 256 && found.Count < 128)
        {
            ct.ThrowIfCancellationRequested(); var (path, depth) = queue.Dequeue();
            TryRead(() =>
            {
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) return;
                found.AddRange(Directory.EnumerateFiles(path, "*.exe").Where(f => !Path.GetFileName(f).Contains("unins", StringComparison.OrdinalIgnoreCase)).Take(128 - found.Count));
                if (depth < 3) foreach (var child in Directory.EnumerateDirectories(path).Take(256)) queue.Enqueue((child, depth + 1));
            });
        }
        return found.OrderBy(f => f.Length).ToArray();
    }
    private static string ReadSmall(string path) { if (new FileInfo(path).Length > 4 * 1024 * 1024) throw new InvalidDataException("Manifest too large."); return File.ReadAllText(path); }
    private static void TryRead(Action action)
    { try { action(); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or JsonException or ArgumentException) { } }
}
