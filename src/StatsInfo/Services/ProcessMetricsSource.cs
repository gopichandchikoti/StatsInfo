using System.ComponentModel;
using System.Diagnostics;
using StatsInfo.Models;

namespace StatsInfo.Services;

public sealed class ProcessMetricsSource : IMetricSource
{
    private Dictionary<int, (long Start, double Cpu)> _previous = new();
    private long _lastTick;
    public string Category => "Process";
    public IReadOnlyList<string> MetricIds { get; } = ["process.top_cpu", "process.top_ram"];
    public Task<IReadOnlyList<MetricSample>> ReadAsync(AppSettings settings, IReadOnlySet<string> requested,
        CancellationToken token)
    {
        var now = Stopwatch.GetTimestamp();
        double elapsed = _lastTick == 0 ? 0 : Stopwatch.GetElapsedTime(_lastTick, now).TotalSeconds;
        var current = new Dictionary<int, (long Start, double Cpu)>();
        double topCpu = -1, topRam = -1;
        string cpuName = "", ramName = "";
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    double ram = process.WorkingSet64 / 1073741824.0;
                    if (ram > topRam) { topRam = ram; ramName = process.ProcessName; }
                    if (!requested.Contains("process.top_cpu")) continue;
                    double cpu = process.TotalProcessorTime.TotalSeconds;
                    long start = process.StartTime.ToUniversalTime().Ticks;
                    current[process.Id] = (start, cpu);
                    if (elapsed <= 0 || !_previous.TryGetValue(process.Id, out var old) || old.Start != start) continue;
                    double usage = Math.Clamp((cpu - old.Cpu) / elapsed / Environment.ProcessorCount * 100, 0, 100);
                    if (usage > topCpu) { topCpu = usage; cpuName = process.ProcessName; }
                }
                catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or NotSupportedException)
                { /* Exited and protected processes are intentionally excluded. */ }
            }
        }
        _previous = current;
        _lastTick = now;
        IReadOnlyList<MetricSample> result = [
            topCpu >= 0 ? MetricSample.Number("process.top_cpu", topCpu,
                "Process.TotalProcessorTime / monotonic interval / logical processors", $"{cpuName} {topCpu:F1}%")
                : MetricSample.Missing("process.top_cpu", "Waiting for process interval; protected processes excluded.",
                    SampleQuality.WarmingUp),
            topRam >= 0 ? MetricSample.Number("process.top_ram", topRam, "Process.WorkingSet64",
                $"{ramName} {topRam:F2} GiB") : MetricSample.Missing("process.top_ram", "No accessible processes.")];
        return Task.FromResult(result);
    }
    public void Dispose() => _previous.Clear();
}
