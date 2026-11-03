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
                string? settingsPath = null;
                int option = Array.IndexOf(e.Args, "--settings");
                if (option >= 0 && option + 1 < e.Args.Length) settingsPath = e.Args[option + 1];
                services.AddSingleton(new SettingsService(settingsPath));
                services.AddSingleton(sp =>
                {
                    var settings = sp.GetRequiredService<SettingsService>();
                    return new MetricRuntime(settings, new IMetricSource[]
                    {
                        new CpuMemorySource("CPU"), new CpuMemorySource("Memory"), new GpuEngineSource(),
                        new HardwareSensorSource(), new StorageMetricsSource(),
                        new NetworkMetricsSource(settings.SettingsPath), new LatencyMetricsSource(),
                        new DisplayMetricsSource(), new PowerMetricsSource(), new ProcessMetricsSource(),
                        new PresentMonSource()
                    });
                });

                services.AddSingleton<SamplingCoordinator>();

                // ViewModels
                services.AddSingleton<OverlayViewModel>();
                services.AddTransient<SettingsViewModel>();

                // Views
                services.AddSingleton<MainWindow>();
            })
            .Build();

        // Start metric sampling
        var runtime = _host.Services.GetRequiredService<MetricRuntime>();
        runtime.Start();

        // Show overlay window
        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
        if (e.Args.Contains("--settings-window")) mainWindow.ShowSettings();
        int captureOption = Array.IndexOf(e.Args, "--verify");
        if (captureOption >= 0 && captureOption + 1 < e.Args.Length)
            _ = VerifyAsync(e.Args[captureOption + 1], mainWindow, runtime);
    }

    private static async Task VerifyAsync(string directory, MainWindow window, MetricRuntime runtime)
    {
        try { await LocalVerification.CaptureAsync(directory, window, runtime); }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or InvalidOperationException)
        { MessageBox.Show($"Local verification failed: {ex.GetType().Name}", "StatsInfo"); }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}
