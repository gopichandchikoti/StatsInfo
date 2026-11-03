using StatsInfo.Models;

namespace StatsInfo.Services;

public interface IMetricCollector : IDisposable
{
    string Key { get; }
    string DisplayName { get; }
    string Source { get; }
    bool IsAvailable { get; }

    Task<MetricSnapshot> CollectAsync(CancellationToken cancellationToken = default);
}
