namespace HDRPilot.Core;

public sealed record StatusNotification(string Title, string Message);

/// <summary>Tracks observed Windows state, independently of whether notifications are enabled.</summary>
public sealed class StatusNotificationTracker
{
    private Dictionary<string, bool> displays = new(StringComparer.OrdinalIgnoreCase);
    private AutomationMode? mode;
    private string? lastError;

    public StatusNotification? Observe(IReadOnlyList<DisplayInfo> current, AutomationMode currentMode, string? error = null)
    {
        var changes = new List<string>();
        var next = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var display in current.Where(d => d.HdrSupported))
        {
            if (displays.TryGetValue(display.Id, out var enabled) && enabled != display.HdrEnabled)
                changes.Add($"{display.Name}: {(display.HdrEnabled ? "HDR ON" : "HDR OFF (SDR)")}");
            next[display.Id] = display.HdrEnabled;
        }
        // A newly connected display establishes a baseline; it is not a successful HDR write.
        displays = next;
        var displayChanged = changes.Count > 0;
        if (mode is not null && mode != currentMode)
            changes.Add($"Automation: {currentMode switch
            {
                AutomationMode.Automatic => "Automatic",
                AutomationMode.AlwaysHdr => "Always HDR",
                AutomationMode.AlwaysSdr => "Always SDR",
                AutomationMode.ProfilesOnly => "Profiles only",
                AutomationMode.Suspended => "Paused",
                _ => currentMode.ToString()
            }}");
        mode = currentMode;
        var newError = !string.IsNullOrWhiteSpace(error) && error != lastError;
        if (newError) changes.Insert(0, error!);
        lastError = error;
        return changes.Count == 0 ? null : new(newError ? "HDR Pilot needs attention" : displayChanged ? "HDR status changed" : "Automation mode changed", string.Join(Environment.NewLine, changes));
    }
}
