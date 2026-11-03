using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using StatsInfo.Models;
using StatsInfo.Services;

namespace StatsInfo.ViewModels;

public partial class SettingsViewModel
{
    public SettingsService Service => _settingsService;
    public ObservableCollection<MetricItemViewModel> Metrics { get; } = new();
    public ICollectionView MetricsView { get; private set; } = null!;
    [ObservableProperty] private string _search = "";
    [ObservableProperty] private string _availability = "All";
    public string[] AvailabilityOptions { get; } = ["All", "Available", "Unavailable", "Enabled"];
    public string[] PresetNames { get; } = ["Everyday", "Gaming", "Network", "Hardware Health", "Minimal", "Custom"];
    public string[] Groups { get; } = MetricRegistry.All.Select(d => d.Group).Distinct().ToArray();

    private void InitializeV2()
    {
        foreach (var d in MetricRegistry.All) Metrics.Add(new(d, _settingsService));
        MetricsView = CollectionViewSource.GetDefaultView(Metrics);
        MetricsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(MetricItemViewModel.Group)));
        MetricsView.SortDescriptions.Add(new(nameof(MetricItemViewModel.Order), ListSortDirection.Ascending));
        MetricsView.Filter = item => item is MetricItemViewModel metric && Matches(metric);
    }

    private bool Matches(MetricItemViewModel metric) =>
        (metric.Label.Contains(Search, StringComparison.OrdinalIgnoreCase) ||
         metric.Id.Contains(Search, StringComparison.OrdinalIgnoreCase) ||
         metric.Description.Contains(Search, StringComparison.OrdinalIgnoreCase)) && (Availability switch
         {
             "Available" => metric.IsAvailable,
             "Unavailable" => !metric.IsAvailable,
             "Enabled" => metric.IsEnabled,
             _ => true
         });
    partial void OnSearchChanged(string value) => MetricsView?.Refresh();
    partial void OnAvailabilityChanged(string value) => MetricsView?.Refresh();

    public void RefreshMetrics(MetricRuntime? runtime)
    {
        var samples = runtime?.Snapshot();
        foreach (var metric in Metrics)
        {
            metric.Reload();
            if (samples != null && samples.TryGetValue(metric.Id, out var sample)) metric.Update(sample, samples);
        }
        MetricsView.Refresh();
    }

    public void MoveMetric(MetricItemViewModel metric, int direction)
    {
        var ordered = Metrics.OrderBy(m => m.Order).ThenBy(m => m.Definition.SortOrder).ToList();
        int from = ordered.IndexOf(metric), to = from + direction;
        if (from < 0 || to < 0 || to >= ordered.Count) return;
        (ordered[from], ordered[to]) = (ordered[to], ordered[from]);
        for (int i = 0; i < ordered.Count; i++) _settingsService.Settings.Metrics[ordered[i].Id].Order = i;
        _settingsService.SaveSettings(_settingsService.Settings);
        RefreshMetrics(null);
    }
}
