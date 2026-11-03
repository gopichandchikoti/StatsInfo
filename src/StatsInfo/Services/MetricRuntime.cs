using System.Collections.Concurrent;
using System.Diagnostics;
using StatsInfo.Models;

namespace StatsInfo.Services;

public sealed class MetricRuntime : IDisposable
{
    private readonly SettingsService _settings;
    private readonly IMetricSource[] _sources;
    private readonly ConcurrentDictionary<string, MetricSample> _latest = new();
    private readonly ConcurrentDictionary<string, string> _sampleCategories = new();
    private readonly ConcurrentDictionary<string, string> _historyRequests = new();
    private CancellationTokenSource? _cts;
    private Task[] _tasks = [];
    private bool _disposed;
    private readonly object _alertGate = new();
    private readonly HistoryStore _historyStore;
    public MetricHistory History { get; } = new();
    public AlertEngine Alerts { get; } = new();
    public ConcurrentDictionary<string, AlertEvent> ActiveAlerts { get; } = new();
    public bool DiagnosticsOpen { get; set; }
    public bool FullscreenActive { get; set; }
    public string HistoryMetric { get; set; } = "";
    public void SetHistoryRequest(string viewId, string metricId) => _historyRequests[viewId] = metricId;
    public void RemoveHistoryRequest(string viewId) => _historyRequests.TryRemove(viewId, out _);
    public event Action<AlertEvent>? AlertRaised;
    public ConcurrentDictionary<string, double> CollectorMilliseconds { get; } = new();
    public string? PersistenceError => _historyStore.Error;
    public T? Source<T>() where T : class, IMetricSource => _sources.OfType<T>().FirstOrDefault();
    public double ExpectedIntervalSeconds(string id)
    {
        var s = _settings.Settings;
        string category = _sampleCategories.GetValueOrDefault(id, MetricRegistry.ById[id].Group);
        double interval = s.CategoryIntervals.GetValueOrDefault(category, s.UpdateIntervalMs) / 1000.0;
        if (s.LowImpactMode) interval = Math.Max(interval, 3);
        if (id is "network.ping" or "network.packet_loss" or "network.jitter")
            interval = Math.Max(interval, s.LatencyIntervalSeconds);
        return interval;
    }

    public MetricRuntime(SettingsService settings, IEnumerable<IMetricSource> sources)
    {
        _settings = settings;
        _sources = sources.ToArray();
        _historyStore = new HistoryStore(settings.SettingsPath);
        History.RetentionSeconds = settings.Settings.HistorySeconds;
        _historyStore.Restore(History, settings.Settings);
        foreach (var d in MetricRegistry.All)
            _latest[d.Id] = MetricSample.Missing(d.Id, "Not sampled; enable this metric or open diagnostics.");
        settings.SettingsChanged += SettingsChanged;
    }

