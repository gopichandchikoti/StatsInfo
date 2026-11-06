using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using StatsInfo.Services;
using StatsInfo.ViewModels;
using StatsInfo.Views;
using Xunit;

namespace StatsInfo.Tests.Views;

[Collection("WPF UI")]
public class SettingsRecoveryTests
{
    [Fact]
    public void Settings_ResetMetricRestoresSavedDefaults_AndDisconnectedHistoryExplainsAvailability()
    {
        MainWindowSettingsTests.WpfTestHost.Run(() =>
        {
            string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            var settings = new SettingsService(Path.Combine(directory, "settings.json"));
            var vm = new SettingsViewModel(settings);
            var cpu = vm.Metrics.Single(m => m.Id == "cpu.usage.total");
            cpu.CustomLabel = "Processor";
            cpu.Decimals = 2;
            cpu.ShowSparkline = true;
            cpu.IsEnabled = false;
            vm.Search = "cpu.usage.total";
            var window = new SettingsWindow(vm, dialogs: new TestDialogs());
            try
            {
                window.Show(); window.UpdateLayout();
                var reset = Descendants<Button>(window).Single(b =>
                    AutomationProperties.GetName(b) == "Reset metric" && ReferenceEquals(b.Tag, cpu));
                reset.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                var preference = new SettingsService(settings.SettingsPath).Settings.Metrics[cpu.Id];
                Assert.Equal("", preference.Label);
                Assert.Equal("", preference.Unit);
                Assert.Equal(-1, preference.Decimals);
                Assert.False(preference.Sparkline);
                Assert.True(preference.Visible);
                Assert.Equal(cpu.Definition.SortOrder, preference.Order);

                var navigation = Descendants<TabControl>(window).Single();
                navigation.SelectedIndex = 5; window.UpdateLayout();
                foreach (string action in new[] { "Open history", "Export CSV…", "Export JSON…" })
                {
                    Click(window, action);
                    Assert.Contains(Descendants<TextBlock>(window), t => t.Text == "No metric runtime connected.");
                    Assert.True(window.IsVisible);
                    Assert.Empty(Application.Current.Windows.OfType<HistoryWindow>());
                }
                Click(window, "Clear history…");
                Assert.Contains(Descendants<TextBlock>(window), t => t.Text == "History cleared.");
            }
            finally { window.Close(); Directory.Delete(directory, true); }
        });
    }

    private static void Click(Window window, string label) => Descendants<Button>(window)
        .Single(b => b.Content?.ToString() == label).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T typed) yield return typed;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }

    private sealed class TestDialogs : ISettingsDialogs
    {
        public bool Confirm(Window owner, string text, string title) => true;
        public string? ExportPath(Window owner, bool json) => null;
    }
}
