using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using StatsInfo.Models;
using StatsInfo.Services;

namespace StatsInfo.Views;

public partial class SettingsWindow
{
    private ComboBox _sensorGpu = new(), _counterGpu = new(), _sensorDrive = new();
    private ComboBox _cpuFan = new(), _boardTemperature = new();
    private ComboBox _cpuTemperature = new(), _gpuTemperature = new();
    private TextBlock _temperatureStatus = new() { TextWrapping = TextWrapping.Wrap };
    private TextBlock _inventory = new() { TextWrapping = TextWrapping.Wrap };
    private Task<string>? _inventoryRead;

    private async Task ReadInventory()
    {
        _inventoryRead ??= HardwareInventory.ReadAsync();
        try { _inventory.Text = await _inventoryRead; }
        catch (Exception ex) when (ex is InvalidOperationException or System.Net.NetworkInformation.NetworkInformationException)
        { _inventory.Text = $"Inventory unavailable ({ex.GetType().Name})."; }
    }

    private void BuildNetwork(SettingsPanelBuilder p)
    {
        p.Choice("Monitored network adapter", "AdapterId", OptionalDiscovery.Read(NetworkMetricsSource.Adapters,
            new[] { new AdapterChoice("Auto", "Auto: active gateway adapter"), new AdapterChoice("All", "All adapters") }, p.Hint), "Name", "Id");
        p.Choice("Network unit", "NetworkUnit", new[] { "Mbps", "MB/s" });
        p.Hint("All adapters can double-count VPN/virtual traffic. These are live byte deltas, not a speed test.");
        p.Check("Enable periodic ICMP probes to my chosen endpoint", "LatencyEnabled");
        p.Text("Latency endpoint (hostname or IP)", "LatencyEndpoint");
        p.Text("Probe interval (seconds; minimum 2)", "LatencyIntervalSeconds");
        p.Text("PresentMon 2.x console executable (official build, supplied by you)", "PresentMonPath");
        p.Text("Capture timeout (seconds; 10–3600)", "CaptureTimeoutSeconds");
        p.Hint("Capture is optional, process-scoped ETW. No injection, automatic download or elevation. " +
            "Protected games, anti-cheat, fullscreen and some APIs may block it. Verify the executable and game policy " +
            "before starting. No FPS is shown without real frames. First active swap chain is used and identified.");
        _processes = new ComboBox { DisplayMemberPath = "Name", SelectedValuePath = "Id" };
        SettingsPanelBuilder.Name(_processes, "Game process");
        p.Panel.Children.Add(_processes);
        p.Action("Refresh processes", (_, _) => RefreshProcesses());
        p.Action("Start capture (opt-in)", (_, _) => StartCapture());
        p.Action("Stop capture", (_, _) => _runtime?.Source<PresentMonSource>()?.Stop());
        _captureStatus = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 12) };
        p.Panel.Children.Add(_captureStatus);
    }

    private void BuildDiagnostics(SettingsPanelBuilder p)
    {
        using var cpu = Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
        p.Hint($"CPU: {cpu?.GetValue("ProcessorNameString") ?? "Unknown"}\n" +
            $"Logical processors: {Environment.ProcessorCount}\nWindows: {Environment.OSVersion.VersionString}\n" +
            "Sources, permissions and timestamps appear below. Device serial numbers are not read.");
        p.Panel.Children.Add(_inventory);
        p.Check("Enable hardware and temperature sensors", "HardwareSensorsEnabled");
        p.Hint("CPU sensors require the signed PawnIO 2.2+ driver and Windows hardware access. " +
            "Install PawnIO, restart StatsInfo, and use Run as administrator if access is denied. " +
            "GPU driver readings can work without PawnIO. Sensor choices show native names so a VRM or SoC sensor stays clearly identified.");
        p.Action("Open official PawnIO driver setup page", (_, _) =>
            Process.Start(new ProcessStartInfo("https://pawnio.eu/") { UseShellExecute = true }));
        p.Panel.Children.Add(_temperatureStatus);
        _sensorGpu = p.Choice("Sensor GPU", "SensorGpuName", new[] { "Auto" });
        _cpuTemperature = p.Choice("CPU temperature sensor", "SensorSelections[cpu.temperature.package]",
            TemperatureOptions(null), "Value", "Key");
        _gpuTemperature = p.Choice("GPU temperature sensor", "SensorSelections[gpu.temperature.core]",
            TemperatureOptions(null), "Value", "Key");
        _counterGpu = p.Choice("Monitored GPU (used when hardware sensors are off)", "GpuAdapter",
            new[] { new AdapterChoice("Auto", "Auto: first available GPU") }, "Name", "Id");
        p.Choice("Physical disk for throughput", "DiskInstance", OptionalDiscovery.Read(
            () => new[] { "Auto" }.Concat(StorageMetricsSource.DiskInstances()), new[] { "Auto" }, p.Hint));
        p.Choice("Volume for free space", "Volume", OptionalDiscovery.Read(
            () => DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed).Select(d => d.Name),
            new[] { _draft.Volume }, p.Hint));
        _sensorDrive = p.Choice("Drive for temperature/endurance sensors", "StorageSensorName", new[] { "Auto" });
        _cpuFan = p.Choice("CPU fan sensor", "SensorSelections[cpu.fan.rpm]", Array.Empty<KeyValuePair<string, string>>(), "Value", "Key");
        _boardTemperature = p.Choice("Motherboard temperature sensor", "SensorSelections[system.motherboard.temperature]",
            Array.Empty<KeyValuePair<string, string>>(), "Value", "Key");
        p.Action("Refresh hardware choices", (_, _) => RefreshHardware());
        _diagnostics = new TextBlock
        {
            FontFamily = new System.Windows.Media.FontFamily("Consolas"),
            FontSize = 11,
            TextWrapping = TextWrapping.Wrap
        };
        p.Panel.Children.Add(_diagnostics);
        RefreshHardware();
    }

    private void RefreshHardware()
    {
        var sensors = _runtime?.Source<HardwareSensorSource>();
        _sensorGpu.ItemsSource = new[] { "Auto" }.Concat(sensors?.GpuNames ?? []).ToArray();
        _sensorDrive.ItemsSource = new[] { "Auto" }.Concat(sensors?.DriveNames ?? []).ToArray();
        _counterGpu.ItemsSource = _runtime?.Source<GpuEngineSource>()?.AdapterChoices ?? [];
        var inventory = sensors?.SensorInventory ?? new Dictionary<string, string>();
        _cpuTemperature.ItemsSource = TemperatureOptions(sensors?.CpuTemperatureSensors);
        _gpuTemperature.ItemsSource = TemperatureOptions(sensors?.GpuTemperatureSensors);
        _cpuFan.ItemsSource = inventory.Where(p => p.Value.Contains("(Fan)")).ToArray();
        _boardTemperature.ItemsSource = inventory.Where(p => p.Value.Contains("(Temperature)")).ToArray();
    }

    private static KeyValuePair<string, string>[] TemperatureOptions(IReadOnlyDictionary<string, string>? sensors) =>
        [new("", "Auto: CPU package/die or GPU core"), .. sensors ?? new Dictionary<string, string>()];

    private void EnableTemperatureSensors_Click(object sender, RoutedEventArgs e)
    {
        var settings = _viewModel.Service.Settings;
        settings.HardwareSensorsEnabled = true;
        _draft.HardwareSensorsEnabled = true;
        _viewModel.Service.SaveSettings(settings);
        foreach (var panel in _draftPanels) panel.DataContext = _draft;
        SaveState.Text = _viewModel.Service.LastError ??
            "Temperature sensors enabled. CPU access and available GPU temperature choices appear in Hardware & Diagnostics.";
        RefreshState();
    }

    private void RefreshProcesses()
    {
        var choices = new List<ProcessChoice>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.MainWindowHandle != IntPtr.Zero && process.Id != Environment.ProcessId)
                        choices.Add(new(process.Id, $"{process.ProcessName} (PID {process.Id})"));
                }
                catch (InvalidOperationException) { /* Process exited during enumeration. */ }
            }
        }
        _processes.ItemsSource = choices.OrderBy(c => c.Name).ToArray();
    }

    private void StartCapture()
    {
        if (_processes.SelectedItem is not ProcessChoice process)
        { _captureStatus.Text = "Select a running game/process first."; return; }
        var source = _runtime?.Source<PresentMonSource>();
        if (source == null) { _captureStatus.Text = "No FPS adapter connected."; return; }
        try
        {
            source.Start(_draft.PresentMonPath, process.Id, _draft.CaptureTimeoutSeconds);
            var saved = _viewModel.Service.Settings;
            saved.PresentMonPath = _draft.PresentMonPath;
            saved.CaptureTimeoutSeconds = _draft.CaptureTimeoutSeconds;
            _viewModel.Service.SaveSettings(saved);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        { _captureStatus.Text = ex.Message; }
    }

    private void OpenHistory()
    {
        if (_runtime == null) { SaveState.Text = "No metric runtime connected."; return; }
        new HistoryWindow(_runtime, _viewModel.Service).Show();
    }

    private async Task Export(bool json)
    {
        if (_runtime == null) return;
        string? path = _dialogs.ExportPath(this, json);
        if (path == null) return;
        try { await _runtime.History.ExportAsync(path, json); SaveState.Text = "History exported."; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { SaveState.Text = $"Export failed: {ex.GetType().Name}"; }
    }

    private void ClearHistory()
    {
        if (!_dialogs.Confirm(this, "Clear sampled history in memory and locally persisted history?", "Clear history")) return;
        _runtime?.ClearHistory();
        SaveState.Text = _runtime?.PersistenceError ?? "History cleared.";
    }
    private sealed record ProcessChoice(int Id, string Name);
}
