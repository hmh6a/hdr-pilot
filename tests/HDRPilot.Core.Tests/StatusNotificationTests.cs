using HDRPilot.Core;

namespace HDRPilot.Core.Tests;

public sealed class StatusNotificationTests
{
    private static DisplayInfo Display(string id, bool enabled) => new(id, id == "primary" ? "MSI OLED" : "Samsung", id, 2560, 1440, 240, id == "primary", true, enabled, true, "GPU");

    [Fact]
    public void StartupAndUnchangedSamplesAreSilent()
    {
        var tracker = new StatusNotificationTracker();
        Assert.Null(tracker.Observe([Display("primary", true), Display("secondary", false)], AutomationMode.Automatic));
        Assert.Null(tracker.Observe([Display("primary", true), Display("secondary", false)], AutomationMode.Automatic));
    }

    [Fact]
    public void SecondaryChangesAreReportedWhilePrimaryRemainsHdr()
    {
        var tracker = new StatusNotificationTracker();
        tracker.Observe([Display("primary", true), Display("secondary", false)], AutomationMode.Automatic);
        var on = tracker.Observe([Display("primary", true), Display("secondary", true)], AutomationMode.Automatic);
        Assert.Equal("Samsung: HDR ON", on?.Message);
        Assert.Null(tracker.Observe([Display("primary", true), Display("secondary", true)], AutomationMode.Automatic));
        var off = tracker.Observe([Display("primary", true), Display("secondary", false)], AutomationMode.Automatic);
        Assert.Equal("Samsung: HDR OFF (SDR)", off?.Message);
    }

    [Fact]
    public void ReorderingAndRenamingDoNotImplyHdrChanges()
    {
        var tracker = new StatusNotificationTracker();
        tracker.Observe([Display("primary", true), Display("secondary", false)], AutomationMode.Automatic);
        Assert.Null(tracker.Observe([Display("secondary", false), Display("PRIMARY", true) with { Name = "Renamed", DeviceName = "DISPLAY2" }], AutomationMode.Automatic));
    }

    [Fact]
    public void HotplugAndReconnectEstablishFreshBaselines()
    {
        var tracker = new StatusNotificationTracker();
        tracker.Observe([Display("primary", true)], AutomationMode.Automatic);
        Assert.Null(tracker.Observe([Display("primary", true), Display("secondary", false)], AutomationMode.Automatic));
        Assert.Null(tracker.Observe([Display("primary", true)], AutomationMode.Automatic));
        Assert.Null(tracker.Observe([Display("primary", true), Display("secondary", true)], AutomationMode.Automatic));
    }

    [Fact]
    public void SimultaneousDisplayAndModeChangesUseOneNotification()
    {
        var tracker = new StatusNotificationTracker();
        tracker.Observe([Display("primary", false), Display("secondary", false)], AutomationMode.Automatic);
        var message = tracker.Observe([Display("primary", true), Display("secondary", true)], AutomationMode.AlwaysHdr);
        Assert.Equal("HDR status changed", message?.Title);
        Assert.Equal($"MSI OLED: HDR ON{Environment.NewLine}Samsung: HDR ON{Environment.NewLine}Automation: Always HDR", message?.Message);
    }

    [Fact]
    public void PausingWithNoHdrChangeStillNotifies()
    {
        var tracker = new StatusNotificationTracker();
        tracker.Observe([Display("primary", true)], AutomationMode.Automatic);
        var message = tracker.Observe([Display("primary", true)], AutomationMode.Suspended);
        Assert.Equal("Automation mode changed", message?.Title);
        Assert.Equal("Automation: Paused", message?.Message);
        Assert.Null(tracker.Observe([Display("primary", true)], AutomationMode.Suspended));
    }

    [Fact]
    public void DiscardedNotificationsAreNotReplayedWhenEnabled()
    {
        var tracker = new StatusNotificationTracker();
        tracker.Observe([Display("secondary", false)], AutomationMode.Automatic);
        // Tray may discard this result while the preference is disabled.
        tracker.Observe([Display("secondary", true)], AutomationMode.Automatic);
        Assert.Null(tracker.Observe([Display("secondary", true)], AutomationMode.Automatic));
    }

    [Fact]
    public void ErrorsAreDeduplicatedAndCanBeReportedAfterRecovery()
    {
        var tracker = new StatusNotificationTracker();
        var display = Display("secondary", false);
        var message = tracker.Observe([display], AutomationMode.Automatic, "Windows could not verify HDR.");
        Assert.Equal("HDR Pilot needs attention", message?.Title);
        Assert.DoesNotContain("HDR ON", message!.Message);
        Assert.Null(tracker.Observe([display], AutomationMode.Automatic, "Windows could not verify HDR."));
        Assert.Null(tracker.Observe([display], AutomationMode.Automatic));
        Assert.NotNull(tracker.Observe([display], AutomationMode.Automatic, "Windows could not verify HDR."));
    }
}
