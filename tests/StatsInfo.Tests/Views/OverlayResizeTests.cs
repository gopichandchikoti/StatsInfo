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
public class OverlayResizeTests
{
    [Fact]
    public void Resize_ReflowsEverySelectedMetric_AndShrinksFurtherWithOnlyCpu()
    {
        MainWindowSettingsTests.WpfTestHost.Run(() =>
        {
            string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            var settings = new SettingsService(Path.Combine(directory, "settings.json"));
            foreach (var definition in MetricRegistry.All) MetricSettings.SetVisible(settings.Settings, definition.Id, false);
            foreach (string id in new[] { "cpu.usage.total", "gpu.usage.total", "memory.physical.used" })
                MetricSettings.SetVisible(settings.Settings, id, true);
            using var coordinator = new SamplingCoordinator([]);
            var overlay = new MainWindow(new OverlayViewModel(coordinator, settings), new SettingsViewModel(settings));
            try
            {
                overlay.Show(); overlay.UpdateLayout();
                overlay.ResizeOverlay(1000, 40); overlay.UpdateLayout();
                var panel = Find<ResponsiveMetricPanel>(overlay);
                var tiles = panel.Children.Cast<UIElement>().ToArray();
                Assert.Equal(3, tiles.Length);
                Assert.All(tiles, t => Assert.Equal(0, VisualTreeHelper.GetOffset(t).Y));
                double wideHeight = overlay.Height;

                overlay.ResizeOverlay(1, wideHeight); overlay.UpdateLayout();
                Assert.True(overlay.Height > wideHeight);
                Assert.True(VisualTreeHelper.GetOffset(tiles[2]).Y > VisualTreeHelper.GetOffset(tiles[0]).Y);
                AssertMetricsFit(overlay, panel);
                double threeHeight = overlay.MinHeight;
                double narrowWidth = overlay.Width;

                // Height-only shrinking must widen the overlay instead of hiding selected tiles.
                overlay.ResizeOverlay(narrowWidth, wideHeight); overlay.UpdateLayout();
                Assert.True(overlay.Width > narrowWidth);
                AssertMetricsFit(overlay, panel);

                MetricSettings.SetVisible(settings.Settings, "gpu.usage.total", false);
                MetricSettings.SetVisible(settings.Settings, "memory.physical.used", false);
                settings.SaveSettings(settings.Settings); overlay.UpdateLayout();
                overlay.ResizeOverlay(1, 1); overlay.UpdateLayout();
                Assert.True(overlay.MinHeight < threeHeight);
                Assert.Single(panel.Children.Cast<UIElement>());
                Assert.True(Find<Button>(overlay).IsVisible);
                AssertMetricsFit(overlay, panel);

                var grip = Find<Thumb>(overlay);
                grip.RaiseEvent(new DragCompletedEventArgs(0, 0, false) { RoutedEvent = Thumb.DragCompletedEvent });
                var saved = new SettingsService(settings.SettingsPath).Settings;
                Assert.Equal(overlay.Width, saved.OverlayWidth);
                Assert.Equal(overlay.Height, saved.OverlayHeight);

                overlay.ShowSettings();
                var window = Application.Current.Windows.OfType<SettingsWindow>().Single();
                window.UpdateLayout();
                var apply = Descendants<Button>(window).Single(b => b.Content?.ToString() == "Apply settings");
                apply.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                overlay.UpdateLayout();
                Assert.Equal(saved.OverlayWidth, overlay.Width);
                Assert.Equal(saved.OverlayHeight, overlay.Height);
                window.Close();

                overlay.Close();
                overlay = new MainWindow(new OverlayViewModel(coordinator, settings), new SettingsViewModel(settings));
                overlay.Show(); overlay.UpdateLayout();
                Assert.Equal(saved.OverlayWidth, overlay.Width);
                Assert.Equal(saved.OverlayHeight, overlay.Height);
            }
            finally { overlay.Close(); Directory.Delete(directory, true); }
        });
    }

