using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StatsInfo.Models;
using StatsInfo.Services;

namespace StatsInfo.ViewModels;

public partial class OverlayViewModel : ObservableObject
{
    private readonly SamplingCoordinator _samplingCoordinator;
    private readonly SettingsService _settingsService;

    [ObservableProperty] private string _cpuText = "CPU —";
    [ObservableProperty] private string _ramText = "RAM —";
    [ObservableProperty] private string _networkText = "NET —";
    [ObservableProperty] private string _gpuText = "GPU —";
    [ObservableProperty] private string _displayHzText = "Display —";
    [ObservableProperty] private string _systemUptimeText = "UP —";
    [ObservableProperty] private double _opacity = 0.85;
    [ObservableProperty] private double _scale = 1.0;
    [ObservableProperty] private bool _isPinned = true;
    [ObservableProperty] private bool _showCpu = true;
    [ObservableProperty] private bool _showRam = true;
    [ObservableProperty] private bool _showNetwork = true;
    [ObservableProperty] private bool _showGpu = true;
    [ObservableProperty] private bool _showDisplayHz = true;

    public event Action? RequestOpenSettings;

    public OverlayViewModel(SamplingCoordinator samplingCoordinator, SettingsService settingsService)
    {
        _samplingCoordinator = samplingCoordinator;
        _settingsService = settingsService;

        ApplySettings(_settingsService.Settings);
        _settingsService.SettingsChanged += ApplySettings;
        _samplingCoordinator.MetricsUpdated += OnMetricsUpdated;
    }

    private void ApplySettings(AppSettings s)
    {
        Opacity = s.Opacity;
        Scale = s.Scale;
        IsPinned = s.AlwaysOnTop;
        ShowCpu = s.ShowCpuUsage;
        ShowRam = s.ShowRam;
        ShowNetwork = s.ShowNetwork;
        ShowGpu = s.ShowGpuUsage;
        ShowDisplayHz = s.ShowDisplayHz;
    }

    private void OnMetricsUpdated(IReadOnlyDictionary<string, MetricSnapshot> metrics)
    {
        UpdateSystemUptime();

        if (metrics.TryGetValue("CPU_USAGE", out var cpu))
        {
            CpuText = $"CPU {cpu.FormattedValue}";
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

    [RelayCommand]
    private void OpenSettings()
    {
        RequestOpenSettings?.Invoke();
    }
}
