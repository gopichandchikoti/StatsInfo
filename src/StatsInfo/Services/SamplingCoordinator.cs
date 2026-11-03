using System.Collections.Concurrent;
using StatsInfo.Models;

namespace StatsInfo.Services;

public class SamplingCoordinator : IDisposable
{
    private readonly IEnumerable<IMetricCollector> _collectors;
    private readonly ConcurrentDictionary<string, MetricSnapshot> _latestSnapshots = new();
    private CancellationTokenSource? _cts;
    private Task? _samplingTask;
    private bool _disposed;

    public event Action<IReadOnlyDictionary<string, MetricSnapshot>>? MetricsUpdated;
    public int IntervalMs { get; set; } = 1000;
    public bool IsRunning => _samplingTask != null && !_samplingTask.IsCompleted;

    public SamplingCoordinator(IEnumerable<IMetricCollector> collectors)
    {
        _collectors = collectors;
    }

    public void Start()
    {
        if (IsRunning) return;

        _cts = new CancellationTokenSource();
        _samplingTask = Task.Run(() => RunSamplingLoopAsync(_cts.Token));
    }

    public void Stop()
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private async Task RunSamplingLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var tasks = _collectors.Select(async collector =>
                {
                    try
                    {
                        var snapshot = await collector.CollectAsync(token);
                        _latestSnapshots[collector.Key] = snapshot;
                    }
                    catch (Exception ex)
                    {
                        _latestSnapshots[collector.Key] = new MetricSnapshot(
                            Key: collector.Key,
                            DisplayName: collector.DisplayName,
                            Value: 0.0,
                            Unit: "",
                            Timestamp: DateTime.UtcNow,
                            Status: MetricStatus.Error,
                            Source: collector.Source,
                            ErrorCode: ex.GetType().Name
                        );
                    }
                });

                await Task.WhenAll(tasks);
                MetricsUpdated?.Invoke(_latestSnapshots);

                await Task.Delay(IntervalMs, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Isolate unexpected scheduling exceptions
                await Task.Delay(IntervalMs, token);
            }
        }
    }

    public IReadOnlyDictionary<string, MetricSnapshot> GetLatestSnapshots() => _latestSnapshots;

    public void Dispose()
    {
        if (!_disposed)
        {
            Stop();
            foreach (var collector in _collectors)
            {
                collector.Dispose();
            }
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
