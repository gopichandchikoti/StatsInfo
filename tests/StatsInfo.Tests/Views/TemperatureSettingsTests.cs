using System.IO;
using System.Windows;
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
public class TemperatureSettingsTests
{
    [Fact]
    public void EnableTemperatureSensors_TurnsOnHardwareMonitoringAndSavesIt()
    {
        MainWindowSettingsTests.WpfTestHost.Run(() =>
        {
            string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            var settings = new SettingsService(Path.Combine(directory, "settings.json"));
            Assert.False(settings.Settings.HardwareSensorsEnabled);
            var window = new SettingsWindow(new SettingsViewModel(settings), dialogs: new AcceptingDialogs());
            try
            {
                window.Show(); window.UpdateLayout();
                // The button sits in the Metrics tab toolbar, which is selected when Settings opens.
                var button = Descendants<Button>(window).Single(b => b.Content?.ToString() == "Enable temperature sensors");
                button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                Assert.True(new SettingsService(settings.SettingsPath).Settings.HardwareSensorsEnabled);
                Assert.Contains(Descendants<TextBlock>(window), t => t.Text.StartsWith("Temperature sensors enabled"));

                // Tab content only enters the visual tree once its tab is selected. The Hardware tab explains
                // the state of each temperature row instead of staying blank before any reading arrives.
                var tabs = Descendants<TabControl>(window).Single();
                tabs.SelectedItem = tabs.Items.OfType<TabItem>().Single(t => t.Header?.ToString() == "Hardware & Diagnostics");
                window.UpdateLayout();
                Assert.Contains(Descendants<TextBlock>(window), t => t.Text.Contains("CPU temp") && t.Text.Contains("GPU temp"));
            }
            finally { window.Close(); Directory.Delete(directory, true); }
        });
    }

    [Fact]
    public void ThemeService_FallsBackToTheDefaultAccent_AndHonoursSystemAndHighContrastThemes()
    {
        MainWindowSettingsTests.WpfTestHost.Run(() =>
        {
            var window = new Window();
            ThemeService.Apply(window, new AppSettings { Theme = "System", AccentColor = "not-a-colour" });
            Assert.Same(Brushes.CornflowerBlue, window.Resources["OverlayAccent"]);
            Assert.IsType<SolidColorBrush>(window.Resources["PageBackground"]);
            ThemeService.Apply(window, new AppSettings { Theme = "High Contrast" });
            Assert.Same(SystemColors.WindowBrush, window.Resources["PageBackground"]);
            Assert.Same(SystemColors.HighlightBrush, window.Resources["OverlayAccent"]);
        });
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T typed) yield return typed;
            foreach (var descendant in Descendants<T>(child)) yield return descendant;
        }
    }

    private sealed class AcceptingDialogs : ISettingsDialogs
    {
        public bool Confirm(Window owner, string text, string title) => true;
        public string? ExportPath(Window owner, bool json) => null;
    }
}
