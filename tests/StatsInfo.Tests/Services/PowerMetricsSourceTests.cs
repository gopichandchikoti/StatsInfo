using StatsInfo.Models;
using StatsInfo.Services;
using Xunit;

namespace StatsInfo.Tests.Services;

public class PowerMetricsSourceTests
{
    private static readonly string[] AllIds =
        ["system.uptime", "system.power_mode", "system.power.total", "battery.percent", "battery.discharge.rate", "battery.health"];

    [Fact]
    public async Task RequestingBatteryMetrics_ReturnsEachMetricOnce_WithAReasonWheneverItIsNotValid()
    {
        using var source = new PowerMetricsSource();
        Assert.Equal("System/Cooling", source.Category);
        Assert.Null(Record.Exception(() => PowerMetricsSource.IsOnBattery()));
        var samples = await source.ReadAsync(new(), source.MetricIds.ToHashSet(), default);
        foreach (string id in AllIds) Assert.Single(samples, m => m.Id == id);
        Assert.True(samples.Single(m => m.Id == "system.uptime").IsNumeric);
        Assert.All(samples, m => Assert.True(m.Quality == SampleQuality.Valid || !string.IsNullOrWhiteSpace(m.Reason),
            $"{m.Id} must explain why it has no reading."));
    }

    [Fact]
    public async Task WhenBatteryAndPowerModeAreNotRequested_TheyAreNotQueried()
    {
        using var source = new PowerMetricsSource();
        var samples = await source.ReadAsync(new(), new HashSet<string>(), default);
        Assert.DoesNotContain(samples, m => m.Id is "system.power_mode" or "battery.discharge.rate" or "battery.health");
        Assert.Contains(samples, m => m.Id == "system.uptime");
    }
}
