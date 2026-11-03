using StatsInfo.Models;
using StatsInfo.Services;
using Xunit;

namespace StatsInfo.Tests.Services;

public class MetricAlgorithmsTests
{
    [Fact]
    public void CounterRetention_PreservesWarmCounters_AndPrunesExitedInstances()
    {
        using var reader = new CounterReader();
        Assert.Null(reader.Read("Processor", "% Processor Time", "_Total"));
        reader.Retain("Processor", new HashSet<string> { "_Total" });
        Assert.NotNull(reader.Read("Processor", "% Processor Time", "_Total"));
        reader.Retain("Processor", new HashSet<string>());
        Assert.Null(reader.Read("Processor", "% Processor Time", "_Total"));
    }
    [Theory]
    [InlineData(2412000u, "2.4 GHz")]
    [InlineData(5180000u, "5 GHz")]
    [InlineData(5955000u, "6 GHz")]
    [InlineData(0u, null)]
    public void WifiBand_ComesFromFrequency_NotAmbiguousChannelNumber(uint frequency, string? expected) =>
        Assert.Equal(expected, WifiReader.FrequencyBand(frequency));
    [Fact]
    public void NetworkDelta_UsesElapsedSeconds_AndResetsOnAdapterResetOrResume()
    {
        var delta = new CounterDelta();
        Assert.Null(delta.Update("a", 100, 100, 10));
        var rate = delta.Update("a", 1000100, 500100, 12)!.Value;
        Assert.Equal(4, rate.DownMbps);
        Assert.Equal(2, rate.UpMbps);
        Assert.Null(delta.Update("a", 0, 0, 13));
        Assert.Null(delta.Update("b", 50000000, 100, 14));
        Assert.Null(delta.Update("b", 50001000, 100, 100));
        Assert.Null(delta.Update("b", 50001000, 100, 100));
    }

    [Fact]
    public void ProbeWindow_LossCountsTimeouts_AndJitterDoesNotBridgeGaps()
    {
        var stats = LatencyMetricsSource.Calculate([10, 20, null, 100, 104]);
        Assert.Equal(20, stats.Loss);
        Assert.Equal(7, stats.Jitter);
        Assert.Null(LatencyMetricsSource.Calculate([10, null, 20]).Jitter);
    }

    [Fact]
    public void History_BoundsSamplesAndTime_AndRetainsQualityGaps()
    {
        var history = new MetricHistory();
        var sample = MetricSample.Number("cpu.usage.total", 42, "test");
        for (int i = 0; i < 10000; i++) history.Add(sample with { SampledAt = sample.SampledAt.AddMilliseconds(i) });
        Assert.Equal(MetricHistory.MaxSamplesPerMetric, history.Get(sample.Id).Length);
        history.Add(sample with
        {
            SampledAt = sample.SampledAt.AddMinutes(5),
            Value = null,
            Quality = SampleQuality.Unavailable
        });
        Assert.Single(history.Get(sample.Id));
        Assert.False(history.Get(sample.Id)[0].IsNumeric);
        history.Clear();
        Assert.Empty(history.GetAll());
    }

    [Fact]
    public void Alerts_RequireSustainedValidSamples_ClearAndRespectCooldown()
    {
        var engine = new AlertEngine();
        var rule = new AlertRule
        {
            Enabled = true,
            Warning = 80,
            Critical = 95,
            Clear = 70,
            SustainedSeconds = 10,
            CooldownSeconds = 60
        };
        var sample = MetricSample.Number("cpu.usage.total", 90, "test");
        Assert.Null(engine.Evaluate(sample, rule));
        Assert.Null(engine.Evaluate(sample with { SampledAt = sample.SampledAt.AddSeconds(9) }, rule));
        Assert.Equal("Warning", engine.Evaluate(sample with { SampledAt = sample.SampledAt.AddSeconds(10) }, rule)!.Severity);
        Assert.Null(engine.Evaluate(sample with { SampledAt = sample.SampledAt.AddSeconds(80) }, rule));
        Assert.Null(engine.Evaluate(sample with { Value = 60, SampledAt = sample.SampledAt.AddSeconds(81) }, rule));
        Assert.Null(engine.Evaluate(sample with { SampledAt = sample.SampledAt.AddSeconds(82) }, rule));
        Assert.NotNull(engine.Evaluate(sample with { SampledAt = sample.SampledAt.AddSeconds(92) }, rule));
    }

    [Fact]
    public void Alerts_ErrorInterruptsSustain_AndQuietHoursCrossMidnight()
    {
        var engine = new AlertEngine();
        var rule = new AlertRule { Enabled = true, SustainedSeconds = 10 };
        var sample = MetricSample.Number("cpu.usage.total", 99, "test");
        engine.Evaluate(sample, rule);
        engine.Evaluate(sample with { Quality = SampleQuality.Error, SampledAt = sample.SampledAt.AddSeconds(8) }, rule);
        Assert.Null(engine.Evaluate(sample with { SampledAt = sample.SampledAt.AddSeconds(11) }, rule));
        Assert.True(AlertEngine.IsQuietHour(23, 22, 7));
        Assert.True(AlertEngine.IsQuietHour(5, 22, 7));
        Assert.False(AlertEngine.IsQuietHour(12, 22, 7));
    }
}