    [Fact]
    public void Resize_RespectsScaleAndFont_AndScrollsOnlyWhenMonitorCannotFitEverything()
    {
        MainWindowSettingsTests.WpfTestHost.Run(() =>
        {
            string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            var settings = new SettingsService(Path.Combine(directory, "settings.json"));
            settings.Settings.Scale = 1.5;
            settings.Settings.FontSize = 18;
            foreach (var definition in MetricRegistry.All) MetricSettings.SetVisible(settings.Settings, definition.Id, true);
            using var coordinator = new SamplingCoordinator([]);
            var overlay = new MainWindow(new OverlayViewModel(coordinator, settings), new SettingsViewModel(settings));
            try
            {
                overlay.Show(); overlay.UpdateLayout();
                overlay.ResizeOverlay(1, 1); overlay.UpdateLayout();
                Assert.Equal(70, Find<ResponsiveMetricPanel>(overlay).Children.Count);
                Assert.InRange(overlay.Width, overlay.MinWidth, overlay.MaxWidth);
                Assert.InRange(overlay.Height, overlay.MinHeight, overlay.MaxHeight);
                Assert.Equal(overlay.MaxHeight, overlay.Height);
                Assert.True(Find<ScrollViewer>(overlay).ScrollableHeight > 0);
                overlay.ResizeOverlay(overlay.MaxWidth, overlay.MaxHeight); overlay.UpdateLayout();
                var scroller = Find<ScrollViewer>(overlay);
                Assert.InRange(scroller.ScrollableWidth, 0, 1);
                // On a small CI monitor even the widest arrangement may need vertical scrolling.
                Assert.True(scroller.ViewportHeight > 0);
                Assert.Equal(70, Find<ResponsiveMetricPanel>(overlay).Children.Count);
            }
            finally { overlay.Close(); Directory.Delete(directory, true); }
        });
    }

    [Fact]
    public void ResizeGrip_DragStartAndDelta_KeepTheSizeWithinLimits_AndCompletionSavesIt()
    {
        MainWindowSettingsTests.WpfTestHost.Run(() =>
        {
            string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            var settings = new SettingsService(Path.Combine(directory, "settings.json"));
            using var coordinator = new SamplingCoordinator([]);
            var overlay = new MainWindow(new OverlayViewModel(coordinator, settings), new SettingsViewModel(settings));
            try
            {
                overlay.Show(); overlay.UpdateLayout();
                var grip = Find<Thumb>(overlay);
                double width = overlay.ActualWidth, height = overlay.ActualHeight;
                grip.RaiseEvent(new DragStartedEventArgs(0, 0) { RoutedEvent = Thumb.DragStartedEvent });
                // The pointer has not moved since the drag began, so the overlay stays at its starting size.
                grip.RaiseEvent(new DragDeltaEventArgs(0, 0) { RoutedEvent = Thumb.DragDeltaEvent });
                Assert.InRange(overlay.Width, overlay.MinWidth, overlay.MaxWidth);
                Assert.InRange(overlay.Height, overlay.MinHeight, overlay.MaxHeight);
                Assert.InRange(overlay.Width, width - 2, width + 2);
                Assert.InRange(overlay.Height, height - 2, height + 2);
                grip.RaiseEvent(new DragCompletedEventArgs(0, 0, false) { RoutedEvent = Thumb.DragCompletedEvent });
                var saved = new SettingsService(settings.SettingsPath).Settings;
                Assert.Equal(overlay.Width, saved.OverlayWidth);
                Assert.Equal(overlay.Height, saved.OverlayHeight);
            }
            finally { overlay.Close(); Directory.Delete(directory, true); }
        });
    }

    private static void AssertMetricsFit(Window overlay, Panel panel)
    {
        var scroller = Find<ScrollViewer>(overlay);
        Assert.InRange(scroller.ScrollableWidth, 0, 1);
        Assert.InRange(scroller.ScrollableHeight, 0, 1);
        Assert.All(panel.Children.Cast<UIElement>(), child =>
        {
            var offset = VisualTreeHelper.GetOffset(child);
            Assert.True(offset.X + child.RenderSize.Width <= scroller.ViewportWidth + 1);
            Assert.True(offset.Y + child.RenderSize.Height <= scroller.ViewportHeight + 1);
        });
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
}
