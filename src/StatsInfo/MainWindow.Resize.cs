using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using StatsInfo.Helpers;
using StatsInfo.Models;
using StatsInfo.Views;

namespace StatsInfo;

public partial class MainWindow
{
    private ResponsiveMetricPanel? _metricPanel;
    private Size[] _lastTiles = [];
    private double _lastWidth;
    private double _requestedHeight;
    private bool _sizing, _resizing, _savingSize;
    private Point _resizeOrigin;
    private Size _resizeStart;

    private void ConfigureOverlaySize(AppSettings settings, MonitorInfo monitor)
    {
        if (_savingSize) return;
        MinWidth = MinHeight = 0;
        MaxWidth = Math.Max(80, (monitor.WorkRight - monitor.WorkLeft) / monitor.DpiScale - 24);
        MaxHeight = Math.Max(40, (monitor.WorkBottom - monitor.WorkTop) / monitor.DpiScale - 24);
        double initialWidth = settings.Layout == "Vertical" ? 260 : settings.Layout == "Expanded" ? 1050 : 850;
        Width = Math.Clamp(settings.OverlayWidth > 0 ? settings.OverlayWidth : initialWidth, 80, MaxWidth);
        _requestedHeight = settings.OverlayHeight;
        Height = Math.Clamp(_requestedHeight > 0 ? _requestedHeight : 40, 1, MaxHeight);
        _lastWidth = 0; // Font, scale, density and monitor changes must recalculate the fit.
    }

    private void MetricItems_LayoutUpdated(object? sender, EventArgs e)
    {
        if (_sizing || _resizing || !IsLoaded) return;
        _metricPanel ??= FindPanel(MetricItems);
        if (_metricPanel == null) return;
        var tiles = _metricPanel.TileSizes;
        if (Math.Abs(Width - _lastWidth) < 0.1 && tiles.SequenceEqual(_lastTiles)) return;
        _lastTiles = tiles.ToArray();
        FitOverlay(Width, _requestedHeight, false);
        _lastWidth = Width;
    }

    /// <summary>Fits the selected metrics, trading width for height when a short layout is requested.</summary>
    public void ResizeOverlay(double width, double height)
    {
        UpdateLayout();
        _metricPanel ??= FindPanel(MetricItems);
        _requestedHeight = height;
        FitOverlay(width, height, height < ActualHeight - 0.5 && width >= Width - 0.5);
    }

    private void FitOverlay(double width, double height, bool preferHeight)
    {
        if (_sizing || _metricPanel == null) return;
        _sizing = true;
        try
        {
            var tiles = _metricPanel.TileSizes;
            double scale = _viewModel.Settings.Settings.Scale;
            double horizontal = (OverlayBorder.Padding.Left + OverlayBorder.Padding.Right + 2 +
                Math.Max(65, OverlayFooter.DesiredSize.Width)) * scale;
            double vertical = (OverlayBorder.Padding.Top + OverlayBorder.Padding.Bottom + 2) * scale;
            double widest = tiles.Select(t => t.Width).DefaultIfEmpty(0).Max();
            MinWidth = Math.Min(MaxWidth, Math.Ceiling(horizontal + widest * scale));
            width = Math.Clamp(width, MinWidth, MaxWidth);
            double RequiredHeight(double candidate) => Math.Ceiling(vertical + Math.Max(24,
                ResponsiveMetricPanel.Pack(tiles, Math.Max(1, (candidate - horizontal) / scale)).Height) * scale);

            // Shrinking height grows width until all selected tiles fit, up to the monitor's safe area.
            if (preferHeight && RequiredHeight(width) > height)
            {
                double low = width, high = MaxWidth;
                for (int i = 0; i < 24; i++)
                {
                    double middle = (low + high) / 2;
                    if (RequiredHeight(middle) <= height) high = middle; else low = middle;
                }
                width = Math.Ceiling(high);
            }
            MinHeight = Math.Min(MaxHeight, RequiredHeight(width));
            Width = Math.Min(MaxWidth, width);
            Height = Math.Clamp(Math.Max(height, MinHeight), MinHeight, MaxHeight);
        }
        finally { _sizing = false; }
    }

    private void ResizeHandle_DragStarted(object sender, DragStartedEventArgs e)
    {
        _resizing = true;
        _resizeOrigin = PointToScreen(System.Windows.Input.Mouse.GetPosition(this));
        _resizeStart = new Size(ActualWidth, ActualHeight);
    }

    private void ResizeHandle_DragDelta(object sender, DragDeltaEventArgs e)
    {
        var point = PointToScreen(System.Windows.Input.Mouse.GetPosition(this));
        var dpi = VisualTreeHelper.GetDpi(this);
        ResizeOverlay(_resizeStart.Width + (point.X - _resizeOrigin.X) / dpi.DpiScaleX,
            _resizeStart.Height + (point.Y - _resizeOrigin.Y) / dpi.DpiScaleY);
    }

    private void ResizeHandle_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        _resizing = false;
        var settings = _viewModel.Settings.Settings;
        settings.OverlayWidth = Width;
        settings.OverlayHeight = Height;
        _requestedHeight = Height;
        _savingSize = true;
        try { _viewModel.Settings.SaveSettings(settings); }
        finally { _savingSize = false; }
        var monitors = DisplayInterop.Monitors();
        if (monitors.Length > 0)
            PositionOverlay(settings, monitors[Math.Clamp(settings.TargetMonitorIndex, 0, monitors.Length - 1)]);
    }

    private static ResponsiveMetricPanel? FindPanel(DependencyObject parent)
    {
        if (parent is ResponsiveMetricPanel panel) return panel;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            if (FindPanel(VisualTreeHelper.GetChild(parent, i)) is { } child) return child;
        return null;
    }
}
