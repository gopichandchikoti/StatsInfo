using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using StatsInfo.Views;

namespace StatsInfo.Services;

public static class LocalVerification
{
    public sealed record Options(int InitialDelayMs = 8000, int SampleSeconds = 15);
    public static async Task CaptureAsync(string directory, MainWindow overlay, MetricRuntime runtime, Options? options = null)
    {
        options ??= new();
        Directory.CreateDirectory(directory);
        await Task.Delay(Math.Clamp(options.InitialDelayMs, 0, 60000));
        await overlay.Dispatcher.InvokeAsync(() =>
        {
            Capture(overlay, Path.Combine(directory, "overlay.png"));
            foreach (var window in Application.Current.Windows.OfType<SettingsWindow>())
            {
                Capture(window, Path.Combine(directory, "settings.png"));
                window.CaptureSections(directory);
            }
        }, DispatcherPriority.ApplicationIdle);
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
        jsonOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
        await File.WriteAllTextAsync(Path.Combine(directory, "live-metrics.json"),
            JsonSerializer.Serialize(runtime.Snapshot().Values, jsonOptions));
        using var process = Process.GetCurrentProcess();
        var startCpu = process.TotalProcessorTime;
        var watch = Stopwatch.StartNew();
        var memory = new List<long>();
        for (int i = 0; i < Math.Clamp(options.SampleSeconds, 1, 60); i++)
        {
            await Task.Delay(1000);
            process.Refresh();
            memory.Add(process.WorkingSet64);
        }
        double cpu = (process.TotalProcessorTime - startCpu).TotalSeconds / watch.Elapsed.TotalSeconds /
            Environment.ProcessorCount * 100;
        await File.WriteAllTextAsync(Path.Combine(directory, "overhead.json"), JsonSerializer.Serialize(new
        {
            SampleSeconds = watch.Elapsed.TotalSeconds,
            CpuPercentOfMachine = cpu,
            WorkingSetAverageMiB = memory.Average() / 1048576,
            WorkingSetPeakMiB = memory.Max() / 1048576.0,
            LogicalProcessors = Environment.ProcessorCount,
            Windows = Environment.OSVersion.VersionString,
            CollectorReadMilliseconds = runtime.CollectorMilliseconds.ToDictionary(),
            FpsCapture = runtime.Source<PresentMonSource>()?.IsCapturing ?? false
        }, jsonOptions));
    }

    public static void Capture(Window window, string path)
    {
        window.UpdateLayout();
        var bitmap = new RenderTargetBitmap(Math.Max(1, (int)Math.Ceiling(window.ActualWidth)),
            Math.Max(1, (int)Math.Ceiling(window.ActualHeight)), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
