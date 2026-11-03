using System.IO;
using StatsInfo.Models;
using StatsInfo.Services;
using StatsInfo.ViewModels;
using Xunit;

namespace StatsInfo.Tests.ViewModels;

public class OverlayViewModelTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private string SettingsPath => Path.Combine(_directory, "settings.json");

    [Fact]
    public void SettingsChanged_UpdatesOverlayAppearanceAndMetricVisibility()
    {
        using var coordinator = new SamplingCoordinator(Array.Empty<IMetricCollector>());
        var service = new SettingsService(SettingsPath);
        var viewModel = new OverlayViewModel(coordinator, service);
        var settings = new AppSettings
        {
            Opacity = 0.6,
            Scale = 1.3,
            AlwaysOnTop = false,
            ShowCpuUsage = false,
            ShowGpuUsage = false,
            ShowRam = false,
            ShowNetwork = false,
            ShowDisplayHz = false
        };

        service.SaveSettings(settings);

        Assert.Equal(0.6, viewModel.Opacity);
        Assert.Equal(1.3, viewModel.Scale);
        Assert.False(viewModel.IsPinned);
        Assert.False(viewModel.ShowCpu);
        Assert.False(viewModel.ShowGpu);
        Assert.False(viewModel.ShowRam);
        Assert.False(viewModel.ShowNetwork);
        Assert.False(viewModel.ShowDisplayHz);
    }

    [Fact]
    public async Task MetricsUpdated_FormatsEveryKnownMetricAndUptime()
    {
        var snapshots = new[]
        {
            new MetricSnapshot("CPU_USAGE", "CPU", 25, "%", DateTime.UtcNow, MetricStatus.Active, "Test"),
            new MetricSnapshot("CPU_TEMPERATURE", "CPU Temperature", 47, "°C", DateTime.UtcNow, MetricStatus.Active, "Test"),
            new MetricSnapshot("RAM_USAGE", "RAM", 8, "GB", DateTime.UtcNow, MetricStatus.Active, "Test"),
            new MetricSnapshot("NETWORK_THROUGHPUT", "NET", 10, "Mbps", DateTime.UtcNow, MetricStatus.Active, "Test", FormattedOverride: "NET test"),
            new MetricSnapshot(
                "GPU_METRICS",
                "GPU",
                50,
                "%",
                DateTime.UtcNow,
                MetricStatus.Active,
                "Test",
                FormattedOverride: "test",
                SecondaryValue: 61,
                SecondaryUnit: "°C",
                SecondaryStatus: MetricStatus.Active),
            new MetricSnapshot("DISPLAY_HZ", "Display", 60, "Hz", DateTime.UtcNow, MetricStatus.Active, "Test", FormattedOverride: "60 Hz")
        };
        using var coordinator = new SamplingCoordinator(snapshots.Select(snapshot => new SnapshotCollector(snapshot)))
        {
            IntervalMs = 100
        };
        var service = new SettingsService(SettingsPath);
        var viewModel = new OverlayViewModel(coordinator, service);
        var updated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        coordinator.MetricsUpdated += _ => updated.TrySetResult();

        coordinator.Start();
        await updated.Task.WaitAsync(TimeSpan.FromSeconds(5));
        coordinator.Stop();

        Assert.Equal("CPU 25.0 %", viewModel.CpuText);
        Assert.Equal("CPU 47.0 °C", viewModel.CpuTempText);
        Assert.Equal("RAM 8.0 GB", viewModel.RamText);
        Assert.Equal("NET test", viewModel.NetworkText);
        Assert.Equal("GPU test", viewModel.GpuText);
        Assert.Equal("GPU 50%", viewModel.GpuUsageText);
        Assert.Equal("GPU 61 °C", viewModel.GpuTempText);
        Assert.Equal("60 Hz", viewModel.DisplayHzText);
        Assert.StartsWith("UP ", viewModel.SystemUptimeText);
    }

    [Fact]
    public async Task MetricsUpdated_LeavesMetricPlaceholders_WhenNoMetricsAreAvailable()
    {
        using var coordinator = new SamplingCoordinator(Array.Empty<IMetricCollector>())
        {
            IntervalMs = 100
        };
        var viewModel = new OverlayViewModel(coordinator, new SettingsService(SettingsPath));
        var updated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        coordinator.MetricsUpdated += _ => updated.TrySetResult();

        coordinator.Start();
        await updated.Task.WaitAsync(TimeSpan.FromSeconds(5));
        coordinator.Stop();

        Assert.Equal("CPU —", viewModel.CpuText);
        Assert.Equal("RAM —", viewModel.RamText);
        Assert.Equal("NET —", viewModel.NetworkText);
        Assert.Equal("GPU —", viewModel.GpuText);
        Assert.Equal("Display —", viewModel.DisplayHzText);
        Assert.StartsWith("UP ", viewModel.SystemUptimeText);
    }

    [Fact]
    public async Task MetricsUpdated_ShowsUnavailableGpuTemperatureInsteadOfZero()
    {
        var snapshot = new MetricSnapshot(
            "GPU_METRICS",
            "GPU",
            50,
            "%",
            DateTime.UtcNow,
            MetricStatus.Active,
            "Test",
            SecondaryUnit: "°C");
        using var coordinator = new SamplingCoordinator(new[] { new SnapshotCollector(snapshot) })
        {
            IntervalMs = 100
        };
        var viewModel = new OverlayViewModel(coordinator, new SettingsService(SettingsPath));
        var updated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        coordinator.MetricsUpdated += _ => updated.TrySetResult();

        coordinator.Start();
        await updated.Task.WaitAsync(TimeSpan.FromSeconds(5));
        coordinator.Stop();

        Assert.Equal("GPU 50%", viewModel.GpuUsageText);
        Assert.Equal("GPU N/A", viewModel.GpuTempText);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private sealed class SnapshotCollector(MetricSnapshot snapshot) : IMetricCollector
    {
        public string Key => snapshot.Key;
        public string DisplayName => snapshot.DisplayName;
        public string Source => snapshot.Source;
        public bool IsAvailable => true;
        public Task<MetricSnapshot> CollectAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(snapshot);
        public void Dispose() { }
    }
}
