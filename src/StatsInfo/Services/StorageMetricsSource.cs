using System.Diagnostics;
using System.IO;
using StatsInfo.Models;

namespace StatsInfo.Services;

public sealed class StorageMetricsSource : IMetricSource
{
    private readonly CounterReader _counters = new();
    public string Category => "Storage";
    public IReadOnlyList<string> MetricIds { get; } = ["storage.read.rate", "storage.write.rate",
        "storage.active_time", "storage.capacity.free", "storage.health.status"];
    public Task<IReadOnlyList<MetricSample>> ReadAsync(AppSettings settings, IReadOnlySet<string> requested,
        CancellationToken token)
    {
        var result = new List<MetricSample>();
        if (requested.Contains("storage.capacity.free"))
        {
            var drive = new DriveInfo(settings.Volume);
            result.Add(drive.IsReady ? MetricSample.Number("storage.capacity.free", drive.AvailableFreeSpace / 1073741824.0,
                $"DriveInfo ({drive.Name})", $"{drive.AvailableFreeSpace / 1073741824.0:F1} GiB " +
                $"({100.0 * drive.AvailableFreeSpace / drive.TotalSize:F0}% free)")
                : MetricSample.Missing("storage.capacity.free", "Selected volume is disconnected or not ready."));
        }
        var mapping = new Dictionary<string, string>
        {
            ["storage.read.rate"] = "Disk Read Bytes/sec",
            ["storage.write.rate"] = "Disk Write Bytes/sec",
            ["storage.active_time"] = "% Idle Time"
        };
        string instance = settings.DiskInstance == "Auto" ? DiskInstances().Order().FirstOrDefault() ?? "" : settings.DiskInstance;
        foreach (var pair in mapping.Where(p => requested.Contains(p.Key)))
        {
            double? value = _counters.Read("PhysicalDisk", pair.Value, instance);
            if (value == null) result.Add(MetricSample.Missing(pair.Key, "Waiting for disk counter interval.",
                SampleQuality.WarmingUp));
            else result.Add(MetricSample.Number(pair.Key, pair.Key == "storage.active_time"
                ? Math.Clamp(100 - value.Value, 0, 100) : Math.Max(0, value.Value) / 1000000,
                $"PDH PhysicalDisk/{pair.Value}/{instance}"));
        }
        result.Add(MetricSample.Missing("storage.health.status",
            "Unknown — no reliable SMART/NVMe health-warning decoder is enabled; absent data is not healthy."));
        return Task.FromResult<IReadOnlyList<MetricSample>>(result);
    }
    public static string[] DiskInstances()
    {
        try { return new PerformanceCounterCategory("PhysicalDisk").GetInstanceNames().Where(n => n != "_Total").ToArray(); }
        catch (InvalidOperationException) { return []; }
    }
    public void Dispose() => _counters.Dispose();
}
