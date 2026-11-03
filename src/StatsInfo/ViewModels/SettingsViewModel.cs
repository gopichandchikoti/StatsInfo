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
    [ObservableProperty] private bool _showCpu;
    [ObservableProperty] private bool _showRam;
    [ObservableProperty] private bool _showNetwork;
    [ObservableProperty] private bool _showGpu;
    [ObservableProperty] private bool _showDisplayHz;
    [ObservableProperty] private string _theme;

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
        _showCpu = s.ShowCpuUsage;
        _showRam = s.ShowRam;
        _showNetwork = s.ShowNetwork;
        _showGpu = s.ShowGpuUsage;
        _showDisplayHz = s.ShowDisplayHz;
        _theme = s.Theme;
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
        s.ShowCpuUsage = ShowCpu;
        s.ShowRam = ShowRam;
        s.ShowNetwork = ShowNetwork;
        s.ShowGpuUsage = ShowGpu;
        s.ShowDisplayHz = ShowDisplayHz;
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
