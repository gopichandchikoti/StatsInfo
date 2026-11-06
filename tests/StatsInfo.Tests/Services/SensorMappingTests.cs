using LibreHardwareMonitor.Hardware;
using Moq;
using StatsInfo.Models;
using StatsInfo.Services;
using Xunit;

namespace StatsInfo.Tests.Services;

public class SensorMappingTests
{
    [Fact]
    public async Task SensorOptIn_MappingUsesExactSensorSemantics_AndNeverInventsMissingReadings()
    {
        var cpu = Hardware("Test CPU", HardwareType.Cpu,
            ("CPU Package", SensorType.Temperature, 60), ("CPU Core #1", SensorType.Temperature, 58),
            ("CPU Package", SensorType.Power, 30), ("CPU Core #1 Effective", SensorType.Clock, 3200),
            ("CPU", SensorType.Voltage, 1.1f));
        var gpu = Hardware("Test GPU", HardwareType.GpuNvidia,
            ("GPU Core", SensorType.Load, 40), ("GPU Core", SensorType.Temperature, 62),
            ("GPU Hot Spot", SensorType.Temperature, 70), ("GPU Board", SensorType.Power, 90),
            ("GPU Core", SensorType.Clock, 1500), ("GPU Memory", SensorType.Clock, 4000),
            ("GPU Fan", SensorType.Fan, 1500), ("GPU Fan", SensorType.Control, 40),
            ("GPU Memory Used", SensorType.SmallData, 2048), ("GPU Memory Total", SensorType.SmallData, 8192));
        var drive = Hardware("Test Drive", HardwareType.Storage,
            ("Composite Temperature", SensorType.Temperature, 40), ("Percentage Used", SensorType.Level, 5));
        var board = Hardware("Test Board", HardwareType.SuperIO,
            ("CPU Fan", SensorType.Fan, 1200), ("Board", SensorType.Temperature, 38));
        using var source = new HardwareSensorSource(_ => [cpu, gpu, drive, board]);
        var s = new AppSettings();
        MetricSettings.Normalize(s);
        var disabled = await source.ReadAsync(s, source.MetricIds.ToHashSet(), default);
        Assert.All(disabled, sample => Assert.Equal(SampleQuality.Unavailable, sample.Quality));
        s.HardwareSensorsEnabled = true;
        s.SensorSelections["cpu.fan.rpm"] = board.Sensors[0].Identifier.ToString();
        s.SensorSelections["system.motherboard.temperature"] = board.Sensors[1].Identifier.ToString();
        var samples = await source.ReadAsync(s, source.MetricIds.ToHashSet(), default);
        Assert.Equal(60, samples.Single(m => m.Id == "cpu.temperature.package").Value);
        Assert.Equal(3.2, samples.Single(m => m.Id == "cpu.clock.effective").Value);
        Assert.Equal(2, samples.Single(m => m.Id == "gpu.vram.used").Value);
        Assert.Equal(8, samples.Single(m => m.Id == "gpu.vram.total").Value);
        Assert.Equal(1200, samples.Single(m => m.Id == "cpu.fan.rpm").Value);
        Assert.Single(samples.Single(m => m.Id == "system.fans").Values!);
        Assert.Contains("Test GPU", source.GpuNames);
        Assert.Contains("Test Drive", source.DriveNames);
        Assert.NotEmpty(source.SensorInventory);
        Assert.Equal(SampleQuality.Unavailable, samples.Single(m => m.Id == "gpu.throttling").Quality);
    }

    [Fact]
    public async Task MissingPackageOrEffectiveClock_DoesNotSubstituteCoreOrRequestedClock()
    {
        var cpu = Hardware("Test CPU", HardwareType.Cpu,
            ("CPU Core #1", SensorType.Temperature, 70), ("CPU Core #1", SensorType.Clock, 5000));
        using var source = new HardwareSensorSource(_ => [cpu]);
        var samples = await source.ReadAsync(new() { HardwareSensorsEnabled = true }, source.MetricIds.ToHashSet(), default);
        foreach (string id in new[] { "cpu.temperature.package", "cpu.clock.effective", "gpu.vram.used", "gpu.active_adapter" })
            Assert.Equal(SampleQuality.Unavailable, samples.Single(m => m.Id == id).Quality);
    }

    internal static IHardware Hardware(string name, HardwareType type, params (string Name, SensorType Type, float Value)[] values)
    {
        var hardware = new Mock<IHardware>();
        hardware.SetupGet(h => h.Name).Returns(name);
        hardware.SetupGet(h => h.HardwareType).Returns(type);
        hardware.SetupGet(h => h.SubHardware).Returns([]);
        hardware.SetupGet(h => h.Identifier).Returns(new Identifier(name.Replace(" ", "")));
        var sensors = values.Select((v, i) =>
        {
            var sensor = new Mock<ISensor>();
            sensor.SetupGet(s => s.Name).Returns(v.Name);
            sensor.SetupGet(s => s.SensorType).Returns(v.Type);
            sensor.SetupGet(s => s.Value).Returns(v.Value);
            sensor.SetupGet(s => s.Hardware).Returns(hardware.Object);
            sensor.SetupGet(s => s.Identifier).Returns(new Identifier(name.Replace(" ", ""), i.ToString()));
            return sensor.Object;
        }).ToArray();
        hardware.SetupGet(h => h.Sensors).Returns(sensors);
        return hardware.Object;
    }

    [Fact]
    public async Task GpuCounters_SumProcessesPerEngine_ThenTakeBusiestEngine_NotEngineSum()
    {
        var counters = new Mock<CounterReader>();
        string prefix = $"pid_{Environment.ProcessId}_luid_0x00000000_0x00000001_phys_0";
        string[] engines = [$"{prefix}_eng_0_engtype_3D", $"{prefix}_eng_1_engtype_Copy"];
        counters.Setup(c => c.Instances("GPU Engine")).Returns(engines);
        counters.Setup(c => c.Instances("GPU Adapter Memory")).Returns(["luid_0x00000000_0x00000001_phys_0"]);
        counters.Setup(c => c.Read("GPU Engine", "Utilization Percentage", engines[0])).Returns(70);
        counters.Setup(c => c.Read("GPU Engine", "Utilization Percentage", engines[1])).Returns(40);
        counters.Setup(c => c.Read("GPU Adapter Memory", "Dedicated Usage", It.IsAny<string>())).Returns(1073741824);
        using var source = new GpuEngineSource(counters.Object);
        var samples = await source.ReadAsync(new(), source.MetricIds.ToHashSet(), default);
        Assert.Equal(70, samples.Single(m => m.Id == "gpu.usage.total").Value);
        Assert.Equal(1, samples.Single(m => m.Id == "gpu.vram.used").Value);
        Assert.Equal(70, samples.Single(m => m.Id == "process.top_gpu").Value);
        Assert.Equal(2, samples.Single(m => m.Id == "gpu.engines").Values!.Count);
        counters.Setup(c => c.Instances("GPU Engine")).Returns([]);
        using var empty = new GpuEngineSource(counters.Object);
        Assert.All(await empty.ReadAsync(new(), empty.MetricIds.ToHashSet(), default), m => Assert.Equal(SampleQuality.Unavailable, m.Quality));
        counters.Setup(c => c.Instances("GPU Engine")).Returns(Enumerable.Repeat("x", 2049).ToArray());
        using var overBudget = new GpuEngineSource(counters.Object);
        Assert.All(await overBudget.ReadAsync(new(), overBudget.MetricIds.ToHashSet(), default), m => Assert.Contains("budget", m.Reason));
    }
}
