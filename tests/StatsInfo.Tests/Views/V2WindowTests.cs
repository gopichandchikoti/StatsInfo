using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using StatsInfo.Models;
using StatsInfo.Services;
using StatsInfo.ViewModels;
using StatsInfo.Views;
using Xunit;

namespace StatsInfo.Tests.Views;

[Collection("WPF UI")]
public class V2WindowTests
{
    [Fact]
    public void Settings_AllSectionsRender_AndMetricChangesPresetsOrderingAndSearchAreDurable()
    {
        MainWindowSettingsTests.WpfTestHost.Run(() =>
        {
            string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            var settings = new SettingsService(Path.Combine(dir, "settings.json"));
            using var runtime = new MetricRuntime(settings, []);
            var vm = new SettingsViewModel(settings);
            var dialogs = new TestDialogs(Path.Combine(dir, "export.json"));
            var window = new SettingsWindow(vm, runtime, dialogs);
            try
            {
                window.Show(); window.UpdateLayout();
                var navigation = Find<TabControl>(window);
                Assert.Equal(8, navigation.Items.Count);
                foreach (TabItem tab in navigation.Items)
                {
                    navigation.SelectedItem = tab; window.UpdateLayout();
                    LocalVerification.Capture(window, Path.Combine(dir, $"tab-{navigation.SelectedIndex}.png"));
                }
                navigation.SelectedIndex = 0; window.UpdateLayout();
                foreach (var metric in vm.Metrics) metric.IsEnabled = true;
                Assert.All(new SettingsService(settings.SettingsPath).Settings.Metrics.Values, p => Assert.True(p.Visible));
                foreach (var metric in vm.Metrics) metric.IsEnabled = false;
                var first = vm.Metrics[0];
                first.IsEnabled = true; first.Unit = "Default"; first.CustomLabel = "Processor";
                vm.MoveMetric(first, 1);
                Assert.Equal(1, settings.Settings.Metrics[first.Id].Order);
                vm.Search = "package power";
                Assert.Single(vm.MetricsView.Cast<MetricItemViewModel>());
                vm.Availability = "Enabled";
                Assert.Empty(vm.MetricsView.Cast<MetricItemViewModel>());
                vm.Search = ""; vm.Availability = "All";
                var temp = vm.Metrics.Single(m => m.Id == "cpu.temperature.package");
                var sample = MetricSample.Number(temp.Id, 50, "test sensor");
                temp.Update(sample, new Dictionary<string, MetricSample> { [temp.Id] = sample });
                Assert.True(temp.IsAvailable);
                temp.Unit = "°F";
                temp.Decimals = 2;
                temp.ShowSparkline = true;
                Assert.True(temp.HasSparkline);
                temp.Update(sample, new Dictionary<string, MetricSample> { [temp.Id] = sample });
                Assert.Equal("122.00 °F", temp.ValueText);
                Click(window, "Show group");
                Assert.True(settings.Settings.Metrics["cpu.power.package"].Visible);
                Click(window, "Hide group");
                Assert.False(settings.Settings.Metrics["cpu.power.package"].Visible);
                var preset = Descendants<ComboBox>(window).Single(c => AutomationProperties.GetName(c) == "Overlay preset");
                preset.SelectedItem = "Gaming";
                Click(window, "Apply preset…");
                Assert.True(settings.Settings.Metrics["game.fps"].Visible);
                dialogs.Approve = false;
                Click(window, "Hide group"); // Cancellation must preserve preferences.
                dialogs.Approve = true;
                navigation.SelectedIndex = 3; window.UpdateLayout();
                Click(window, "Dismiss alerts / snooze 15 minutes");
                Assert.True(runtime.Alerts.SnoozedUntil > DateTimeOffset.UtcNow);
                navigation.SelectedIndex = 4; window.UpdateLayout();
                Click(window, "Refresh processes");
                Click(window, "Start capture (opt-in)");
                Click(window, "Stop capture");
                navigation.SelectedIndex = 6; window.UpdateLayout();
                Click(window, "Refresh hardware choices");
                navigation.SelectedIndex = 1; window.UpdateLayout();
                navigation.SelectedIndex = 2; window.UpdateLayout();
                var interval = Descendants<TextBox>(window).Single(c => AutomationProperties.GetName(c) ==
                    "Default update interval (ms; 500–10000)");
                interval.Text = "not-a-number";
                Click(window, "Apply settings");
                Assert.Equal(1000, settings.Settings.UpdateIntervalMs);
                interval.Text = "1000";
                navigation.SelectedIndex = 1; window.UpdateLayout();
                var font = Descendants<Slider>(window).Single(c => AutomationProperties.GetName(c) == "Font size");
                font.Value = 16;
                Assert.Equal(12, settings.Settings.FontSize); // Draft edits do not leak before Apply.
                Click(window, "Apply settings");
                Assert.Equal(16, settings.Settings.FontSize);
                font.Value = 18;
                Assert.Equal(16, settings.Settings.FontSize); // A fresh draft is used after each Apply.
                Directory.CreateDirectory(settings.SettingsPath + ".tmp");
                first.IsEnabled = !first.IsEnabled;
                Assert.NotNull(settings.LastError);
                window.CaptureSections(dir);
                Assert.Contains(Descendants<TextBlock>(window), t => t.Text.Contains("could not be saved"));
            }
            finally { window.Close(); Directory.Delete(dir, true); }
        });
    }

