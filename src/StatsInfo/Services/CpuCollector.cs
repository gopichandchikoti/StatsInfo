using System.Diagnostics;
using StatsInfo.Models;

namespace StatsInfo.Services;

public class CpuCollector : IMetricCollector
{
    private PerformanceCounter? _cpuCounter;
    private bool _initialized;
    private bool _disposed;

    public string Key => "CPU_USAGE";
    public string DisplayName => "CPU";
    public string Source => "PerformanceCounter (Processor Total)";
    public bool IsAvailable => _initialized && _cpuCounter != null;

    public CpuCollector()
    {
        try
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total", true);
            // First call always returns 0 in performance counters
            _cpuCounter.NextValue();
            _initialized = true;
        }
        catch
        {
            _initialized = false;
        }
    }

    public Task<MetricSnapshot> CollectAsync(CancellationToken cancellationToken = default)
    {
        if (!_initialized || _cpuCounter == null)
        {
            return Task.FromResult(MetricSnapshot.Unavailable(Key, DisplayName, "%", Source, "CounterInitializationFailed"));
        }

        try
        {
            float rawValue = _cpuCounter.NextValue();
            double value = Math.Clamp(Math.Round(rawValue, 1), 0.0, 100.0);

            return Task.FromResult(new MetricSnapshot(
                Key: Key,
                DisplayName: DisplayName,
                Value: value,
                Unit: "%",
                Timestamp: DateTime.UtcNow,
                Status: MetricStatus.Active,
                Source: Source
            ));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new MetricSnapshot(
                Key: Key,
                DisplayName: DisplayName,
                Value: 0.0,
                Unit: "%",
                Timestamp: DateTime.UtcNow,
                Status: MetricStatus.Error,
                Source: Source,
                ErrorCode: ex.GetType().Name
            ));
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _cpuCounter?.Dispose();
            _cpuCounter = null;
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
