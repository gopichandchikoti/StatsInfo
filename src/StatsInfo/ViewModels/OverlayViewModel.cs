using CommunityToolkit.Mvvm.ComponentModel;
using StatsInfo.Models;
using StatsInfo.Services;

namespace StatsInfo.ViewModels;

public partial class OverlayViewModel : ObservableObject
{
    private readonly SamplingCoordinator _samplingCoordinator;
    private readonly SettingsService _settingsService;

    [ObservableProperty] private string _cpuText = "CPU —";
    [ObservableProperty] private string _cpuTempText = "CPU N/A";
    [ObservableProperty] private string _ramText = "RAM —";
    [ObservableProperty] private string _networkText = "NET —";
    [ObservableProperty] private string _gpuText = "GPU —";
    [ObservableProperty] private string _gpuUsageText = "GPU —";
    [ObservableProperty] private string _gpuTempText = "GPU N/A";
    [ObservableProperty] private string _displayHzText = "Display —";
    [ObservableProperty] private string _systemUptimeText = "UP —";
    [ObservableProperty] private double _opacity = 0.85;
    [ObservableProperty] private double _scale = 1.0;
    [ObservableProperty] private bool _isPinned = true;
    [ObservableProperty] private bool _showCpuUsage = true;
    [ObservableProperty] private bool _showCpuTemp = true;
    [ObservableProperty] private bool _showRam = true;
    [ObservableProperty] private bool _showNetwork = true;
    [ObservableProperty] private bool _showGpuUsage = true;
    [ObservableProperty] private bool _showGpuTemp = true;
    [ObservableProperty] private bool _showDisplayHz = true;

    public bool ShowCpu
    {
        get => ShowCpuUsage;
        set => ShowCpuUsage = value;
    }

    public bool ShowGpu
    {
        get => ShowGpuUsage;
        set => ShowGpuUsage = value;
    }

    public OverlayViewModel(SamplingCoordinator samplingCoordinator, SettingsService settingsService,
        MetricRuntime? runtime = null)
    {
        _samplingCoordinator = samplingCoordinator;
        _settingsService = settingsService;

        ApplySettings(_settingsService.Settings);
        _settingsService.SettingsChanged += ApplySettings;
        _samplingCoordinator.MetricsUpdated += OnMetricsUpdated;
        InitializeV2(runtime);
    }

    private void ApplySettings(AppSettings s)
    {
        Opacity = s.Opacity;
        Scale = s.Scale;
        IsPinned = s.AlwaysOnTop;
        ShowCpuUsage = s.ShowCpuUsage;
        ShowCpuTemp = s.ShowCpuTemp;
        ShowRam = s.ShowRam;
        ShowNetwork = s.ShowNetwork;
        ShowGpuUsage = s.ShowGpuUsage;
        ShowGpuTemp = s.ShowGpuTemp;
        ShowDisplayHz = s.ShowDisplayHz;
        ApplyV2Settings();
    }

    private void OnMetricsUpdated(IReadOnlyDictionary<string, MetricSnapshot> metrics)
    {
        UpdateSystemUptime();

        if (metrics.TryGetValue("CPU_USAGE", out var cpu))
        {
            CpuText = $"CPU {cpu.FormattedValue}";
        }

        if (metrics.TryGetValue("CPU_TEMPERATURE", out var cpuTemperature))
        {
            CpuTempText = cpuTemperature.Status == MetricStatus.Active
                ? $"CPU {cpuTemperature.FormattedValue}"
                : "CPU N/A";
        }

        if (metrics.TryGetValue("RAM_USAGE", out var ram))
        {
            RamText = $"RAM {ram.FormattedValue}";
        }

        if (metrics.TryGetValue("NETWORK_THROUGHPUT", out var net))
        {
            NetworkText = net.FormattedValue;
        }

        if (metrics.TryGetValue("GPU_METRICS", out var gpu))
        {
            GpuText = $"GPU {gpu.FormattedValue}";
            GpuUsageText = gpu.Status == MetricStatus.Active
                ? $"GPU {gpu.Value:F0}%"
                : "GPU N/A";
            GpuTempText = gpu.SecondaryStatus == MetricStatus.Active && gpu.SecondaryFormattedValue != null
                ? $"GPU {gpu.SecondaryFormattedValue}"
                : "GPU N/A";
        }

        if (metrics.TryGetValue("DISPLAY_HZ", out var hz))
        {
            DisplayHzText = hz.FormattedValue;
        }
    }

    private void UpdateSystemUptime()
    {
        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        SystemUptimeText = uptime.TotalDays >= 1
            ? $"UP {uptime.Days}d {uptime.Hours:D2}h {uptime.Minutes:D2}m"
            : $"UP {uptime.Hours:D2}h {uptime.Minutes:D2}m";
    }
}
