using StatsInfo.Helpers;
using StatsInfo.Models;

namespace StatsInfo.Services;

public class DisplayModeCollector : IMetricCollector
{
    public string Key => "DISPLAY_HZ";
    public string DisplayName => "Display";
    public string Source => "EnumDisplaySettings (Win32 User32)";
    public bool IsAvailable => true;

    public Task<MetricSnapshot> CollectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var devMode = new NativeInterop.DEVMODE();
            devMode.dmSize = (ushort)System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeInterop.DEVMODE));

            if (NativeInterop.EnumDisplaySettings(null, NativeInterop.ENUM_CURRENT_SETTINGS, ref devMode))
            {
                uint hz = devMode.dmDisplayFrequency;
                string formatted = $"{hz} Hz ({devMode.dmPelsWidth}x{devMode.dmPelsHeight})";

                return Task.FromResult(new MetricSnapshot(
                    Key: Key,
                    DisplayName: DisplayName,
                    Value: hz,
                    Unit: "Hz",
                    Timestamp: DateTime.UtcNow,
                    Status: MetricStatus.Active,
                    Source: Source,
                    FormattedOverride: formatted
                ));
            }

            return Task.FromResult(MetricSnapshot.Unavailable(Key, DisplayName, "Hz", Source, "EnumDisplaySettingsFailed"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new MetricSnapshot(
                Key: Key,
                DisplayName: DisplayName,
                Value: 0.0,
                Unit: "Hz",
                Timestamp: DateTime.UtcNow,
                Status: MetricStatus.Error,
                Source: Source,
                ErrorCode: ex.GetType().Name
            ));
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
