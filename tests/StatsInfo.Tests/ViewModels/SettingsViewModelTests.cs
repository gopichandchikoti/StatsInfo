using System.IO;
using StatsInfo.Models;
using StatsInfo.Services;
using StatsInfo.ViewModels;
using Xunit;

namespace StatsInfo.Tests.ViewModels;

public class SettingsViewModelTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
    private string SettingsPath => Path.Combine(_directory, "settings.json");

    [Fact]
    public void Constructor_CopiesSettingsIntoViewModel()
    {
        var settings = new AppSettings
        {
            Opacity = 0.5,
            Scale = 1.2,
            Theme = "Light",
            AlwaysOnTop = false,
            ClickThrough = true,
            UpdateIntervalMs = 2000,
            ShowCpuUsage = false,
            ShowRam = false,
            ShowNetwork = false,
            ShowGpuUsage = false,
            ShowDisplayHz = false
        };
        var service = new SettingsService(SettingsPath);
        service.SaveSettings(settings);

        var viewModel = new SettingsViewModel(service);

        Assert.Equal(settings.Opacity, viewModel.Opacity);
        Assert.Equal(settings.Scale, viewModel.Scale);
        Assert.Equal(settings.Theme, viewModel.Theme);
        Assert.Equal(settings.AlwaysOnTop, viewModel.AlwaysOnTop);
        Assert.Equal(settings.ClickThrough, viewModel.ClickThrough);
        Assert.Equal(settings.UpdateIntervalMs, viewModel.UpdateIntervalMs);
        Assert.False(viewModel.ShowCpu);
        Assert.False(viewModel.ShowRam);
        Assert.False(viewModel.ShowNetwork);
        Assert.False(viewModel.ShowGpu);
        Assert.False(viewModel.ShowDisplayHz);
    }

    [Fact]
    public void SaveCommand_PersistsAllEditableSettings_AndRequestsClose()
    {
        var service = new SettingsService(SettingsPath);
        var viewModel = new SettingsViewModel(service)
        {
            Opacity = 0.4,
            Scale = 1.5,
            Theme = "Light",
            AlwaysOnTop = false,
            ClickThrough = true,
            UpdateIntervalMs = 3000,
            ShowCpu = false,
            ShowRam = false,
            ShowNetwork = false,
            ShowGpu = false,
            ShowDisplayHz = false
        };
        var closeRequested = false;
        viewModel.RequestClose += () => closeRequested = true;

        viewModel.SaveCommand.Execute(null);

        var saved = new SettingsService(SettingsPath).Settings;
        Assert.Equal(0.4, saved.Opacity);
        Assert.Equal(1.5, saved.Scale);
        Assert.Equal("Light", saved.Theme);
        Assert.False(saved.AlwaysOnTop);
        Assert.True(saved.ClickThrough);
        Assert.Equal(3000, saved.UpdateIntervalMs);
        Assert.False(saved.ShowCpuUsage);
        Assert.False(saved.ShowRam);
        Assert.False(saved.ShowNetwork);
        Assert.False(saved.ShowGpuUsage);
        Assert.False(saved.ShowDisplayHz);
        Assert.True(closeRequested);
    }

    [Fact]
    public void SaveCommand_PersistsCpuAndGpuVisibilityIndependently()
    {
        var service = new SettingsService(SettingsPath);
        var viewModel = new SettingsViewModel(service)
        {
            ShowCpuUsage = true,
            ShowCpuTemp = false,
            ShowGpuUsage = false,
            ShowGpuTemp = true
        };

        viewModel.SaveCommand.Execute(null);

        var saved = new SettingsService(SettingsPath).Settings;
        Assert.True(saved.ShowCpuUsage);
        Assert.False(saved.ShowCpuTemp);
        Assert.False(saved.ShowGpuUsage);
        Assert.True(saved.ShowGpuTemp);
    }

    [Fact]
    public void CancelCommand_RequestsClose_WithoutSavingChanges()
    {
        var settings = new AppSettings { Opacity = 0.8 };
        var service = new SettingsService(SettingsPath);
        service.SaveSettings(settings);
        var viewModel = new SettingsViewModel(service) { Opacity = 0.2 };
        var closeRequested = false;
        viewModel.RequestClose += () => closeRequested = true;

        viewModel.CancelCommand.Execute(null);

        Assert.True(closeRequested);
        Assert.Equal(0.8, new SettingsService(SettingsPath).Settings.Opacity);
    }

    [Fact]
    public void Commands_Complete_WhenNoCloseHandlerIsRegistered()
    {
        var viewModel = new SettingsViewModel(new SettingsService(SettingsPath));

        viewModel.CancelCommand.Execute(null);
        viewModel.SaveCommand.Execute(null);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
