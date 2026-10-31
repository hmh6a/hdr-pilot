using HDRPilot.Windows;

namespace HDRPilot.Windows.Tests;
public sealed class HardwareFactAttribute : FactAttribute
{
    public HardwareFactAttribute() { if (Environment.GetEnvironmentVariable("HDRPILOT_HW_READONLY") != "1") Skip = "Opt-in interactive Windows hardware test. Set HDRPILOT_HW_READONLY=1."; }
}
public sealed class HardwareTests
{
    [HardwareFact]
    public async Task Read_real_active_display_configuration()
    {
        using var service = new WindowsDisplayService(); var displays = await service.GetDisplaysAsync();
        Assert.NotEmpty(displays); Assert.Equal(displays.Count, displays.Select(d => d.Id).Distinct().Count());
        Assert.All(displays, d => { Assert.NotEmpty(d.Id); Assert.True(d.Width > 0); Assert.True(d.Height > 0); Assert.True(d.RefreshRate > 0); });
    }
}
