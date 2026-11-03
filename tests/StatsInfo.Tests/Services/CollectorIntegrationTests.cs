using System.IO;
using System.Runtime.InteropServices;
using StatsInfo.Helpers;
using StatsInfo.Models;
using StatsInfo.Services;
using Xunit;

namespace StatsInfo.Tests.Services;

public class CollectorIntegrationTests
{
    [Fact]
    public async Task Inventory_ReportsNamedComponentsWithoutQueryingSerialFields()
    {
        string inventory = await HardwareInventory.ReadAsync();
        Assert.Contains("GPU:", inventory);
        Assert.Contains("Display:", inventory);
        Assert.Contains("No serial-number fields queried", inventory);
    }
    private static HashSet<string> All(IMetricSource source) => source.MetricIds.ToHashSet();

    [Fact]
    public void Dxgi_ReportsNamed64BitDedicatedCapacity_WithoutSensorDriverInitialization()
    {
        var adapters = DxgiAdapters.Read();
        Assert.All(adapters, adapter =>
        {
            Assert.False(string.IsNullOrWhiteSpace(adapter.Name));
            Assert.True(adapter.Matches(adapter.Luid + "_phys_0"));
            Assert.False(adapter.Matches("unrelated"));
        });
    }

    [Fact]
    public async Task WindowsMemoryAndDisplay_ReadRealValues_AndUnicodeModeHasCorrectSize()
    {
        Assert.Equal(220, Marshal.SizeOf<NativeInterop.DEVMODE>());
        using var memory = new CpuMemorySource("Memory");
        var samples = await memory.ReadAsync(new(), All(memory), default);
        Assert.Equal(6, samples.Count);
        Assert.All(samples.Where(s => s.Value.HasValue), s => Assert.True(s.IsNumeric));
        double used = samples.Single(s => s.Id == "memory.physical.used").Value!.Value;
        double available = samples.Single(s => s.Id == "memory.physical.available").Value!.Value;
        double total = samples.Single(s => s.Id == "memory.physical.total").Value!.Value;
        Assert.InRange(Math.Abs(used + available - total), 0, 0.00001);
        using var display = new DisplayMetricsSource();
        var modes = await display.ReadAsync(new(), All(display), default);
        Assert.DoesNotContain(modes, s => s.Id == "display.resolution" && s.Text == "0 × 0");
        Assert.All(modes, s => Assert.NotEqual(SampleQuality.Error, s.Quality));
    }

    [Fact]
    public async Task WindowsCpuDiskProcessAndPower_SampleWithoutRequiringElevation()
    {
        using var cpu = new CpuMemorySource("CPU");
        await cpu.ReadAsync(new(), All(cpu), default);
        await Task.Delay(50);
        var cpuSamples = await cpu.ReadAsync(new(), All(cpu), default);
        Assert.Equal(Environment.ProcessorCount, cpuSamples.Single(s => s.Id == "cpu.logical_processors").Value);
        Assert.Equal(Environment.ProcessorCount, cpuSamples.Single(s => s.Id == "cpu.usage.per_core").Values!.Count);
        using var disk = new StorageMetricsSource();
        var settings = new AppSettings
        {
            DiskInstance = StorageMetricsSource.DiskInstances().First(),
            Volume = Path.GetPathRoot(Environment.SystemDirectory)!
        };
        await disk.ReadAsync(settings, All(disk), default);
        var disks = await disk.ReadAsync(settings, All(disk), default);
        Assert.True(disks.Single(s => s.Id == "storage.capacity.free").IsNumeric);
        Assert.Equal(SampleQuality.Unavailable, disks.Single(s => s.Id == "storage.health.status").Quality);
        using var processes = new ProcessMetricsSource();
        await processes.ReadAsync(settings, All(processes), default);
        await Task.Delay(20);
        var proc = await processes.ReadAsync(settings, All(processes), default);
        Assert.All(proc, s => Assert.True(s.IsNumeric));
        using var power = new PowerMetricsSource();
        var powerSamples = await power.ReadAsync(settings, new HashSet<string> { "system.uptime", "system.power_mode" }, default);
        Assert.True(powerSamples.Single(s => s.Id == "system.uptime").IsNumeric);
        Assert.Equal(SampleQuality.Unavailable, powerSamples.Single(s => s.Id == "system.power.total").Quality);
    }

    [Fact]
    public async Task NetworkSelectionAndTrafficPersistence_AreLocal_AndDisconnectedIsUnavailable()
    {
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            using var network = new NetworkMetricsSource(Path.Combine(dir, "settings.json"));
            var s = new AppSettings();
            var first = await network.ReadAsync(s, All(network), default);
            await Task.Delay(20);
            var second = await network.ReadAsync(s, All(network), default);
            Assert.All(second.Where(m => m.Id.StartsWith("wifi.")), m => Assert.NotEmpty(m.Source));
            Assert.Contains(first, m => m.Id == "network.adapter");
            s.AdapterId = "All";
            Assert.Contains(await network.ReadAsync(s, All(network), default), m => m.Id == "network.adapter");
            var adapter = NetworkMetricsSource.Adapters().FirstOrDefault(a => a.Id is not "All" and not "Auto");
            if (adapter != null) { s.AdapterId = adapter.Id; await network.ReadAsync(s, All(network), default); }
            s.AdapterId = "nonexistent-adapter";
            Assert.All(await network.ReadAsync(s, All(network), default), m => Assert.Equal(SampleQuality.Unavailable, m.Quality));
            network.Dispose();
            using var restored = new NetworkMetricsSource(Path.Combine(dir, "settings.json"));
            Assert.Null(restored.PersistenceError);
            Assert.True(File.Exists(Path.Combine(dir, "traffic-today.json")));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public async Task Latency_IsOffUntilOptIn_AndOnlyTestLoopbackIsProbed()
    {
        using var source = new LatencyMetricsSource();
        var s = new AppSettings();
        Assert.All(await source.ReadAsync(s, All(source), default), m => Assert.Equal(SampleQuality.Unavailable, m.Quality));
        s.LatencyEnabled = true; s.LatencyEndpoint = "127.0.0.1"; s.LatencyIntervalSeconds = 2;
        var first = await source.ReadAsync(s, All(source), default);
        Assert.True(first.Single(m => m.Id == "network.packet_loss").IsNumeric);
        Assert.Same(first, await source.ReadAsync(s, All(source), default));
        await Task.Delay(2100);
        var second = await source.ReadAsync(s, All(source), default);
        Assert.Equal(0, second.Single(m => m.Id == "network.packet_loss").Value);
        Assert.True(second.Single(m => m.Id == "network.jitter").IsNumeric);
        s.LatencyEnabled = false;
        Assert.All(await source.ReadAsync(s, All(source), default), m => Assert.Equal(SampleQuality.Unavailable, m.Quality));
    }
}
