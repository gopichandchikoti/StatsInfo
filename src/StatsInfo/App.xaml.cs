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
                int smoke = Array.IndexOf(e.Args, "--smoke-test");
                if (smoke >= 0 && smoke + 1 < e.Args.Length)
                    settingsPath = System.IO.Path.Combine(e.Args[smoke + 1], "settings.json");
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
        int smokeOption = Array.IndexOf(e.Args, "--smoke-test");
        if (smokeOption >= 0 && smokeOption + 1 < e.Args.Length)
        {
            _ = SmokeTestAsync(e.Args[smokeOption + 1], mainWindow,
                _host.Services.GetRequiredService<SettingsService>());
            return;
        }
        if (e.Args.Contains("--settings-window")) mainWindow.ShowSettings();
        int captureOption = Array.IndexOf(e.Args, "--verify");
        if (captureOption >= 0 && captureOption + 1 < e.Args.Length)
            _ = VerifyAsync(e.Args[captureOption + 1], mainWindow, runtime);
    }

    // Exercise the published single-file app with an isolated settings profile before releasing it.
    private async Task SmokeTestAsync(string directory, MainWindow overlay, SettingsService settings)
    {
        System.IO.Directory.CreateDirectory(directory);
        try
        {
            await Task.Delay(8000);
            overlay.ShowSettings();
            var preferences = Windows.OfType<SettingsWindow>().Single();
            if (!preferences.IsVisible || preferences.Icon == null || !overlay.IsVisible)
                throw new InvalidOperationException("Packaged Settings did not open with its embedded icon.");
            preferences.UpdateLayout();
            LocalVerification.Capture(preferences, System.IO.Path.Combine(directory, "settings.png"));
            overlay.ShowSettings();
            if (Windows.OfType<SettingsWindow>().Count() != 1)
                throw new InvalidOperationException("Settings opened more than once.");
            preferences.Close();
            overlay.ShowSettings();
            if (!Windows.OfType<SettingsWindow>().Single().IsVisible)
                throw new InvalidOperationException("Settings did not reopen.");
            Windows.OfType<SettingsWindow>().Single().Close();

            foreach (var metric in Models.MetricRegistry.All)
                MetricSettings.SetVisible(settings.Settings, metric.Id,
                    metric.Id is "cpu.usage.total" or "gpu.usage.total" or "memory.physical.used");
            settings.SaveSettings(settings.Settings);
            overlay.UpdateLayout();
            overlay.ResizeOverlay(700, 40); overlay.UpdateLayout();
            double wideHeight = overlay.Height;
            LocalVerification.Capture(overlay, System.IO.Path.Combine(directory, "overlay-wide.png"));
            overlay.ResizeOverlay(1, 40); overlay.UpdateLayout();
            double narrowHeight = overlay.Height;
            if (narrowHeight <= wideHeight) throw new InvalidOperationException("Metrics did not stack when narrowed.");
            LocalVerification.Capture(overlay, System.IO.Path.Combine(directory, "overlay-narrow.png"));
            MetricSettings.SetVisible(settings.Settings, "gpu.usage.total", false);
            MetricSettings.SetVisible(settings.Settings, "memory.physical.used", false);
            settings.SaveSettings(settings.Settings); overlay.UpdateLayout();
            overlay.ResizeOverlay(1, 1); overlay.UpdateLayout();
            if (overlay.Height >= narrowHeight) throw new InvalidOperationException("Single-metric minimum did not shrink.");
            LocalVerification.Capture(overlay, System.IO.Path.Combine(directory, "overlay-cpu.png"));
            await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(directory, "smoke-test.json"),
                System.Text.Json.JsonSerializer.Serialize(new
                {
                    Version = typeof(App).Assembly.GetName().Version?.ToString(),
                    SettingsOpenedAndReopened = true,
                    WideHeight = wideHeight,
                    NarrowHeight = narrowHeight,
                    CpuOnlyWidth = overlay.Width,
                    CpuOnlyHeight = overlay.Height
                }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            Shutdown(0);
        }
        catch (Exception ex)
        {
            await System.IO.File.WriteAllTextAsync(System.IO.Path.Combine(directory, "smoke-test-error.txt"), ex.ToString());
            Shutdown(1);
        }
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