    public IReadOnlyDictionary<string, MetricSample> Snapshot()
    {
        var now = DateTimeOffset.UtcNow;
        var s = _settings.Settings;
        return _latest.ToDictionary(p => p.Key, p =>
        {
            if (p.Value.Quality != SampleQuality.Valid) return p.Value;
            double maxAge = Math.Max(15, ExpectedIntervalSeconds(p.Key) * 3 + 5);
            return now - p.Value.SampledAt > TimeSpan.FromSeconds(maxAge)
                ? p.Value with { Quality = SampleQuality.Stale, Reason = "Sampling paused or source did not update." }
                : p.Value;
        });
    }

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_cts != null) return;
        _cts = new();
        var token = _cts.Token;
        _tasks = _sources.Select(source => Task.Run(() => RunSourceAsync(source, token))).ToArray();
    }

    private async Task RunSourceAsync(IMetricSource source, CancellationToken token)
    {
        Task<IReadOnlyList<MetricSample>>? pending = null;
        while (!token.IsCancellationRequested)
        {
            var s = _settings.Settings;
            HashSet<string> requested = [];
            try
            {
                requested = source.MetricIds.Where(id => DiagnosticsOpen || id == HistoryMetric ||
                    _historyRequests.Values.Contains(id) ||
                    s.Metrics.GetValueOrDefault(id)?.Visible == true ||
                    s.Alerts.Any(a => a.Enabled && a.MetricId == id)).ToHashSet();
                bool paused = s.Paused || s.AutoPauseOnBattery && PowerMetricsSource.IsOnBattery();
                bool releaseSensors = source is HardwareSensorSource { IsOpen: true } && !s.HardwareSensorsEnabled;
                if ((requested.Count > 0 && !paused) || releaseSensors)
                {
                    var watch = Stopwatch.StartNew();
                    pending ??= Task.Run(() => source.ReadAsync(s, requested, token), token);
                    var samples = await pending.WaitAsync(TimeSpan.FromSeconds(5), token);
                    pending = null;
                    CollectorMilliseconds[source.Category] = watch.Elapsed.TotalMilliseconds;
                    Publish(samples, s, source.Category);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                var quality = ex is UnauthorizedAccessException ? SampleQuality.PermissionDenied : SampleQuality.Error;
                if (ex is not TimeoutException) pending = null;
                Publish(requested.Select(id => MetricSample.Missing(id,
                    ex is TimeoutException ? "Collector timeout; waiting without starting overlapping reads."
                        : $"Source failed: {ex.GetType().Name}", quality)).ToArray(), s, source.Category);
            }
            int interval = s.CategoryIntervals.GetValueOrDefault(source.Category, s.UpdateIntervalMs);
            if (s.LowImpactMode) interval = Math.Max(interval, 3000);
            try { await Task.Delay(Math.Clamp(interval, 500, 60000), token); }
            catch (OperationCanceledException) { break; }
        }
        if (pending != null)
        {
            try { await pending; }
            catch (Exception) { /* Source failures are already exposed as quality states. */ }
        }
    }

    private void Publish(IReadOnlyList<MetricSample> samples, AppSettings s, string category)
    {
        History.RetentionSeconds = s.HistorySeconds;
        foreach (var sample in samples)
        {
            if (_latest.TryGetValue(sample.Id, out var old) && old.SampledAt == sample.SampledAt) continue;
            _latest[sample.Id] = sample;
            _sampleCategories[sample.Id] = category;
            History.Add(sample);
            var rule = s.Alerts.FirstOrDefault(a => a.MetricId == sample.Id);
            if (rule == null) continue;
            bool quiet = AlertEngine.IsQuietHour(DateTime.Now.Hour, s.QuietStartHour, s.QuietEndHour);
            AlertEvent? alert;
            lock (_alertGate) alert = Alerts.Evaluate(sample, rule,
                quiet || s.SuppressAlertsFullscreen && FullscreenActive);
            if (!rule.Enabled || !sample.IsNumeric ||
                (rule.Comparison == "<=" ? sample.Value >= rule.Clear : sample.Value <= rule.Clear))
                ActiveAlerts.TryRemove(sample.Id, out _);
            if (alert != null)
            {
                ActiveAlerts[sample.Id] = alert;
                WriteAlertLog(alert, rule);
                AlertRaised?.Invoke(alert);
            }
        }
        _historyStore.Save(History, s);
    }

    private void WriteAlertLog(AlertEvent alert, AlertRule rule)
    {
        string path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(_settings.SettingsPath)!, "alerts.jsonl");
        lock (_alertGate)
        {
            try
            {
                if (System.IO.File.Exists(path) && new System.IO.FileInfo(path).Length > 2 * 1024 * 1024)
                    System.IO.File.Move(path, path + ".1", true);
                System.IO.File.AppendAllText(path, System.Text.Json.JsonSerializer.Serialize(new
                {
                    alert.MetricId,
                    alert.Severity,
                    alert.Value,
                    alert.Unit,
                    alert.At,
                    Threshold = alert.Severity == "Critical" ? rule.Critical : rule.Warning
                }) + Environment.NewLine);
            }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
            { CollectorMilliseconds["Alert log unavailable"] = -1; }
        }
    }

    public void ClearHistory() { History.Clear(); _historyStore.Clear(); }

    private void SettingsChanged(AppSettings settings)
    {
        foreach (string id in ActiveAlerts.Keys)
            if (!settings.Alerts.Any(a => a.Enabled && a.MetricId == id)) ActiveAlerts.TryRemove(id, out _);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _settings.SettingsChanged -= SettingsChanged;
        _cts?.Cancel();
        _historyStore.Save(History, _settings.Settings, true);
        for (int i = 0; i < _sources.Length; i++)
        {
            var source = _sources[i];
            if (i < _tasks.Length) _ = _tasks[i].ContinueWith(_ => source.Dispose(), TaskScheduler.Default);
            else source.Dispose();
        }
        _cts?.Dispose();
    }
}
