using System.Windows;
using System.Windows.Input;
using StatsInfo.ViewModels;
using StatsInfo.Views;

namespace StatsInfo;

public partial class MainWindow : Window
{
    private readonly OverlayViewModel _viewModel;
    private readonly SettingsViewModel _settingsViewModel;
    private SettingsWindow? _settingsWindow;

    public MainWindow(OverlayViewModel viewModel, SettingsViewModel settingsViewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _settingsViewModel = settingsViewModel;
        DataContext = _viewModel;

        _viewModel.RequestOpenSettings += ShowSettings;

        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Default position: top center of primary screen
        double screenWidth = SystemParameters.PrimaryScreenWidth;
        Left = (screenWidth - Width) / 2;
        Top = 16;
    }

    private void Window_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void ShowSettings()
    {
        if (_settingsWindow == null || !_settingsWindow.IsLoaded)
        {
            _settingsWindow = new SettingsWindow(_settingsViewModel);
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.Show();
        }
        else
        {
            _settingsWindow.Activate();
        }
    }
}
