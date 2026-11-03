using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using StatsInfo.Services;
using StatsInfo.ViewModels;
using StatsInfo.Views;
using Xunit;

namespace StatsInfo.Tests.Views;

[Collection("WPF UI")]
public class MainWindowSettingsTests
{
    [Fact]
    public void SettingsButton_OpensSettingsWindow_WithoutClosingOverlay()
    {
        WpfTestHost.Run(() =>
        {
            using var coordinator = new SamplingCoordinator(Array.Empty<IMetricCollector>());
            var settingsDirectory = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}");
            var settingsService = new SettingsService(Path.Combine(settingsDirectory, "settings.json"));
            var overlayViewModel = new OverlayViewModel(coordinator, settingsService);
            var settingsViewModel = new SettingsViewModel(settingsService);
            var mainWindow = new MainWindow(overlayViewModel, settingsViewModel);

            try
            {
                mainWindow.Show();
                Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
                var button = FindButton(mainWindow, "Settings")
                    ?? throw new InvalidOperationException("Settings button was not found.");

                button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));

                var settingsWindow = Assert.Single(Application.Current.Windows
                    .OfType<SettingsWindow>());
                Assert.True(settingsWindow.IsVisible);
                Assert.True(mainWindow.IsVisible);
                Assert.Same(settingsViewModel, settingsWindow.DataContext);
                Assert.NotNull(settingsWindow.Icon);

                button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
                Assert.Single(Application.Current.Windows.OfType<SettingsWindow>());

                settingsViewModel.CancelCommand.Execute(null);
                Assert.False(settingsWindow.IsVisible);
                Assert.True(mainWindow.IsVisible);

                button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, button));
                Assert.Single(Application.Current.Windows.OfType<SettingsWindow>());

                settingsViewModel.ShowCpu = false;
                settingsViewModel.SaveCommand.Execute(null);
                Assert.False(overlayViewModel.ShowCpu);
                Assert.True(mainWindow.IsVisible);

                var closeButton = FindButton(mainWindow, "Close StatsInfo")
                    ?? throw new InvalidOperationException("Close button was not found.");
                closeButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent, closeButton));
                Assert.False(mainWindow.IsVisible);
            }
            finally
            {
                foreach (Window window in Application.Current.Windows.Cast<Window>().ToArray())
                {
                    window.Close();
                }

                if (Directory.Exists(settingsDirectory))
                {
                    Directory.Delete(settingsDirectory, recursive: true);
                }
            }
        });
    }

    private static Button? FindButton(DependencyObject parent, string name)
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is Button button &&
                System.Windows.Automation.AutomationProperties.GetName(button) == name)
            {
                return button;
            }

            var descendantButton = FindButton(child, name);
            if (descendantButton != null)
            {
                return descendantButton;
            }
        }

        return null;
    }

    public static class WpfTestHost
    {
        private static readonly Lazy<Dispatcher> TestDispatcher = new(Start);

        public static void Run(Action action)
        {
            TestDispatcher.Value.Invoke(action);
        }

        private static Dispatcher Start()
        {
            using var initialized = new ManualResetEventSlim();
            Dispatcher? dispatcher = null;
            var thread = new Thread(() =>
            {
                _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                dispatcher = Dispatcher.CurrentDispatcher;
                initialized.Set();
                Dispatcher.Run();
            })
            {
                IsBackground = true,
                Name = "WPF test dispatcher"
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            initialized.Wait();
            return dispatcher!;
        }
    }
}
