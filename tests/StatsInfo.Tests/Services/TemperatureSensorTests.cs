using System.IO;
using LibreHardwareMonitor.Hardware;
using StatsInfo.Models;
using StatsInfo.Services;
using StatsInfo.ViewModels;
using Xunit;
using static StatsInfo.Tests.Services.SensorMappingTests;

namespace StatsInfo.Tests.Services;

public class TemperatureSensorTests
{
    private static AppSettings Enabled()
    {
        var settings = new AppSettings { HardwareSensorsEnabled = true };
        MetricSettings.Normalize(settings);
        return settings;
    }

    private static async Task<IReadOnlyList<MetricSample>> Read(HardwareSensorSource source, AppSettings settings) =>
        await source.ReadAsync(settings, source.MetricIds.ToHashSet(), default);

    [Fact]
    public async Task AutomaticTemperature_UsesPackageOrDieSensor_AndKeepsTheDefaultLabel()
    {
        var cpu = Hardware("Test CPU", HardwareType.Cpu,
            ("CPU Core #1", SensorType.Temperature, 50), ("Core (Tctl/Tdie)", SensorType.Temperature, 55));
        var gpu = Hardware("Test GPU", HardwareType.GpuNvidia, ("GPU Core", SensorType.Temperature, 62));
        using var source = new HardwareSensorSource(_ => [cpu, gpu]);
        var samples = await Read(source, Enabled());
        var cpuTemperature = samples.Single(m => m.Id == "cpu.temperature.package");
        var gpuTemperature = samples.Single(m => m.Id == "gpu.temperature.core");
        Assert.Equal(55, cpuTemperature.Value);
        Assert.Equal(62, gpuTemperature.Value);
        Assert.Null(cpuTemperature.LabelOverride);
        Assert.Null(gpuTemperature.LabelOverride);
        Assert.Equal(2, source.CpuTemperatureSensors.Count);
        Assert.Single(source.GpuTemperatureSensors);
    }

    [Fact]
    public async Task AutomaticTemperature_WithoutPackageSensor_ListsChoicesInsteadOfGuessing()
    {
        var cpu = Hardware("Test CPU", HardwareType.Cpu, ("CPU VRM", SensorType.Temperature, 48));
        using var source = new HardwareSensorSource(_ => [cpu]);
        var sample = (await Read(source, Enabled())).Single(m => m.Id == "cpu.temperature.package");
        Assert.Equal(SampleQuality.Unavailable, sample.Quality);
        Assert.Null(sample.Value);
        Assert.Contains("Available temperature sensors: CPU VRM", sample.Reason);
    }

    [Fact]
    public async Task ExplicitTemperature_ShowsTheChosenSensorsNativeName()
    {
        var cpu = Hardware("Test CPU", HardwareType.Cpu,
            ("CPU Package", SensorType.Temperature, 60), ("CPU VRM", SensorType.Temperature, 48));
        var gpu = Hardware("Test GPU", HardwareType.GpuNvidia,
            ("GPU Core", SensorType.Temperature, 62), ("GPU Hot Spot", SensorType.Temperature, 70));
        using var source = new HardwareSensorSource(_ => [cpu, gpu]);
        var settings = Enabled();
        settings.SensorSelections["cpu.temperature.package"] = cpu.Sensors[1].Identifier.ToString();
        settings.SensorSelections["gpu.temperature.core"] = gpu.Sensors[1].Identifier.ToString();
        var samples = await Read(source, settings);
        var cpuTemperature = samples.Single(m => m.Id == "cpu.temperature.package");
        var gpuTemperature = samples.Single(m => m.Id == "gpu.temperature.core");
        Assert.Equal((48, "CPU VRM"), (cpuTemperature.Value, cpuTemperature.LabelOverride));
        Assert.Equal((70, "GPU Hot Spot"), (gpuTemperature.Value, gpuTemperature.LabelOverride));
    }

    [Fact]
    public async Task ExplicitTemperature_ThatDisappeared_IsUnavailableAndDoesNotFallBackToAutomatic()
    {
        var cpu = Hardware("Test CPU", HardwareType.Cpu, ("CPU Package", SensorType.Temperature, 60));
        using var source = new HardwareSensorSource(_ => [cpu]);
        var settings = Enabled();
        settings.SensorSelections["cpu.temperature.package"] = "/Unplugged/99";
        var sample = (await Read(source, settings)).Single(m => m.Id == "cpu.temperature.package");
        Assert.Equal(SampleQuality.Unavailable, sample.Quality);
        Assert.Null(sample.Value);
        Assert.Contains("selected temperature sensor is unavailable", sample.Reason);
    }

