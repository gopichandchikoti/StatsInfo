using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using StatsInfo.Models;
using StatsInfo.Services;
using StatsInfo.ViewModels;

namespace StatsInfo.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;
    private readonly MetricRuntime? _runtime;
    private readonly ISettingsDialogs _dialogs;
    private AppSettings _draft;
    private readonly List<StackPanel> _draftPanels = new();
    private readonly DispatcherTimer _timer;
    private readonly Dictionary<string, TextBox> _categoryEditors = new();
    private TextBlock _diagnostics = new();
    private TextBlock _overhead = new();
    private TextBlock _captureStatus = new();
    private ComboBox _processes = new();
    private ComboBox _alertMetric = new();
    private StackPanel _alertEditor = new();
    public event Action? RecoveryRequested;

    public SettingsWindow(SettingsViewModel viewModel, MetricRuntime? runtime = null, ISettingsDialogs? dialogs = null)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _runtime = runtime;
        _dialogs = dialogs ?? new SettingsDialogs();
        _draft = CloneSettings(viewModel.Service.Settings);
        DataContext = viewModel;
        BuildPanels();
        // Subscribe only after construction succeeds, so a failed optional discovery cannot retain this window.
        viewModel.RequestClose += Close;
        SaveState.Text = viewModel.Service.LastError ?? SaveState.Text;
        _timer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background,
            (_, _) => RefreshState(), Dispatcher);
        Closed += (_, _) =>
        {
            _timer.Stop();
            viewModel.RequestClose -= Close;
            if (_runtime != null) _runtime.DiagnosticsOpen = false;
        };
        RefreshState();
        _timer.Start();
    }

    private void AddPanel(string title, Action<SettingsPanelBuilder> build)
    {
        var builder = new SettingsPanelBuilder(title, _draft);
        _draftPanels.Add(builder.Panel);
        build(builder);
        Navigation.Items.Add(new TabItem
        {
            Header = title,
            Content = new ScrollViewer
            { Content = builder.Panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }
        });
    }

    private void RefreshState()
    {
        if (_viewModel.Service.LastError != null) SaveState.Text = _viewModel.Service.LastError;
        else if (_runtime?.PersistenceError != null) SaveState.Text = _runtime.PersistenceError;
        _viewModel.RefreshMetrics(_runtime);
        _temperatureStatus.Text = string.Join("\n", _viewModel.Metrics
            .Where(m => m.Id is "cpu.temperature.package" or "gpu.temperature.core")
            .Select(m => $"{m.DisplayLabel}: {m.ValueText}. {m.AvailabilityReason}"));
        _captureStatus.Text = _runtime?.Source<PresentMonSource>()?.Status ?? "No frame capture source connected.";
        _overhead.Text = _runtime == null ? "No runtime connected." :
            string.Join("\n", _runtime.CollectorMilliseconds.OrderBy(p => p.Key)
                .Select(p => $"{p.Key}: last read {p.Value:F1} ms (wall time, not process CPU)."));
        if (_runtime?.DiagnosticsOpen == true)
            _diagnostics.Text = string.Join("\n\n", _runtime.Snapshot().Select(p =>
                $"{p.Key}: {p.Value.Quality}\n{p.Value.Source}\n" +
                $"{p.Value.SampledAt.ToLocalTime():HH:mm:ss} {p.Value.Reason}"));
    }

    private async void Navigation_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_runtime != null && ReferenceEquals(e.Source, Navigation))
        {
            _runtime.DiagnosticsOpen = (Navigation.SelectedItem as TabItem)?.Header?.ToString() == "Hardware & Diagnostics";
            if (_runtime.DiagnosticsOpen) await ReadInventory();
        }
    }

    private void ApplySettings_Click(object sender, RoutedEventArgs e)
    {
        if (HasValidationError(this))
        {
            SaveState.Text = "Correct the highlighted numeric fields before applying settings.";
            return;
        }
        foreach (var pair in _categoryEditors)
        {
            if (!int.TryParse(pair.Value.Text, out var interval) || interval < 500 || interval > 60000)
            { SaveState.Text = $"{pair.Key}: enter 500–60000 ms."; return; }
            _draft.CategoryIntervals[pair.Key] = interval;
        }
        var error = SettingsValidation.Validate(_draft);
        if (error != null) { SaveState.Text = error; return; }
        var current = _viewModel.Service.Settings;
        _draft.OverlayWidth = _draft.Layout == current.Layout ? current.OverlayWidth : 0;
        _draft.OverlayHeight = _draft.Layout == current.Layout ? current.OverlayHeight : 0;
        _draft.Metrics = current.Metrics;
        _draft.SavedCustomVisibility = current.SavedCustomVisibility;
        _draft.Preset = current.Preset;
        foreach (var d in MetricRegistry.All) MetricSettings.SetVisible(_draft, d.Id, current.Metrics[d.Id].Visible);
        _viewModel.Service.SaveSettings(_draft);
        _draft = CloneSettings(_viewModel.Service.Settings);
        foreach (var panel in _draftPanels) panel.DataContext = _draft;
        SelectAlert();
        ThemeService.Apply(this, _viewModel.Service.Settings);
        string? shellError = Application.Current?.Windows.OfType<MainWindow>().FirstOrDefault()?.ShellError;
        SaveState.Text = _viewModel.Service.LastError ?? shellError ?? "Settings saved.";
    }

    private static bool HasValidationError(DependencyObject root)
    {
        if (Validation.GetHasError(root)) return true;
        return LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>().Any(HasValidationError);
    }

    private static AppSettings CloneSettings(AppSettings settings) =>
        System.Text.Json.JsonSerializer.Deserialize<AppSettings>(System.Text.Json.JsonSerializer.Serialize(settings))!;

    public void CaptureSections(string directory)
    {
        int selected = Navigation.SelectedIndex;
        for (int index = 0; index < Navigation.Items.Count; index++)
        {
            Navigation.SelectedIndex = index;
            UpdateLayout();
            RefreshState();
            LocalVerification.Capture(this, System.IO.Path.Combine(directory, $"settings-{index:D2}.png"));
        }
        Navigation.SelectedIndex = selected;
    }

    private void SetGroup(bool enabled)
    {
        string group = GroupSelection.SelectedItem?.ToString() ?? "CPU";
        if (!_dialogs.Confirm(this, $"{(enabled ? "Show" : "Hide")} all metrics in {group}?", "Visible Metrics")) return;
        foreach (var metric in _viewModel.Metrics.Where(m => m.Group == group))
            MetricSettings.SetVisible(_viewModel.Service.Settings, metric.Id, enabled);
        _viewModel.Service.Settings.Preset = "Custom";
        _viewModel.Service.SaveSettings(_viewModel.Service.Settings);
        RefreshState();
    }
    private void ShowGroup_Click(object sender, RoutedEventArgs e) => SetGroup(true);
    private void HideGroup_Click(object sender, RoutedEventArgs e) => SetGroup(false);
    private void ApplyPreset_Click(object sender, RoutedEventArgs e)
    {
        string preset = PresetSelection.SelectedItem?.ToString() ?? "Custom";
        if (!_dialogs.Confirm(this, $"Apply {preset}? Your current Custom visibility is saved and can be restored with Custom. " +
            "Monitor, appearance, order and hotkeys are retained.", "Choose preset")) return;
        MetricSettings.ApplyPreset(_viewModel.Service.Settings, preset);
        _viewModel.Service.SaveSettings(_viewModel.Service.Settings);
        RefreshState();
    }
    private void MoveUp_Click(object sender, RoutedEventArgs e)
    { if ((sender as Button)?.Tag is MetricItemViewModel metric) _viewModel.MoveMetric(metric, -1); }
    private void MoveDown_Click(object sender, RoutedEventArgs e)
    { if ((sender as Button)?.Tag is MetricItemViewModel metric) _viewModel.MoveMetric(metric, 1); }
    private void ResetMetric_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not MetricItemViewModel metric) return;
        var pref = _viewModel.Service.Settings.Metrics[metric.Id];
        pref.Unit = "";
        pref.Label = "";
        pref.Decimals = -1;
        pref.Sparkline = false;
        pref.Order = metric.Definition.SortOrder;
        MetricSettings.SetVisible(_viewModel.Service.Settings, metric.Id, metric.Definition.DefaultVisibility);
        _viewModel.Service.SaveSettings(_viewModel.Service.Settings);
        RefreshState();
    }
}
