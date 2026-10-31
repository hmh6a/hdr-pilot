using System.IO;
using System.Windows;
using System.Windows.Controls;
using HDRPilot.Core;
using HDRPilot.GameDetection;

namespace HDRPilot.App;

public partial class GameSetupWindow : Window
{
    private sealed record DisplayOption(string? Id, string Name);
    private readonly string gameName, source, initialDirectory;
    private readonly IReadOnlyList<DisplayInfo> displays;
    private readonly IReadOnlyList<GameProfile> existing;
    private readonly Func<IReadOnlyList<GameExecutableChoice>> discover;
    private readonly Dictionary<string, GameProfile> drafts = new(StringComparer.OrdinalIgnoreCase);
    public GameProfile? Profile { get; private set; }
    public bool ShouldResume => ResumeAutomation.IsChecked == true;

    public GameSetupWindow(string name, string source, string directory, IReadOnlyList<DisplayInfo> displays,
        IReadOnlyList<GameProfile> existing, Func<IReadOnlyList<GameExecutableChoice>> discover, string? preferredPath = null)
    {
        gameName = name; this.source = source; initialDirectory = directory;
        this.displays = displays; this.existing = existing; this.discover = discover;
        InitializeComponent(); GameTitle.Text = name; LoadProfile();
        ReloadChoices(preferredPath);
    }

    private void ReloadChoices(string? preferredPath = null)
    {
        var choices = discover(); ExecutableChoice.ItemsSource = choices;
        ExecutableChoice.SelectedItem = choices.FirstOrDefault(c => c.Path.Equals(preferredPath, StringComparison.OrdinalIgnoreCase)) ?? GameSetup.Suggested(choices);
        ChoiceHint.Text = choices.Count == 0 ? "شغّل اللعبة ثم اضغط تحديث الكشف، أو اختر ملفها يدويًا من الخيارات الإضافية."
            : ExecutableChoice.SelectedItem is not null ? "تم تحديد الملف. راجع الشاشة ثم اضغط حفظ."
            : "يوجد أكثر من ملف. الأسهل: شغّل اللعبة ثم اضغط تحديث الكشف ليظهر ملفها المفتوح أولًا، أو اختره من القائمة.";
    }

    private void ChoiceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SaveButton is null) return;
        ErrorText.Text = "";
        if (ExecutableChoice.SelectedItem is not GameExecutableChoice choice)
        { Profile = null; LoadProfile(); SaveButton.IsEnabled = AdvancedButton.IsEnabled = false; return; }
        Profile = drafts.TryGetValue(choice.Path, out var edited) ? edited : GameSetup.CreateProfile(gameName, choice.Path, source, existing);
        LoadProfile(); SaveButton.IsEnabled = AdvancedButton.IsEnabled = true;
    }

    private void LoadProfile()
    {
        var preview = Profile ?? new GameProfile();
        Target.IsEnabled = Profile is not null;
        ProfileEnabled.IsChecked = preview.Enabled;
        var options = new List<DisplayOption> { new(null, "تلقائي — الشاشة التي تظهر عليها اللعبة") };
        options.AddRange(displays.Where(d => d.HdrSupported).Select(d => new DisplayOption(d.Id, d.Name + (d.IsPrimary ? " — الرئيسية" : ""))));
        if (preview.PreferredDisplayId is { } id && !options.Any(d => d.Id == id)) options.Add(new(id, "الشاشة المحفوظة غير متصلة حاليًا"));
        Target.ItemsSource = options; Target.SelectedItem = options.FirstOrDefault(d => d.Id == preview.PreferredDisplayId) ?? options[0];
        Behavior.Text = preview.Mode switch
        {
            HdrMode.WhileRunning => preview.IgnoreWhenMinimized ? "يشغّل HDR عندما تكون نافذة اللعبة ظاهرة. يعيد الحالة السابقة بعد إغلاقها أو تصغيرها، ما دام التحكم بيد HDR Pilot." : "يشغّل HDR أثناء تشغيل اللعبة. يعيد الحالة السابقة بعد انتهائها، ما دام التحكم بيد HDR Pilot.",
            HdrMode.WhileForeground => "يشغّل HDR عندما تكون اللعبة هي النافذة النشطة أمامك.",
            HdrMode.Automatic or HdrMode.FullscreenOnly => "يشغّل HDR عندما تكون اللعبة نشطة وبوضع ملء الشاشة.",
            HdrMode.NeverModify => "يحافظ على الحالة الحالية للشاشة أثناء انطباق هذا البروفايل.",
            HdrMode.ForceSdr => "يلغي HDR الذي فعّله HDR Pilot فقط. لا يطفئ HDR المفعّل مسبقًا.",
            _ => "يطلب تشغيل HDR عند ظهور اللعبة. الإعدادات التفصيلية موجودة في الخيارات الإضافية."
        };
    }

    private void RefreshClicked(object sender, RoutedEventArgs e) => ReloadChoices();
    private void BrowseClicked(object sender, RoutedEventArgs e)
    {
        var picker = new Microsoft.Win32.OpenFileDialog { Filter = "Game executable (*.exe)|*.exe", CheckFileExists = true, Title = "اختر ملف تشغيل اللعبة", InitialDirectory = Directory.Exists(initialDirectory) ? initialDirectory : "" };
        if (picker.ShowDialog(this) != true) return;
        var choices = (ExecutableChoice.ItemsSource as IEnumerable<GameExecutableChoice> ?? []).ToList();
        var choice = choices.FirstOrDefault(c => c.Path.Equals(picker.FileName, StringComparison.OrdinalIgnoreCase));
        if (choice is null) { choice = new(picker.FileName, Path.GetFileName(picker.FileName), false); choices.Add(choice); }
        ExecutableChoice.ItemsSource = choices; ExecutableChoice.SelectedItem = choice;
        ChoiceHint.Text = "تم اختيار الملف يدويًا. راجع الشاشة ثم احفظ.";
    }

    private void AdvancedClicked(object sender, RoutedEventArgs e)
    {
        if (Profile is null) return;
        Profile.PreferredDisplayId = (Target.SelectedItem as DisplayOption)?.Id;
        Profile.Enabled = ProfileEnabled.IsChecked == true;
        var editor = new ProfileWindow(Profile, displays) { Owner = this };
        if (editor.ShowDialog() != true) return;
        // Keep executable selection and the edited profile in sync, including a manually changed path.
        Profile = editor.Profile;
        var choice = new GameExecutableChoice(Profile.ExecutablePath, Path.GetFileName(Profile.ExecutablePath), false);
        drafts[choice.Path] = Profile;
        var choices = (ExecutableChoice.ItemsSource as IEnumerable<GameExecutableChoice> ?? []).Where(c => !c.Path.Equals(choice.Path, StringComparison.OrdinalIgnoreCase)).Append(choice).ToArray();
        ExecutableChoice.ItemsSource = choices; ExecutableChoice.SelectedItem = choice;
    }

    private void SaveClicked(object sender, RoutedEventArgs e)
    {
        if (Profile is null) return;
        if (!File.Exists(Profile.ExecutablePath)) { ErrorText.Text = "ملف اللعبة غير موجود. شغّل اللعبة وحدّث الكشف، أو اختر ملفًا آخر."; return; }
        Profile.PreferredDisplayId = (Target.SelectedItem as DisplayOption)?.Id;
        Profile.Enabled = ProfileEnabled.IsChecked == true;
        try { SettingsValidation.Validate(new() { Profiles = [Profile] }); }
        catch (Exception ex) { ErrorText.Text = ex.Message; return; }
        DialogResult = true;
    }
}
