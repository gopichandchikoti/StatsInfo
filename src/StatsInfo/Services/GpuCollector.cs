using LibreHardwareMonitor.Hardware;
using StatsInfo.Models;

namespace StatsInfo.Services;

public class GpuCollector : IMetricCollector
{
    private Computer? _computer;
    private IHardware? _gpuHardware;
    private bool _initialized;
    private bool _disposed;

    public string Key => "GPU_METRICS";
    public string DisplayName => "GPU";
    public string Source => "LibreHardwareMonitorLib";
    public bool IsAvailable => _initialized && _gpuHardware != null;

    public GpuCollector()
    {
        try
        {
            _computer = new Computer
            {
                IsGpuEnabled = true
            };
            _computer.Open();

            foreach (var hardware in _computer.Hardware)
            {
                if (hardware.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel)
                {
                    _gpuHardware = hardware;
                    _initialized = true;
                    break;
                }
            }
        }
        catch
        {
            _initialized = false;
        }
    }

    public Task<MetricSnapshot> CollectAsync(CancellationToken cancellationToken = default)
    {
        if (!_initialized || _gpuHardware == null)
        {
            return Task.FromResult(MetricSnapshot.Unavailable(Key, DisplayName, "%", Source, "GpuHardwareNotDetected"));
        }

        try
        {
            _gpuHardware.Update();

            float? gpuLoad = null;
            float? gpuTemp = null;

            foreach (var sensor in _gpuHardware.Sensors)
            {
                if (sensor.Value is not float sensorValue || !float.IsFinite(sensorValue))
                {
                    continue;
                }

                if (sensor.SensorType == SensorType.Load && sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase))
                {
                    gpuLoad ??= sensorValue;
                }
                else if (sensor.SensorType == SensorType.Temperature && sensor.Name.Contains("Core", StringComparison.OrdinalIgnoreCase))
                {
                    gpuTemp ??= sensorValue;
                }
            }

            if (gpuLoad is null)
            {
                return Task.FromResult(new MetricSnapshot(
                    Key,
                    DisplayName,
                    0,
                    "%",
                    DateTime.UtcNow,
                    MetricStatus.Unavailable,
                    Source,
                    "GpuLoadSensorNotDetected",
                    "N/A",
                    gpuTemp,
                    "°C",
                    gpuTemp.HasValue ? MetricStatus.Active : MetricStatus.Unavailable));
            }

            double loadVal = Math.Round(gpuLoad.Value, 0);
            string tempStr = gpuTemp.HasValue ? $"{gpuTemp.Value:F0}°C" : "N/A";
            string formatted = $"{loadVal:F0}% · {tempStr}";

            return Task.FromResult(new MetricSnapshot(
                Key: Key,
                DisplayName: DisplayName,
                Value: loadVal,
                Unit: "%",
                Timestamp: DateTime.UtcNow,
                Status: MetricStatus.Active,
                Source: $"{Source} ({_gpuHardware.Name})",
                FormattedOverride: formatted,
                SecondaryValue: gpuTemp,
                SecondaryUnit: "°C",
                SecondaryStatus: gpuTemp.HasValue ? MetricStatus.Active : MetricStatus.Unavailable
            ));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new MetricSnapshot(
                Key: Key,
                DisplayName: DisplayName,
                Value: 0.0,
                Unit: "%",
                Timestamp: DateTime.UtcNow,
                Status: MetricStatus.Degraded,
                Source: Source,
                ErrorCode: ex.GetType().Name,
                FormattedOverride: "GPU N/A"
            ));
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _computer?.Close();
            _computer = null;
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
