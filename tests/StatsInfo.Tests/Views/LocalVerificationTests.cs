using System.IO;
using System.Text.Json;
using StatsInfo.Services;
using StatsInfo.ViewModels;
using Xunit;

namespace StatsInfo.Tests.Views;

[Collection("WPF UI")]
public class LocalVerificationTests
{
    [Fact]
    public async Task LocalVerification_WritesRenderedWindowsTypedSamplesAndMeasuredOverhead()
    {
        string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Task? capture = null;
        MainWindow? overlay = null;
        MetricRuntime? runtime = null;
        using var coordinator = new SamplingCoordinator([]);
        MainWindowSettingsTests.WpfTestHost.Run(() =>
        {
            var settings = new SettingsService(Path.Combine(dir, "settings.json"));
            runtime = new MetricRuntime(settings, []);
            overlay = new MainWindow(new OverlayViewModel(coordinator, settings, runtime), new SettingsViewModel(settings));
            overlay.Show(); overlay.ShowSettings();
            capture = LocalVerification.CaptureAsync(dir, overlay, runtime, new(0, 1));
        });
        try
        {
            await capture!.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.True(File.Exists(Path.Combine(dir, "overlay.png")));
            Assert.True(File.Exists(Path.Combine(dir, "settings.png")));
            using var metrics = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "live-metrics.json")));
            Assert.Equal(70, metrics.RootElement.GetArrayLength());
            using var overhead = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, "overhead.json")));
            Assert.True(overhead.RootElement.GetProperty("SampleSeconds").GetDouble() >= 1);
            Assert.False(overhead.RootElement.GetProperty("FpsCapture").GetBoolean());
        }
        finally
        {
            MainWindowSettingsTests.WpfTestHost.Run(() => overlay?.Close());
            runtime?.Dispose();
            Directory.Delete(dir, true);
        }
    }
}
