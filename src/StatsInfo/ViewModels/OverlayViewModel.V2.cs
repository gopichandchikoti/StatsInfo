using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using StatsInfo.Services;

namespace StatsInfo.ViewModels;

public partial class OverlayViewModel
{
    public MetricRuntime? Runtime { get; private set; }
    public SettingsService Settings => _settingsService;
    public ObservableCollection<MetricItemViewModel> VisibleMetrics { get; } = new();
    private MetricItemViewModel[] _allMetrics = [];
    private DispatcherTimer? _refresh;
    public string MonitorState => _settingsService.Settings.Paused ? "Paused" : "Live · local";

    private void InitializeV2(MetricRuntime? runtime)
    {
        Runtime = runtime;
        _allMetrics = Models.MetricRegistry.All.Select(d => new MetricItemViewModel(d, _settingsService)).ToArray();
        ApplyV2Settings();
        if (runtime == null || Application.Current == null) return;
        _refresh = new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Background,
            (_, _) => RefreshV2(), Application.Current.Dispatcher);
        _refresh.Start();
    }

    private void ApplyV2Settings()
    {
        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.BeginInvoke(ApplyV2Settings);
            return;
        }
        var desired = _allMetrics.Where(m => _settingsService.Settings.Metrics[m.Id].Visible)
            .OrderBy(m => m.Order).ThenBy(m => m.Definition.SortOrder).ToArray();
        foreach (var metric in _allMetrics) metric.Reload();
        if (!VisibleMetrics.SequenceEqual(desired))
        {
            VisibleMetrics.Clear();
            foreach (var metric in desired) VisibleMetrics.Add(metric);
        }
        OnPropertyChanged(nameof(MonitorState));
        RefreshV2();
    }

    private void RefreshV2()
    {
        if (Runtime == null) return;
        var samples = Runtime.Snapshot();
        foreach (var metric in VisibleMetrics)
        {
            if (samples.TryGetValue(metric.Id, out var sample)) metric.Update(sample, samples);
            if (metric.HasSparkline) metric.Trend = Runtime.History.Get(metric.Id)
                .Where(s => s.SampledAt >= DateTimeOffset.UtcNow.AddSeconds(-60)).TakeLast(120).ToArray();
            metric.IsAlert = Runtime.ActiveAlerts.ContainsKey(metric.Id) &&
                _settingsService.Settings.Alerts.FirstOrDefault(a => a.MetricId == metric.Id)?.DeliveryChannel != "Tray only";
        }
    }

    public void DisposeV2() => _refresh?.Stop();
}
