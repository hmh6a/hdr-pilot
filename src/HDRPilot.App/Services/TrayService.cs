using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using HDRPilot.Core;
using HDRPilot.App.ViewModels;
using Forms = System.Windows.Forms;

namespace HDRPilot.App.Services;
public sealed class TrayService : INotificationService, IDisposable
{
    private readonly Forms.NotifyIcon icon;
    private readonly ISettingsService settings;
    private readonly MainViewModel vm;
    private readonly Forms.ToolStripMenuItem status = new("Starting…") { Enabled = false };
    private readonly Forms.ToolStripMenuItem startup;
    private string lastState = "";
    private readonly StatusNotificationTracker notifications = new();
    private readonly Dictionary<string, Icon> icons = [];
    public TrayService(MainViewModel vm, ISettingsService settings, IStartupManager startupManager, Action open, Action showSettings, Action exit)
    {
        this.vm = vm; this.settings = settings;
        foreach (var (name, color) in new[] { ("SDR", Color.SlateGray), ("HDR", Color.MediumAquamarine), ("Automatic", Color.CornflowerBlue), ("Paused", Color.Goldenrod), ("Error", Color.IndianRed) }) icons[name] = MakeIcon(color);
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open HDR Pilot", null, (_, _) => open()); menu.Items.Add(new Forms.ToolStripSeparator()); menu.Items.Add(status);
        menu.Items.Add("Enable HDR on selected display", null, async (_, _) => await Run(() => vm.ManualAsync(vm.SelectedDisplay, true)));
        menu.Items.Add("Disable HDR on selected display", null, async (_, _) => await Run(() => vm.ManualAsync(vm.SelectedDisplay, false)));
        menu.Items.Add("Automatic mode", null, (_, _) => vm.AutomaticCommand.Execute(null));
        menu.Items.Add("Pause automation", null, (_, _) => vm.PauseCommand.Execute(null)); menu.Items.Add(new Forms.ToolStripSeparator());
        startup = new Forms.ToolStripMenuItem("Start with Windows") { Checked = startupManager.IsEnabled };
        startup.Click += (_, _) => { try { startupManager.SetEnabled(!startupManager.IsEnabled); startup.Checked = startupManager.IsEnabled; vm.StartWithWindows = startup.Checked; } catch (Exception ex) { vm.Status = ex.Message; } };
        menu.Items.Add(startup); menu.Items.Add("Settings", null, (_, _) => showSettings()); menu.Items.Add("Exit", null, (_, _) => exit());
        icon = new() { Text = "HDR Pilot", Icon = icons["Automatic"], ContextMenuStrip = menu, Visible = true };
        icon.DoubleClick += (_, _) => open();
        vm.NotificationRequested += Notify;
    }
    private async Task Run(Func<Task> action) { try { await action(); } catch (Exception ex) { vm.Status = ex.Message; } }
    public void Update(RuntimeSnapshot snapshot)
    {
        var mode = settings.Current.Mode;
        var state = snapshot.State == "Error" ? "Error" : mode == AutomationMode.Suspended ? "Paused" : snapshot.Displays.Any(d => d.HdrEnabled) ? "HDR" : mode is AutomationMode.Automatic or AutomationMode.ProfilesOnly ? "Automatic" : "SDR";
        status.Text = $"HDR: {(snapshot.Displays.Any(d => d.HdrEnabled) ? "Enabled" : "Disabled")} · Mode: {mode}";
        icon.Text = $"HDR Pilot · {state}";
        if (state != lastState) { icon.Icon = icons[state]; lastState = state; }
        if (notifications.Observe(snapshot.Displays, mode, snapshot.State == "Error" ? snapshot.Reason : null) is { } notification)
            Notify(notification.Title, notification.Message);
    }
    public void Notify(string title, string message)
    {
        if (settings.Current.Notifications)
            icon.ShowBalloonTip(5000, title.Length > 63 ? title[..62] + "…" : title,
                message.Length > 255 ? message[..254] + "…" : message, Forms.ToolTipIcon.Info);
    }
    private static Icon MakeIcon(Color color)
    {
        using var bitmap = new Bitmap(32, 32); using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(color); graphics.FillEllipse(brush, 1, 1, 30, 30);
        using var pen = new Pen(Color.FromArgb(12, 25, 24), 4); graphics.DrawLines(pen, [new(11, 24), new(11, 8), new(21, 8), new(23, 12), new(21, 16), new(11, 16)]);
        var handle = bitmap.GetHicon(); try { using var temporary = Icon.FromHandle(handle); return (Icon)temporary.Clone(); } finally { DestroyIcon(handle); }
    }
    [DllImport("user32.dll")] private static extern bool DestroyIcon(nint icon);
    public void Dispose() { vm.NotificationRequested -= Notify; icon.Visible = false; icon.ContextMenuStrip?.Dispose(); icon.Dispose(); foreach (var value in icons.Values) value.Dispose(); }
}
