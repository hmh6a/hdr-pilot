using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using HDRPilot.Core;
using HDRPilot.Engine;
using HDRPilot.Infrastructure;
using HDRPilot.GameDetection;
using HDRPilot.App.Services;

namespace HDRPilot.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly ISettingsService settings;
    private readonly IProfileRepository profiles;
    private readonly IGameDiscoveryService games;
    private readonly IDisplayDiscoveryService discovery;
    private readonly AutomationEngine engine;
    private readonly AutomationRuntime runtime;
    private readonly ActivityLog log;
    private readonly IStartupManager startup;
    private string status = "Ready", filter = "", logFilter = "", automation = "Starting", rule = "Reading displays…", active = "—", metrics = "Measuring resource use…", browserStatus = "No companion connected";
    private DisplayInfo? selectedDisplay;
    private GameProfile? selectedProfile;
    private ProcessInfo? selectedProcess;
    private ProcessInfo? selectedRunningGame;
    private bool busy, gameDiscoveryStarted;
    private string gamePageStatus = "افتح الصفحة لعرض ألعابك تلقائيًا.";
    private string gameSearch = "", profileStamp = "";
    private AppSettings draft = new();
    public ObservableCollection<DisplayInfo> Displays { get; } = [];
    public ObservableCollection<GameProfile> Profiles { get; } = [];
    public ObservableCollection<ProcessInfo> Running { get; } = [];
    public ObservableCollection<GameLibraryItem> Games { get; } = [];
    public ObservableCollection<ProcessInfo> RunningGames { get; } = [];
    public ObservableCollection<ActivityEntry> Activity { get; } = [];
    public ICollectionView ProfileView { get; }
    public ICollectionView ActivityView { get; }
    public ICollectionView GameView { get; }
    public string GameSearch { get => gameSearch; set { if (Set(ref gameSearch, value)) GameView.Refresh(); } }
    public string Status { get => status; set => Set(ref status, value); }
    public string Search { get => filter; set { if (Set(ref filter, value)) ProfileView.Refresh(); } }
    public string LogFilter { get => logFilter; set { if (Set(ref logFilter, value)) ActivityView.Refresh(); } }
    public string Automation => automation;
    public string ActiveRule => rule;
    public string ActiveApplication => active;
    public string Metrics => metrics;
    public string BrowserStatus => browserStatus;
    public string GpuStatus => Displays.Any(d => d.Adapter.Contains("VEN_10DE", StringComparison.OrdinalIgnoreCase)) ? "NVIDIA display adapter detected · RTX Video capability is not queried" : "No NVIDIA adapter driving an active display detected";
    public bool IsBusy { get => busy; private set => Set(ref busy, value); }
    public DisplayInfo? SelectedDisplay { get => selectedDisplay; set { Set(ref selectedDisplay, value); Raise(nameof(DisplayTitle)); Raise(nameof(DisplaySummary)); Raise(nameof(HdrStatus)); } }
    public GameProfile? SelectedProfile { get => selectedProfile; set => Set(ref selectedProfile, value); }
    public ProcessInfo? SelectedProcess { get => selectedProcess; set => Set(ref selectedProcess, value); }
    public ProcessInfo? SelectedRunningGame { get => selectedRunningGame; set => Set(ref selectedRunningGame, value); }
    public string GamePageStatus { get => gamePageStatus; private set => Set(ref gamePageStatus, value); }
    public string RunningGameHint => RunningGames.Count == 0 ? "شغّل اللعبة ثم ارجع هنا. تظهر التطبيقات ذات النوافذ فقط." : "اختر لعبتك من التطبيقات المفتوحة. لا تحتاج تعرف مكان ملفها.";
    public string DisplayTitle => SelectedDisplay?.Name ?? "No display connected";
    public string DisplaySummary => SelectedDisplay?.Summary ?? "Waiting for an active desktop display";
    public string HdrStatus => SelectedDisplay?.Status ?? "UNAVAILABLE";
    public AppSettings Draft { get => draft; private set { draft = value; Raise(); } }
    public bool StartWithWindows { get; set; }
    public Array Modes { get; } = Enum.GetValues<AutomationMode>();
    public ICommand ToggleCommand { get; }
    public ICommand AutomaticCommand { get; }
    public ICommand PauseCommand { get; }
    public ICommand CycleCommand { get; }
    public ICommand ScanCommand { get; }
    public ICommand AddCommand { get; }
    public ICommand AddRunningCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand AddGameCommand { get; }
    public ICommand AddRunningGameCommand { get; }
    public ICommand BrowseGameCommand { get; }
    public ICommand SaveSettingsCommand { get; }
    public ICommand TestNotificationCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand ExportDiagnosticsCommand { get; }
    public ICommand WindowsSettingsCommand { get; }
    public ICommand NvidiaHelpCommand { get; }
    public ICommand RefreshRunningCommand { get; }
    public event Action? SettingsApplied;
    public event Action<string, string>? NotificationRequested;

    public MainViewModel(ISettingsService settings, IProfileRepository profiles, IGameDiscoveryService games, IDisplayDiscoveryService discovery,
        AutomationEngine engine, AutomationRuntime runtime, ActivityLog log, IStartupManager startup)
    {
        this.settings = settings; this.profiles = profiles; this.games = games; this.discovery = discovery; this.engine = engine; this.runtime = runtime; this.log = log; this.startup = startup;
        ProfileView = CollectionViewSource.GetDefaultView(Profiles); ProfileView.Filter = o => o is GameProfile p && (p.Name.Contains(Search, StringComparison.OrdinalIgnoreCase) || p.Source.Contains(Search, StringComparison.OrdinalIgnoreCase));
        GameView = CollectionViewSource.GetDefaultView(Games); GameView.Filter = o => o is GameLibraryItem g && (g.Name.Contains(GameSearch, StringComparison.OrdinalIgnoreCase) || g.Source.Contains(GameSearch, StringComparison.OrdinalIgnoreCase));
        ActivityView = CollectionViewSource.GetDefaultView(Activity); ActivityView.Filter = o => o is ActivityEntry a && a.Text.Contains(LogFilter, StringComparison.OrdinalIgnoreCase);
        ToggleCommand = Command(async o => await ManualAsync(o as DisplayInfo ?? SelectedDisplay));
        AutomaticCommand = Command(async _ => await SetModeAsync(AutomationMode.Automatic));
        PauseCommand = Command(async _ => await SetModeAsync(AutomationMode.Suspended));
        CycleCommand = Command(async _ => await SetModeAsync(settings.Current.Mode switch { AutomationMode.Automatic => AutomationMode.AlwaysHdr, AutomationMode.AlwaysHdr => AutomationMode.AlwaysSdr, _ => AutomationMode.Automatic }));
        ScanCommand = Command(async _ =>
        {
            IsBusy = true; GamePageStatus = "جارٍ البحث عن الألعاب المثبتة…";
            try
            {
                var found = await games.DiscoverAsync(); Games.Clear();
                foreach (var game in found) { var item = new GameLibraryItem(game); item.Refresh(Profiles); Games.Add(item); }
                GamePageStatus = Games.Count == 0 ? "لم نجد ألعابًا مثبتة. استخدم اللعبة المفتوحة الآن أو اختر ملفها يدويًا." : $"تم العثور على {Games.Count} لعبة. اضغط إعداد HDR بجانب لعبتك.";
            }
            catch (Exception ex) { GamePageStatus = "تعذّر إكمال البحث. تقدر تضيف اللعبة وهي مفتوحة أو تختار ملفها."; log.Add(ex.Message, "Warning"); }
            finally { IsBusy = false; }
        });
        AddCommand = Command(async _ => { var picker = new Microsoft.Win32.OpenFileDialog { Filter = "Windows applications (*.exe)|*.exe", CheckFileExists = true }; if (picker.ShowDialog() == true) await EditProfileAsync(new() { Name = System.IO.Path.GetFileNameWithoutExtension(picker.FileName), ExecutablePath = picker.FileName }); });
        AddRunningCommand = Command(async _ => { if (SelectedProcess?.ExecutablePath is { } path) await EditProfileAsync(new() { Name = SelectedProcess.Name, ExecutablePath = path }); else Status = "Select an application with a readable executable path."; });
        EditCommand = Command(async _ => { if (SelectedProfile is { } p) await EditProfileAsync(p); });
        DeleteCommand = Command(async _ => { if (SelectedProfile is { } p) { await profiles.DeleteProfileAsync(p.Id); ReloadProfiles(); runtime.Wake(); Status = "Profile removed."; } });
        AddGameCommand = Command(async parameter =>
        {
            if (parameter is not GameLibraryItem item) return;
            var game = item.Game;
            var saved = profiles.GetProfiles().Where(p => GameSetup.IsInside(p.ExecutablePath, game.InstallDirectory)).ToArray();
            await SetupGameAsync(game.Name, game.Source, game.InstallDirectory, () => GameSetup.Choices(game, runtime.Latest.VisibleApplications), saved.Length == 1 ? saved[0].ExecutablePath : null);
        });
        AddRunningGameCommand = Command(async _ =>
        {
            if (SelectedRunningGame?.ExecutablePath is not { } path) { GamePageStatus = "اختَر اللعبة المفتوحة من القائمة أولًا."; return; }
            var game = Games.FirstOrDefault(g => GameSetup.IsInside(path, g.Game.InstallDirectory));
            await SetupGameAsync(game?.Name ?? SelectedRunningGame.Name, game?.Source ?? "Running application", System.IO.Path.GetDirectoryName(path)!,
                () => [new(path, System.IO.Path.GetFileName(path), true)], path);
        });
        BrowseGameCommand = Command(async _ =>
        {
            var picker = new Microsoft.Win32.OpenFileDialog { Filter = "Game executable (*.exe)|*.exe", CheckFileExists = true, Title = "اختر ملف تشغيل اللعبة" };
            if (picker.ShowDialog() == true)
                await SetupGameAsync(System.IO.Path.GetFileNameWithoutExtension(picker.FileName), "Manual", System.IO.Path.GetDirectoryName(picker.FileName)!,
                    () => [new(picker.FileName, System.IO.Path.GetFileName(picker.FileName), false)], picker.FileName);
        });
        SaveSettingsCommand = Command(async _ =>
        {
            await settings.UpdateAsync(value =>
            {
                value.Mode = Draft.Mode; value.CooldownMs = Draft.CooldownMs; value.StartMinimized = Draft.StartMinimized;
                value.Notifications = Draft.Notifications; value.BrowserEnabled = Draft.BrowserEnabled;
                value.RequestHdrForSdrVideo = Draft.RequestHdrForSdrVideo; value.BrowserFullscreenOnly = Draft.BrowserFullscreenOnly;
                value.Hotkeys = Draft.Hotkeys;
            });
            startup.SetEnabled(StartWithWindows); await engine.RearmAsync(await discovery.GetDisplaysAsync()); runtime.Wake(); SettingsApplied?.Invoke(); Status = "Settings saved. Shortcuts re-registered.";
        });
        TestNotificationCommand = Command(async _ =>
        {
            await settings.UpdateAsync(value => value.Notifications = true);
            Draft.Notifications = true; Raise(nameof(Draft));
            NotificationRequested?.Invoke("HDR Pilot notifications enabled", "HDR changes will show the display name and its new Windows HDR state. This is a test notification.");
            Status = "Notifications enabled and test sent. Windows Do not disturb may hide banners.";
        });
        ExportCommand = Command(async _ => { var picker = new Microsoft.Win32.SaveFileDialog { Filter = "JSON settings|*.json", FileName = "HDRPilot-settings.json" }; if (picker.ShowDialog() == true) { await settings.ExportAsync(picker.FileName); Status = "Settings exported (contains executable paths)."; } });
        ImportCommand = Command(async _ => { var picker = new Microsoft.Win32.OpenFileDialog { Filter = "JSON settings|*.json" }; if (picker.ShowDialog() == true) { await settings.ImportAsync(picker.FileName); ReloadSettings(); runtime.Wake(); SettingsApplied?.Invoke(); Status = "Settings imported; backup preserved."; } });
        ResetCommand = Command(async _ =>
        {
            if (MessageBox.Show("Reset all profiles and preferences? The previous configuration is kept as config.json.bak.", "Reset HDR Pilot", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            await settings.SaveAsync(new()); startup.SetEnabled(false); ReloadSettings(); runtime.Wake(); SettingsApplied?.Invoke(); Status = "Defaults restored.";
        });
        ExportDiagnosticsCommand = Command(async _ =>
        {
            var picker = new Microsoft.Win32.SaveFileDialog { Filter = "Text report|*.txt", FileName = "HDRPilot-diagnostics.txt" };
            if (picker.ShowDialog() != true) return;
            var report = $"HDR Pilot 0.1.0\nWindows {Environment.OSVersion.Version}\n{Metrics}\n{Automation}\n" + string.Join("\n", Displays.Select(d => $"{d.Name}: {d.Summary}; {d.Status}; {d.Limitation}")) + "\n\n" + string.Join("\n", Activity.Select(a => a.Text));
            await System.IO.File.WriteAllTextAsync(picker.FileName, report); Status = "Diagnostics exported. Review application names before sharing.";
        });
        WindowsSettingsCommand = new RelayCommand(_ => Process.Start(new ProcessStartInfo("ms-settings:display") { UseShellExecute = true }));
        NvidiaHelpCommand = new RelayCommand(_ => Process.Start(new ProcessStartInfo("https://nvidia.custhelp.com/app/answers/detail/a_id/5448") { UseShellExecute = true }));
        RefreshRunningCommand = new RelayCommand(_ => RefreshRunning());
        runtime.Updated += snapshot => Application.Current.Dispatcher.InvokeAsync(() => Update(snapshot));
        log.Added += entry => Application.Current.Dispatcher.InvokeAsync(() => { Activity.Insert(0, entry); while (Activity.Count > 300) Activity.RemoveAt(Activity.Count - 1); });
        foreach (var entry in log.Entries.Reverse()) Activity.Add(entry);
        ReloadSettings(); if (settings.RecoveryMessage is { } recovery) Status = recovery;
    }
    private AsyncCommand Command(Func<object?, Task> action) => new(action, ex => { Status = ex.Message; log.Add(ex.Message, "Error"); });
    public void ReloadSettings() { Draft = settings.Current; StartWithWindows = startup.IsEnabled; Raise(nameof(StartWithWindows)); ReloadProfiles(); }
    private void ReloadProfiles() { var selected = SelectedProfile?.Id; Profiles.Clear(); foreach (var p in profiles.GetProfiles().OrderBy(p => p.Name)) Profiles.Add(p); SelectedProfile = Profiles.FirstOrDefault(p => p.Id == selected); foreach (var game in Games) game.Refresh(Profiles); }
    public void OpenGames()
    {
        UpdateRunningGames(runtime.Latest);
        if (!gameDiscoveryStarted) { gameDiscoveryStarted = true; ScanCommand.Execute(null); }
    }
    private async Task SetupGameAsync(string name, string source, string directory, Func<IReadOnlyList<GameExecutableChoice>> choices, string? preferredPath)
    {
        var editor = new GameSetupWindow(name, source, directory, Displays.ToArray(), profiles.GetProfiles(), choices, preferredPath) { Owner = Application.Current.MainWindow };
        if (editor.ShowDialog() != true || editor.Profile is not { } profile) return;
        await settings.UpdateAsync(value => GameSetup.SaveProfile(value, profile, editor.ShouldResume));
        Draft.Mode = settings.Current.Mode; Raise(nameof(Draft)); ReloadProfiles();
        if (editor.ShouldResume) await engine.RearmAsync(await discovery.GetDisplaysAsync());
        runtime.Wake();
        Status = GamePageStatus = !profile.Enabled ? $"تم حفظ {profile.Name}. البروفايل متوقف حتى تفعّله." : editor.ShouldResume ? $"✓ تم حفظ {profile.Name}. البروفايل مفعّل؛ شغّل اللعبة لتطبيقه." : $"✓ تم حفظ {profile.Name}. وضع الأتمتة الحالي: {settings.Current.Mode}.";
        log.Add($"Game profile saved: {profile.Name} · {profile.Mode}");
        NotificationRequested?.Invoke("تم حفظ بروفايل اللعبة", profile.Name + (!profile.Enabled ? " — البروفايل متوقف" : editor.ShouldResume ? " — الأتمتة جاهزة" : " — تم الحفظ"));
    }
    private void UpdateRunningGames(RuntimeSnapshot snapshot)
    {
        var visible = snapshot.VisibleApplications.Where(p => p.ExecutablePath is { } path && GameSetup.IsGameExecutable(path) &&
            !GameSetup.IsInside(path, Environment.GetFolderPath(Environment.SpecialFolder.Windows)))
            .DistinctBy(p => p.ExecutablePath, StringComparer.OrdinalIgnoreCase).OrderBy(p => p.Name).ToArray();
        if (RunningGames.SequenceEqual(visible)) return;
        var selected = SelectedRunningGame?.ExecutablePath;
        RunningGames.Clear(); foreach (var process in visible) RunningGames.Add(process);
        SelectedRunningGame = RunningGames.FirstOrDefault(p => p.ExecutablePath == selected); Raise(nameof(RunningGameHint));
    }
    private async Task EditProfileAsync(GameProfile profile)
    {
        var editor = new ProfileWindow(profile, Displays) { Owner = Application.Current.MainWindow };
        if (editor.ShowDialog() == true) { await profiles.SaveProfileAsync(editor.Profile); ReloadProfiles(); runtime.Wake(); Status = $"Saved {editor.Profile.Name}."; }
    }
    public async Task ManualAsync(DisplayInfo? display, bool? enabled = null)
    {
        try
        {
            if (display is null) throw new InvalidOperationException("No display selected.");
            var fresh = (await discovery.GetDisplaysAsync()).FirstOrDefault(d => d.Id == display.Id)
                ?? throw new InvalidOperationException($"{display.Name}: display disconnected.");
            var result = await engine.ManualAsync(fresh, enabled ?? !fresh.HdrEnabled);
            if (!result.Success) throw new InvalidOperationException($"{fresh.Name}: {result.ErrorMessage ?? "Operation failed."}");
            Status = $"{fresh.Name}: {(result.ActualState == true ? "HDR ON" : "HDR OFF (SDR)")} verified by Windows. Click Automatic to resume automation for this display.";
        }
        catch (Exception ex)
        {
            Status = ex.Message; log.Add(ex.Message, "Error");
            NotificationRequested?.Invoke("HDR change failed", ex.Message);
        }
        finally { runtime.Wake(); }
    }
    public async Task SetModeAsync(AutomationMode mode)
    {
        await settings.UpdateAsync(config => config.Mode = mode); Draft.Mode = mode; Raise(nameof(Draft));
        if (mode != AutomationMode.Suspended) await engine.RearmAsync(await discovery.GetDisplaysAsync()); runtime.Wake(); Status = $"Mode: {mode}";
    }
    private void RefreshRunning()
    {
        Running.Clear(); foreach (var p in runtime.Latest.Processes.Where(p => p.ExecutablePath is not null).DistinctBy(p => p.ExecutablePath, StringComparer.OrdinalIgnoreCase).OrderBy(p => p.Name)) Running.Add(p);
    }
    private void Update(RuntimeSnapshot snapshot)
    {
        UpdateRunningGames(snapshot);
        var stamp = string.Join('|', settings.Current.Profiles.Select(p => $"{p.Id}:{p.LastPlayed}"));
        if (stamp != profileStamp) { profileStamp = stamp; ReloadProfiles(); }
        var id = SelectedDisplay?.Id;
        if (!Displays.SequenceEqual(snapshot.Displays)) { Displays.Clear(); foreach (var d in snapshot.Displays) Displays.Add(d); SelectedDisplay = Displays.FirstOrDefault(d => d.Id == id) ?? Displays.FirstOrDefault(d => d.IsPrimary) ?? Displays.FirstOrDefault(); Raise(nameof(GpuStatus)); }
        Set(ref automation, snapshot.State, nameof(Automation)); Set(ref rule, snapshot.Reason, nameof(ActiveRule)); Set(ref active, snapshot.ActiveApplication, nameof(ActiveApplication));
        Set(ref metrics, snapshot.MemoryMb == 0 ? "Measuring resource use…" : $"CPU {snapshot.CpuPercent:0.00}% · Working set {snapshot.MemoryMb:0.0} MB · measured every 10 seconds", nameof(Metrics));
        Set(ref browserStatus, snapshot.BrowserSessions == 0 ? "No companion connected" : $"{snapshot.BrowserSessions} companion connection(s) · states expire after 12 seconds", nameof(BrowserStatus));
    }
}
