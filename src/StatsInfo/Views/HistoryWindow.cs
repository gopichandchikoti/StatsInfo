using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using StatsInfo.Models;
using StatsInfo.Services;

namespace StatsInfo.Views;

public sealed class HistoryWindow : Window
{
    private readonly MetricRuntime _runtime;
    private readonly ComboBox _metric;
    private readonly TextBlock _summary = new() { Margin = new Thickness(0, 14, 0, 8) };
    private readonly HistoryPlot _plot = new() { Height = 260, Margin = new Thickness(0, 14, 0, 14) };
    private readonly DispatcherTimer _timer;
    private readonly string _viewId = Guid.NewGuid().ToString("N");
    public HistoryWindow(MetricRuntime runtime, SettingsService settings, string? metricId = null)
    {
        _runtime = runtime;
        Title = "StatsInfo · Metric history";
        Width = 780; Height = 470; MinWidth = 450; MinHeight = 400;
        ThemeService.Apply(this, settings.Settings);
        SetResourceReference(BackgroundProperty, "PageBackground");
        SetResourceReference(ForegroundProperty, "PageForeground");
        var panel = new StackPanel { Margin = new Thickness(24) };
        _metric = new ComboBox
        {
            ItemsSource = MetricRegistry.All.Where(d => !string.IsNullOrEmpty(d.Unit)).ToArray(),
            DisplayMemberPath = "Label",
            SelectedValuePath = "Id"
        };
        SettingsPanelBuilder.Name(_metric, "History metric");
        _metric.SelectionChanged += (_, _) => Refresh();
        panel.Children.Add(_metric);
        panel.Children.Add(_summary);
        panel.Children.Add(_plot);
        panel.Children.Add(new TextBlock
        {
            Text = "Gaps indicate unavailable/stale readings or interrupted sampling. " +
            "Numeric values use source units. CSV/JSON export is in Settings → History & Export.",
            TextWrapping = TextWrapping.Wrap
        });
        Content = panel;
        _metric.SelectedValue = metricId ?? "cpu.usage.total";
        _timer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => Refresh(), Dispatcher);
        _timer.Start();
        Closed += (_, _) => { _timer.Stop(); _runtime.HistoryMetric = ""; _runtime.RemoveHistoryRequest(_viewId); };
    }

    private void Refresh()
    {
        if (_metric.SelectedItem is not MetricDefinition definition) return;
        _runtime.HistoryMetric = definition.Id;
        _runtime.SetHistoryRequest(_viewId, definition.Id);
        var samples = _runtime.History.Get(definition.Id);
        var values = samples.Where(s => s.IsNumeric).Select(s => s.Value!.Value).ToArray();
        _summary.Text = values.Length == 0 ? "No numeric samples yet. Enable a supported source or wait for collection."
            : $"Min {values.Min():0.##} · Avg {values.Average():0.##} · Max {values.Max():0.##} {definition.Unit} · " +
              $"{values.Length} valid samples · {samples.First().SampledAt.LocalDateTime:T}–{samples.Last().SampledAt.LocalDateTime:T}";
        _plot.Samples = samples;
        _plot.MaxGapSeconds = Math.Max(5, _runtime.ExpectedIntervalSeconds(definition.Id) * 3);
        _plot.InvalidateVisual();
    }
}

public sealed class HistoryPlot : FrameworkElement
{
    public MetricSample[] Samples { get; set; } = [];
    public double MaxGapSeconds { get; set; } = 5;
    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);
        context.DrawRectangle(Brushes.White, new Pen(Brushes.LightGray, 1), new Rect(RenderSize));
        var valid = Samples.Where(s => s.IsNumeric).ToArray();
        if (valid.Length < 2 || ActualWidth <= 0 || ActualHeight <= 0) return;
        double min = valid.Min(s => s.Value!.Value), max = valid.Max(s => s.Value!.Value);
        double range = Math.Max(max - min, 1);
        var start = Samples.First().SampledAt;
        double duration = Math.Max(1, (Samples.Last().SampledAt - start).TotalSeconds);
        Point? previous = null;
        DateTimeOffset? previousTime = null;
        double previousPixel = -1;
        foreach (var sample in Samples)
        {
            if (!sample.IsNumeric) { previous = null; previousTime = null; continue; }
            double x = 8 + (ActualWidth - 16) * (sample.SampledAt - start).TotalSeconds / duration;
            double y = ActualHeight - 8 - (ActualHeight - 16) * (sample.Value!.Value - min) / range;
            var point = new Point(x, y);
            if (previousTime is { } time && (sample.SampledAt - time).TotalSeconds > MaxGapSeconds) previous = null;
            if (previous is { } p && x - previousPixel >= 1) context.DrawLine(new Pen(Brushes.SteelBlue, 1.5), p, point);
            if (x - previousPixel >= 1 || previous == null) { previous = point; previousPixel = x; }
            previousTime = sample.SampledAt;
        }
    }
}
