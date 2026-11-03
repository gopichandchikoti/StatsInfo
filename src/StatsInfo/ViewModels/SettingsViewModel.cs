using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using StatsInfo.Models;
using StatsInfo.Services;

namespace StatsInfo.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsService _settingsService;

    [ObservableProperty] private double _opacity;
    [ObservableProperty] private double _scale;
    [ObservableProperty] private bool _alwaysOnTop;
    [ObservableProperty] private bool _clickThrough;
    [ObservableProperty] private int _updateIntervalMs;
    [ObservableProperty] private bool _showCpuUsage;
    [ObservableProperty] private bool _showCpuTemp;
    [ObservableProperty] private bool _showRam;
    [ObservableProperty] private bool _showNetwork;
    [ObservableProperty] private bool _showGpuUsage;
    [ObservableProperty] private bool _showGpuTemp;
    [ObservableProperty] private bool _showDisplayHz;
    [ObservableProperty] private string _theme;

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

    public event Action? RequestClose;

    public SettingsViewModel(SettingsService settingsService)
    {
        _settingsService = settingsService;
        var s = _settingsService.Settings;

        _opacity = s.Opacity;
        _scale = s.Scale;
        _alwaysOnTop = s.AlwaysOnTop;
        _clickThrough = s.ClickThrough;
        _updateIntervalMs = s.UpdateIntervalMs;
        _showCpuUsage = s.ShowCpuUsage;
        _showCpuTemp = s.ShowCpuTemp;
        _showRam = s.ShowRam;
        _showNetwork = s.ShowNetwork;
        _showGpuUsage = s.ShowGpuUsage;
        _showGpuTemp = s.ShowGpuTemp;
        _showDisplayHz = s.ShowDisplayHz;
        _theme = s.Theme;
        InitializeV2();
    }

    [RelayCommand]
    private void Save()
    {
        var s = _settingsService.Settings;
        s.Opacity = Opacity;
        s.Scale = Scale;
        s.AlwaysOnTop = AlwaysOnTop;
        s.ClickThrough = ClickThrough;
        s.UpdateIntervalMs = UpdateIntervalMs;
        s.ShowCpuUsage = ShowCpuUsage;
        s.ShowCpuTemp = ShowCpuTemp;
        s.ShowRam = ShowRam;
        s.ShowNetwork = ShowNetwork;
        s.ShowGpuUsage = ShowGpuUsage;
        s.ShowGpuTemp = ShowGpuTemp;
        s.ShowDisplayHz = ShowDisplayHz;
        MetricSettings.SetVisible(s, "cpu.usage.total", ShowCpuUsage);
        MetricSettings.SetVisible(s, "cpu.temperature.package", ShowCpuTemp);
        MetricSettings.SetVisible(s, "gpu.usage.total", ShowGpuUsage);
        MetricSettings.SetVisible(s, "gpu.temperature.core", ShowGpuTemp);
        MetricSettings.SetVisible(s, "memory.physical.used", ShowRam);
        MetricSettings.SetVisible(s, "network.download.rate", ShowNetwork);
        MetricSettings.SetVisible(s, "network.upload.rate", ShowNetwork);
        MetricSettings.SetVisible(s, "display.refresh.configured", ShowDisplayHz);
        s.Theme = Theme;

        _settingsService.SaveSettings(s);
        RequestClose?.Invoke();
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke();
    }
}
