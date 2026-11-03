using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using StatsInfo.Helpers;
using StatsInfo.Models;
using StatsInfo.Services;
using StatsInfo.ViewModels;
using StatsInfo.Views;

namespace StatsInfo;

public partial class MainWindow : Window
{
    private readonly SettingsViewModel _settingsViewModel;
    private SettingsWindow? _settingsWindow;
    private readonly OverlayViewModel _viewModel;
    private bool _dragging;

    public MainWindow(OverlayViewModel viewModel, SettingsViewModel settingsViewModel)
    {
        InitializeComponent();
        _settingsViewModel = settingsViewModel;
        _viewModel = viewModel;
        DataContext = viewModel;

        Loaded += OnLoaded;
        viewModel.Settings.SettingsChanged += SettingsChanged;
        Closed += (_, _) => CleanupShell();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        InitializeShell();
        ApplyAppearance(_viewModel.Settings.Settings);
    }

    private void DragHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.Settings.Settings.LockPosition || e.ChangedButton != MouseButton.Left) return;
        if (e.OriginalSource is DependencyObject target && FindButton(target) != null) return;
        try
        {
            _dragging = true;
            DragMove();
            var s = _viewModel.Settings.Settings;
            s.Position = "Custom";
            s.CustomX = Left;
            s.CustomY = Top;
            var monitors = DisplayInterop.Monitors();
            var monitor = monitors.ElementAtOrDefault(Math.Clamp(s.TargetMonitorIndex, 0, Math.Max(0, monitors.Length - 1)));
            if (monitor != null) s.MonitorPositions[monitor.Device] = new() { X = Left, Y = Top };
            _viewModel.Settings.SaveSettings(s);
        }
        catch (InvalidOperationException) { /* Mouse capture may be lost during display changes. */ }
        finally { _dragging = false; }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        ShowSettings();
    }

    public void ShowSettings()
    {
        if (_settingsWindow == null || !_settingsWindow.IsLoaded)
        {
            _settingsWindow = new SettingsWindow(_settingsViewModel, _viewModel.Runtime);
            _settingsWindow.RecoveryRequested += RecoverOverlay;
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
        }
        else
        {
            _settingsWindow.Activate();
        }
    }

    private void SettingsChanged(AppSettings settings)
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(() => SettingsChanged(settings)); return; }
        ApplyAppearance(settings);
        ApplyShellSettings(settings);
    }

    private void ApplyAppearance(AppSettings s)
    {
        ThemeService.Apply(this, s);
        OverlayBorder.Opacity = SystemParameters.HighContrast || s.Theme == "High Contrast" ? 1 : s.Opacity;
        FontSize = s.FontSize;
        FontFamily = Fonts.SystemFontFamilies.FirstOrDefault(f => f.Source.Equals(s.FontFamily,
            StringComparison.OrdinalIgnoreCase)) ?? new FontFamily("Segoe UI");
        Resources["MetricSpacing"] = new Thickness(0, 0, s.MetricSpacing, 0);
        OverlayBorder.LayoutTransform = new ScaleTransform(s.Scale, s.Scale);
        OverlayBorder.Padding = s.Density == "Compact" ? new Thickness(6, 3, 6, 3) : new Thickness(12, 6, 12, 6);
        bool compact = s.Layout == "Compact Gaming";
        var panel = new FrameworkElementFactory(compact ? typeof(StackPanel) : typeof(WrapPanel));
        if (compact) panel.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        else panel.SetValue(WrapPanel.OrientationProperty, s.Layout == "Vertical" ? Orientation.Vertical : Orientation.Horizontal);
        MetricItems.ItemsPanel = new ItemsPanelTemplate(panel);
        Grid.SetColumnSpan(MetricScroller, compact ? 1 : 2);
        Grid.SetRow(OverlayFooter, compact ? 0 : 1);
        Grid.SetColumn(OverlayFooter, compact ? 1 : 0);
        Grid.SetColumnSpan(OverlayFooter, compact ? 1 : 2);
        StatusText.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        MetricScroller.HorizontalScrollBarVisibility = compact ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
        MetricScroller.VerticalScrollBarVisibility = compact ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
        var monitors = DisplayInterop.Monitors();
        if (monitors.Length == 0) return;
        var monitor = monitors[Math.Clamp(s.TargetMonitorIndex, 0, monitors.Length - 1)];
        double maxWidth = (monitor.WorkRight - monitor.WorkLeft) / monitor.DpiScale - 24;
        Width = Math.Min(maxWidth, s.Layout == "Vertical" ? 340 : s.Layout == "Expanded" ? 1050 : 850);
        MaxHeight = Math.Min(240 * s.Scale, (monitor.WorkBottom - monitor.WorkTop) / monitor.DpiScale / 3);
        if (!_dragging) PositionOverlay(s, monitor);
    }

    private void PositionOverlay(AppSettings s, MonitorInfo monitor)
    {
        var custom = s.MonitorPositions.GetValueOrDefault(monitor.Device);
        double customX = custom?.X ?? s.CustomX, customY = custom?.Y ?? s.CustomY;
        if (!double.IsFinite(customX)) customX = 100;
        if (!double.IsFinite(customY)) customY = 20;
        double left = monitor.WorkLeft / monitor.DpiScale, right = monitor.WorkRight / monitor.DpiScale;
        double top = monitor.WorkTop / monitor.DpiScale;
        double x = s.Position switch
        {
            "TopLeft" => left + 12,
            "TopRight" => right - Width - 12,
            "Custom" => customX,
            _ => left + (right - left - Width) / 2
        };
        Left = Math.Clamp(x, left + 6, Math.Max(left + 6, right - Width - 6));
        Top = Math.Clamp(s.Position == "Custom" ? customY : top + 12, top + 6,
            Math.Max(top + 6, monitor.WorkBottom / monitor.DpiScale - Math.Max(ActualHeight, 72) - 6));
    }

    private void Metric_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.Runtime != null && (sender as FrameworkElement)?.DataContext is MetricItemViewModel metric)
            new HistoryWindow(_viewModel.Runtime, _viewModel.Settings, metric.Id).Show();
    }
    private static Button? FindButton(DependencyObject target)
    {
        while (target != null)
        {
            if (target is Button button) return button;
            target = VisualTreeHelper.GetParent(target);
        }
        return null;
    }
}
