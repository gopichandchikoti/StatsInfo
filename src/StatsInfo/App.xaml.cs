using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StatsInfo.Services;
using StatsInfo.ViewModels;
using StatsInfo.Views;

namespace StatsInfo;

public partial class App : Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices((_, services) =>
            {
                // Services & Collectors
                services.AddSingleton<SettingsService>();
                services.AddSingleton<IMetricCollector, CpuCollector>();
                services.AddSingleton<IMetricCollector, MemoryCollector>();
                services.AddSingleton<IMetricCollector, NetworkCollector>();
                services.AddSingleton<IMetricCollector, DisplayModeCollector>();
                services.AddSingleton<IMetricCollector, GpuCollector>();

                services.AddSingleton<SamplingCoordinator>();

                // ViewModels
                services.AddSingleton<OverlayViewModel>();
                services.AddTransient<SettingsViewModel>();

                // Views
                services.AddSingleton<MainWindow>();
            })
            .Build();

        // Start metric sampling
        var coordinator = _host.Services.GetRequiredService<SamplingCoordinator>();
        coordinator.Start();

        // Show overlay window
        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}