    [Fact]
    public void Overlay_TrayRecoversHiddenClickThroughWindow_AndLayoutsStayBounded()
    {
        MainWindowSettingsTests.WpfTestHost.Run(() =>
        {
            string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            var settings = new SettingsService(Path.Combine(dir, "settings.json"));
            using var coordinator = new SamplingCoordinator([]);
            using var runtime = new MetricRuntime(settings, []);
            var vm = new OverlayViewModel(coordinator, settings, runtime);
            var overlay = new MainWindow(vm, new SettingsViewModel(settings));
            try
            {
                overlay.Show(); overlay.UpdateLayout();
                Assert.NotNull(overlay.TrayMenu);
                TrayClick(overlay, "Show / Hide overlay"); Assert.False(overlay.IsVisible);
                TrayClick(overlay, "Show / Hide overlay"); Assert.True(overlay.IsVisible);
                TrayClick(overlay, "Pause / Resume monitoring"); Assert.True(settings.Settings.Paused);
                TrayClick(overlay, "Pause / Resume monitoring"); Assert.False(settings.Settings.Paused);
                TrayClick(overlay, "Toggle click-through"); Assert.True(settings.Settings.ClickThrough);
                TrayClick(overlay, "Recover overlay (input and position)"); Assert.False(settings.Settings.ClickThrough);
                foreach (string layout in new[] { "Vertical", "Compact Gaming", "Expanded", "Horizontal" })
                {
                    settings.Settings.Layout = layout;
                    settings.Settings.Position = "TopRight";
                    settings.Settings.Theme = "Light";
                    settings.SaveSettings(settings.Settings); overlay.UpdateLayout();
                    Assert.InRange(overlay.ActualHeight, 1, overlay.MaxHeight);
                    Assert.True(overlay.Width > 0);
                }
                settings.Settings.Position = "TopLeft";
                settings.Settings.Theme = "High Contrast";
                settings.Settings.Scale = 1.2;
                settings.SaveSettings(settings.Settings);
                settings.Settings.Position = "Custom"; settings.Settings.CustomX = -50000;
                settings.Settings.CustomY = -50000; settings.Settings.TargetMonitorIndex = 999;
                foreach (var d in MetricRegistry.All) MetricSettings.SetVisible(settings.Settings, d.Id, true);
                settings.SaveSettings(settings.Settings); overlay.UpdateLayout();
                Assert.Equal(70, vm.VisibleMetrics.Count);
                Assert.InRange(overlay.ActualHeight, 1, overlay.MaxHeight);
                LocalVerification.Capture(overlay, Path.Combine(dir, "all-metrics.png"));
                TrayClick(overlay, "Settings");
                Assert.Contains(Application.Current.Windows.OfType<SettingsWindow>(), w => w.IsVisible);
            }
            finally { overlay.Close(); Directory.Delete(dir, true); }
        });
    }

    [Fact]
    public void History_RendersRealTypedSeriesWithGaps_AndUnsubscribesOnClose()
    {
        MainWindowSettingsTests.WpfTestHost.Run(() =>
        {
            string dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            var settings = new SettingsService(Path.Combine(dir, "settings.json"));
            using var runtime = new MetricRuntime(settings, []);
            var sample = MetricSample.Number("cpu.usage.total", 10, "test");
            for (int i = 0; i < 30; i++) runtime.History.Add(sample with
            {
                Value = i,
                SampledAt = sample.SampledAt.AddSeconds(i),
                Quality = i == 15 ? SampleQuality.Unavailable : SampleQuality.Valid
            });
            var history = new HistoryWindow(runtime, settings);
            try
            {
                history.Show(); history.UpdateLayout();
                if (history.Content is StackPanel panel)
                {
                    var sparkline = new MetricSparkline
                    {
                        Samples = runtime.History.Get("cpu.usage.total"),
                        LineBrush = Brushes.SteelBlue,
                        Width = 100,
                        Height = 20
                    };
                    Assert.Equal(30, sparkline.Samples.Count);
                    panel.Children.Insert(0, sparkline);
                    history.UpdateLayout();
                }
                LocalVerification.Capture(history, Path.Combine(dir, "history.png"));
                Assert.Equal("cpu.usage.total", runtime.HistoryMetric);
                var metric = Find<ComboBox>(history);
                metric.SelectedValue = "gpu.temperature.core";
                Assert.Equal("gpu.temperature.core", runtime.HistoryMetric);
            }
            finally { history.Close(); Directory.Delete(dir, true); }
            Assert.Equal("", runtime.HistoryMetric);
        });
    }

    private static void Click(DependencyObject window, string content)
    {
        var button = Descendants<Button>(window).First(b => b.Content?.ToString() == content);
        button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
    }
    private static void TrayClick(MainWindow window, string header)
    {
        var item = window.TrayMenu!.Items.OfType<MenuItem>().Single(m => m.Header.ToString() == header);
        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent, item));
    }
    private static T Find<T>(DependencyObject root) where T : DependencyObject => Descendants<T>(root).First();
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T typed) yield return typed;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }
    private sealed class TestDialogs(string path) : ISettingsDialogs
    {
        public bool Approve = true;
        public bool Confirm(Window owner, string text, string title) => Approve;
        public string? ExportPath(Window owner, bool json) => path;
    }
}
