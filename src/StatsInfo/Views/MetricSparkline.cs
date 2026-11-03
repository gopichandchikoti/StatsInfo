using System.Windows;
using System.Windows.Media;
using StatsInfo.Models;

namespace StatsInfo.Views;

public sealed class MetricSparkline : FrameworkElement
{
    public static readonly DependencyProperty SamplesProperty = DependencyProperty.Register(nameof(Samples),
        typeof(IReadOnlyList<MetricSample>), typeof(MetricSparkline), new FrameworkPropertyMetadata(Array.Empty<MetricSample>(),
            FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty LineBrushProperty = DependencyProperty.Register(nameof(LineBrush),
        typeof(Brush), typeof(MetricSparkline), new FrameworkPropertyMetadata(Brushes.CornflowerBlue,
            FrameworkPropertyMetadataOptions.AffectsRender));
    public IReadOnlyList<MetricSample> Samples
    {
        get => (IReadOnlyList<MetricSample>)GetValue(SamplesProperty);
        set => SetValue(SamplesProperty, value);
    }
    public Brush LineBrush
    {
        get => (Brush)GetValue(LineBrushProperty);
        set => SetValue(LineBrushProperty, value);
    }
    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);
        var numeric = Samples.Where(s => s.IsNumeric).ToArray();
        if (numeric.Length < 2 || ActualWidth < 2 || ActualHeight < 2) return;
        double min = numeric.Min(s => s.Value!.Value), max = numeric.Max(s => s.Value!.Value);
        double range = Math.Max(1, max - min);
        double seconds = Math.Max(1, (Samples.Last().SampledAt - Samples.First().SampledAt).TotalSeconds);
        var pen = new Pen(LineBrush, 1.2);
        Point? previous = null;
        DateTimeOffset? previousTime = null;
        foreach (var sample in Samples.TakeLast(120))
        {
            if (!sample.IsNumeric) { previous = null; previousTime = null; continue; }
            double x = (ActualWidth - 2) * (sample.SampledAt - Samples.First().SampledAt).TotalSeconds / seconds + 1;
            double y = ActualHeight - 1 - (ActualHeight - 2) * (sample.Value!.Value - min) / range;
            var point = new Point(x, y);
            if (previousTime is { } time && (sample.SampledAt - time).TotalSeconds > 5) previous = null;
            if (previous is { } p) context.DrawLine(pen, p, point);
            previous = point;
            previousTime = sample.SampledAt;
        }
    }
}
