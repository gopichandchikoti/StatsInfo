using StatsInfo.Models;

namespace StatsInfo.Services;

public interface IMetricSource : IDisposable
{
    string Category { get; }
    IReadOnlyList<string> MetricIds { get; }
    Task<IReadOnlyList<MetricSample>> ReadAsync(AppSettings settings, IReadOnlySet<string> requested,
        CancellationToken token);
}
