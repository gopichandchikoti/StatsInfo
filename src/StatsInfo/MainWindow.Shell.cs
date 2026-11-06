using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Hardcodet.Wpf.TaskbarNotification;
using StatsInfo.Helpers;
using StatsInfo.Models;
using StatsInfo.Services;

namespace StatsInfo;

public partial class MainWindow
{
    private TaskbarIcon? _tray;
    private HwndSource? _source;
    private DispatcherTimer? _shellTimer;
    private string _hotkeys = "";
    private bool _recoveryRegistered;
    private bool _userHidden;
    private bool? _startupApplied;
    private string _monitorSignature = "";
    public string? ShellError { get; private set; }
    public ContextMenu? TrayMenu => _tray?.ContextMenu;

    private void InitializeShell()
    {
        if (_viewModel.Runtime == null) return;
        _source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
        _source?.AddHook(WindowMessage);
        var menu = new ContextMenu();
        AddMenu(menu, "Show / Hide overlay", ToggleOverlay);
        AddMenu(menu, "Settings", ShowSettings);
        AddMenu(menu, "Pause / Resume monitoring", TogglePause);
        var presets = new MenuItem { Header = "Choose preset" };
        foreach (string name in MetricRegistry.Presets.Keys.Append("Custom"))
        {
            var item = new MenuItem { Header = name };
            item.Click += (_, _) => ApplyTrayPreset(name);
            presets.Items.Add(item);
        }
        menu.Items.Add(presets);
        AddMenu(menu, "Toggle click-through", () =>
        {
            var s = _viewModel.Settings.Settings;
            s.ClickThrough = !s.ClickThrough;
            _viewModel.Settings.SaveSettings(s);
        });
        AddMenu(menu, "Recover overlay (input and position)", RecoverOverlay);
        AddMenu(menu, "Exit", Close);
        _tray = new TaskbarIcon
        {
            IconSource = new BitmapImage(new Uri(
            "pack://application:,,,/StatsInfo;component/Assets/StatsInfo.ico")),
            ToolTipText = "StatsInfo 2 · local monitoring",
            ContextMenu = menu
        };
        _tray.TrayMouseDoubleClick += (_, _) => ShowSettings();
        _viewModel.Runtime.AlertRaised += AlertRaised;
        _shellTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background,
            (_, _) => UpdateShell(), Dispatcher);
        _shellTimer.Start();
        ApplyShellSettings(_viewModel.Settings.Settings);
    }

    private static void AddMenu(ContextMenu menu, string title, Action action)
    {
        var item = new MenuItem { Header = title };
        item.Click += (_, _) => action();
        menu.Items.Add(item);
    }
    private void ApplyTrayPreset(string preset)
    {
        if (MessageBox.Show($"Apply {preset}? Custom visibility will be saved for restoration.", "Choose preset",
            MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
        MetricSettings.ApplyPreset(_viewModel.Settings.Settings, preset);
        _viewModel.Settings.SaveSettings(_viewModel.Settings.Settings);
    }
    private void TogglePause()
    {
        _viewModel.Settings.Settings.Paused = !_viewModel.Settings.Settings.Paused;
        _viewModel.Settings.SaveSettings(_viewModel.Settings.Settings);
    }
    private void ToggleOverlay()
    {
        _userHidden = !_userHidden;
        if (_userHidden) Hide(); else Show();
    }
    public void RecoverOverlay()
    {
        var s = _viewModel.Settings.Settings;
        s.ClickThrough = false;
        s.Position = "TopCenter";
        _userHidden = false;
        Show();
        _viewModel.Settings.SaveSettings(s);
        Activate();
    }

    private void ApplyShellSettings(AppSettings s)
    {
        if (_source == null) return;
        var handle = _source.Handle;
        string signature = $"{s.ToggleHotkey}|{s.RecoveryHotkey}";
        if (_hotkeys != signature)
        {
            OverlayShell.UnregisterShortcut(handle, 1);
            OverlayShell.UnregisterShortcut(handle, 2);
            bool toggle = SettingsValidation.TryHotkey(s.ToggleHotkey, out var modifiers, out var key) &&
                OverlayShell.RegisterShortcut(handle, 1, modifiers, key);
            _recoveryRegistered = SettingsValidation.TryHotkey(s.RecoveryHotkey, out modifiers, out key) &&
                OverlayShell.RegisterShortcut(handle, 2, modifiers, key);
            ShellError = toggle && _recoveryRegistered ? null : "Hotkey unavailable or already in use. Tray recovery remains available; click-through disabled if recovery registration failed.";
            _hotkeys = signature;
        }
        int style = NativeInterop.GetWindowLong(handle, NativeInterop.GWL_EXSTYLE);
        style = s.ClickThrough && _recoveryRegistered ? style | NativeInterop.WS_EX_TRANSPARENT
            : style & ~NativeInterop.WS_EX_TRANSPARENT;
        NativeInterop.SetWindowLong(handle, NativeInterop.GWL_EXSTYLE, style | NativeInterop.WS_EX_TOOLWINDOW);
        if (_startupApplied != s.StartWithWindows)
        {
            try
            {
                if (s.StartWithWindows || _startupApplied == true)
                    OverlayShell.SetStartup(s.StartWithWindows, Environment.ProcessPath!);
                _startupApplied = s.StartWithWindows;
            }
            catch (UnauthorizedAccessException) { ShellError = "Windows denied sign-in registration."; }
        }
        if (s.Paused) _viewModel.Runtime?.Source<PresentMonSource>()?.Stop();
    }

    private IntPtr WindowMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != 0x0312) return IntPtr.Zero;
        if (wParam.ToInt32() == 1) ToggleOverlay();
        if (wParam.ToInt32() == 2) RecoverOverlay();
        handled = true;
        return IntPtr.Zero;
    }

    private void UpdateShell()
    {
        var monitors = DisplayInterop.Monitors();
        if (monitors.Length == 0) return;
        var s = _viewModel.Settings.Settings;
        var monitor = monitors[Math.Clamp(s.TargetMonitorIndex, 0, monitors.Length - 1)];
        bool full = OverlayShell.IsFullscreen(new WindowInteropHelper(this).Handle, monitor);
        if (_viewModel.Runtime != null) _viewModel.Runtime.FullscreenActive = full;
        if (!_userHidden)
        {
            if (s.HideInFullscreen && full) Hide();
            else if (!IsVisible) Show();
        }
        if (s.AutoPauseOnBattery && PowerMetricsSource.IsOnBattery())
            _viewModel.Runtime?.Source<PresentMonSource>()?.Stop();
        string signature = string.Join(";", monitors.Select(m => $"{m.Device}:{m.Left}:{m.Top}:{m.Right}:{m.Bottom}:{m.DpiScale}"));
        if (_monitorSignature != signature) { _monitorSignature = signature; ApplyAppearance(s); }
        if (_tray != null) _tray.ToolTipText = _viewModel.Runtime?.ActiveAlerts.IsEmpty == false
            ? "StatsInfo · active metric alert (Settings → Alerts)" : "StatsInfo 2 · local monitoring";
    }

    private void AlertRaised(AlertEvent alert)
    {
        Dispatcher.BeginInvoke(() =>
        {
            var s = _viewModel.Settings.Settings;
            string channel = s.Alerts.FirstOrDefault(a => a.MetricId == alert.MetricId)?.DeliveryChannel ?? "Default";
            bool tray = channel is "Tray only" or "Overlay + Tray" || channel == "Default" && s.TrayNotifications;
            if (tray) _tray?.ShowBalloonTip($"StatsInfo · {alert.Severity}",
                $"{MetricRegistry.ById[alert.MetricId].Label}: {alert.Value:F1} {alert.Unit}", BalloonIcon.Warning);
            if (s.AlertSound) System.Media.SystemSounds.Exclamation.Play();
        });
    }

    private void CleanupShell()
    {
        _viewModel.Settings.SettingsChanged -= SettingsChanged;
        MetricItems.LayoutUpdated -= MetricItems_LayoutUpdated;
        _viewModel.DisposeV2();
        _shellTimer?.Stop();
        _settingsWindow?.Close();
        _tray?.Dispose();
        _viewModel.Runtime?.Source<PresentMonSource>()?.Stop();
        if (_viewModel.Runtime != null) _viewModel.Runtime.AlertRaised -= AlertRaised;
        if (_source == null) return;
        OverlayShell.UnregisterShortcut(_source.Handle, 1);
        OverlayShell.UnregisterShortcut(_source.Handle, 2);
        _source.RemoveHook(WindowMessage);
    }
}
