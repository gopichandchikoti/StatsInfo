using System.Runtime.InteropServices;
using StatsInfo.Helpers;
using StatsInfo.Models;

namespace StatsInfo.Services;

public sealed class DisplayMetricsSource : IMetricSource
{
    public string Category => "Display/Gaming";
    public IReadOnlyList<string> MetricIds { get; } = ["display.refresh.configured", "display.resolution",
        "display.hdr", "display.vrr", "display.scaling"];
    public Task<IReadOnlyList<MetricSample>> ReadAsync(AppSettings settings, IReadOnlySet<string> requested,
        CancellationToken token)
    {
        var monitors = DisplayInterop.Monitors();
        if (monitors.Length == 0) return Task.FromResult<IReadOnlyList<MetricSample>>(MetricIds.Select(id =>
            MetricSample.Missing(id, "No active display is attached.")).ToArray());
        var monitor = monitors[Math.Clamp(settings.TargetMonitorIndex, 0, monitors.Length - 1)];
        var mode = new NativeInterop.DEVMODE { dmSize = (ushort)Marshal.SizeOf<NativeInterop.DEVMODE>() };
        bool ok = EnumDisplaySettingsEx(monitor.Device, -1, ref mode, 0);
        var active = DisplayInterop.ActiveMode(monitor.Device);
        double? hz = active.Hz ?? (ok && mode.dmDisplayFrequency > 1 ? mode.dmDisplayFrequency : null);
        var result = new List<MetricSample>
        {
            hz is double refresh ? MetricSample.Number("display.refresh.configured", refresh,
                $"{(active.Hz.HasValue ? "QueryDisplayConfig rational configured refresh" : "EnumDisplaySettingsEx integer refresh")} / {monitor.Device}",
                $"{refresh:0.##} Hz") : MetricSample.Missing("display.refresh.configured", "Active configured refresh unavailable."),
            ok && mode.dmPelsWidth > 0 && mode.dmPelsHeight > 0
                ? MetricSample.State("display.resolution", $"{mode.dmPelsWidth} × {mode.dmPelsHeight}",
                $"EnumDisplaySettingsEx / {monitor.Device}") : MetricSample.Missing("display.resolution", "Display mode unavailable."),
            MetricSample.Number("display.scaling", monitor.DpiScale * 100, $"GetDpiForMonitor / {monitor.Device}"),
            active.Hdr is bool hdr ? MetricSample.State("display.hdr", hdr ? "On" : "Off",
                $"DisplayConfigGetDeviceInfo advancedColorEnabled / {monitor.Device}")
                : MetricSample.Missing("display.hdr", "Current advanced-color state unavailable."),
            MetricSample.Missing("display.vrr", "Unknown — no reliable current VRR-state source; configured Hz is not instantaneous VRR.")
        };
        return Task.FromResult<IReadOnlyList<MetricSample>>(result);
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "EnumDisplaySettingsExW")]
    private static extern bool EnumDisplaySettingsEx(string name, int mode, ref NativeInterop.DEVMODE deviceMode, uint flags);
    public void Dispose() { }
}