    [Fact]
    public async Task ZeroTemperature_IsTreatedAsWarmingUp_NotAsARealReading()
    {
        var cpu = Hardware("Test CPU", HardwareType.Cpu, ("CPU Package", SensorType.Temperature, 0));
        using var source = new HardwareSensorSource(_ => [cpu]);
        var sample = (await Read(source, Enabled())).Single(m => m.Id == "cpu.temperature.package");
        Assert.Equal(SampleQuality.WarmingUp, sample.Quality);
        Assert.Null(sample.Value);
    }

    [Fact]
    public async Task CpuAccessIssue_ReplacesOnlyCpuSensorsAndLeavesGpuReadingsAlone()
    {
        var cpu = Hardware("Test CPU", HardwareType.Cpu, ("CPU Package", SensorType.Temperature, 60),
            ("CPU Package", SensorType.Power, 30));
        var gpu = Hardware("Test GPU", HardwareType.GpuNvidia, ("GPU Core", SensorType.Temperature, 62));
        var denied = HardwareSensorAccess.DeviceIssue(5);
        using var blocked = new HardwareSensorSource(_ => [cpu, gpu], () => denied);
        var samples = await Read(blocked, Enabled());
        var cpuSamples = samples.Where(m => m.Id.StartsWith("cpu.") && m.Id != "cpu.fan.rpm").ToArray();
        Assert.NotEmpty(cpuSamples);
        Assert.All(cpuSamples, m => Assert.Equal((SampleQuality.PermissionDenied, denied.Reason), (m.Quality, m.Reason)));
        Assert.Equal(62, samples.Single(m => m.Id == "gpu.temperature.core").Value);

        using var open = new HardwareSensorSource(_ => [cpu, gpu], () => null);
        Assert.Equal(60, (await Read(open, Enabled())).Single(m => m.Id == "cpu.temperature.package").Value);
    }

    [Fact]
    public async Task DisabledSensors_ExplainHowToEnableThem()
    {
        using var source = new HardwareSensorSource(_ => []);
        var samples = await source.ReadAsync(new(), source.MetricIds.ToHashSet(), default);
        Assert.Contains("Enable temperature sensors", samples.Single(m => m.Id == "cpu.temperature.package").Reason);
    }

    [Fact]
    public void Normalize_AddsTemperatureSensorSelections_WithAutomaticAsTheDefault()
    {
        var settings = new AppSettings();
        MetricSettings.Normalize(settings);
        Assert.Equal("", settings.SensorSelections["cpu.temperature.package"]);
        Assert.Equal("", settings.SensorSelections["gpu.temperature.core"]);
    }

    [Fact]
    public void DeviceIssue_SeparatesPermissionDenialFromAnUnavailableDriverDevice()
    {
        var denied = HardwareSensorAccess.DeviceIssue(5);
        Assert.Equal(SampleQuality.PermissionDenied, denied.Quality);
        Assert.Contains("Run as administrator", denied.Reason);
        var missing = HardwareSensorAccess.DeviceIssue(2);
        Assert.Equal(SampleQuality.Unavailable, missing.Quality);
        Assert.Contains("Windows error 2", missing.Reason);
    }

    [Fact]
    public void CpuIssue_ReportsAReasonOrNothing_AndNeverThrowsWhenPawnIoIsMissing()
    {
        SensorAccessIssue? issue = null;
        var failure = Record.Exception(() => issue = HardwareSensorAccess.CpuIssue());
        Assert.Null(failure);
        if (issue != null) Assert.False(string.IsNullOrWhiteSpace(issue.Reason));
    }

    [Fact]
    public void MetricItem_ShowsTheNativeSensorName_UnlessTheUserSetACustomLabel()
    {
        string directory = Directory.CreateTempSubdirectory("statsinfo-label-").FullName;
        try
        {
            var settings = new SettingsService(Path.Combine(directory, "settings.json"));
            var item = new MetricItemViewModel(MetricRegistry.All.Single(d => d.Id == "cpu.temperature.package"), settings);
            Assert.Equal("CPU temp", item.DisplayLabel);
            var changed = new List<string?>();
            item.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
            var sample = new MetricSample("cpu.temperature.package", 48, "°C", DateTimeOffset.UtcNow,
                "LibreHardwareMonitor", LabelOverride: "CPU VRM");
            item.Update(sample, new Dictionary<string, MetricSample>());
            Assert.Contains(nameof(MetricItemViewModel.DisplayLabel), changed);
            Assert.Equal("CPU VRM", item.DisplayLabel);
            item.CustomLabel = "My CPU";
            Assert.Equal("My CPU", item.DisplayLabel);
        }
        finally { Directory.Delete(directory, true); }
    }
}
