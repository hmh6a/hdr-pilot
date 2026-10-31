using System.Text.Json;
using System.Windows;
using HDRPilot.Core;
using HDRPilot.Infrastructure;

namespace HDRPilot.App;
public partial class ProfileWindow : Window
{
    private sealed record DisplayOption(string? Id, string Name);
    public GameProfile Profile { get; }
    public ProfileWindow(GameProfile profile, IEnumerable<DisplayInfo> displays)
    {
        InitializeComponent();
        Profile = JsonSerializer.Deserialize<GameProfile>(JsonSerializer.Serialize(profile, JsonSettingsService.JsonOptions), JsonSettingsService.JsonOptions)!;
        ProfileName.Text = Profile.Name; Executable.Text = Profile.ExecutablePath; Policy.ItemsSource = Enum.GetValues<HdrMode>(); Policy.SelectedItem = Profile.Mode;
        Activation.Text = Profile.ActivationDelayMs.ToString(); Deactivation.Text = Profile.DeactivationDelayMs.ToString(); Priority.Text = Profile.Priority.ToString(); Enabled.IsChecked = Profile.Enabled; Minimized.IsChecked = Profile.IgnoreWhenMinimized;
        var options = new List<DisplayOption> { new(null, "Follow application window") }; options.AddRange(displays.Select(d => new DisplayOption(d.Id, d.Label)));
        if (Profile.PreferredDisplayId is { } id && !options.Any(d => d.Id == id)) options.Add(new(id, "Previously selected display (disconnected)"));
        Target.ItemsSource = options; Target.SelectedItem = options.FirstOrDefault(d => d.Id == Profile.PreferredDisplayId) ?? options[0];
    }
    private void Save(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(Activation.Text, out var activate) || !int.TryParse(Deactivation.Text, out var deactivate) || !int.TryParse(Priority.Text, out var priority)) { ErrorText.Text = "Enter whole numbers for timing and priority."; return; }
        Profile.Name = ProfileName.Text.Trim(); Profile.ExecutablePath = Executable.Text.Trim(); Profile.Mode = (HdrMode)Policy.SelectedItem;
        Profile.ActivationDelayMs = activate; Profile.DeactivationDelayMs = deactivate; Profile.Priority = priority; Profile.Enabled = Enabled.IsChecked == true; Profile.IgnoreWhenMinimized = Minimized.IsChecked == true;
        Profile.PreferredDisplayId = (Target.SelectedItem as DisplayOption)?.Id;
        try { SettingsValidation.Validate(new() { Profiles = [Profile] }); } catch (Exception ex) { ErrorText.Text = ex.Message; return; }
        if (Profile.Enabled && !System.IO.File.Exists(Profile.ExecutablePath)) { ErrorText.Text = "Executable not found. Select an existing file or disable the profile."; return; }
        DialogResult = true;
    }
}
