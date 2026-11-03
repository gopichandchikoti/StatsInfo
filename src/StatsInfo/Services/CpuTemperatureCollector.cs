using LibreHardwareMonitor.Hardware;
using StatsInfo.Models;

namespace StatsInfo.Services;

public class CpuTemperatureCollector : IMetricCollector
{
    private readonly Computer? _computer;
    private readonly IHardware? _cpuHardware;
    private readonly string? _initializationErrorCode;
    private bool _disposed;

    public string Key => "CPU_TEMPERATURE";
    public string DisplayName => "CPU Temperature";
    public string Source => "LibreHardwareMonitorLib";
    public bool IsAvailable => _cpuHardware != null;

    public CpuTemperatureCollector()
    {
        try
        {
            _computer = new Computer { IsCpuEnabled = true };
            _computer.Open();
            _cpuHardware = _computer.Hardware.FirstOrDefault(hardware => hardware.HardwareType == HardwareType.Cpu);
        }
        catch (Exception ex)
        {
            _initializationErrorCode = ex.GetType().Name;
        }
    }

    public Task<MetricSnapshot> CollectAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_initializationErrorCode != null)
        {
            return Task.FromResult(new MetricSnapshot(
                Key, DisplayName, 0, "°C", DateTime.UtcNow, MetricStatus.Error, Source, _initializationErrorCode, "N/A"));
        }

        if (_cpuHardware == null)
        {
            return Task.FromResult(MetricSnapshot.Unavailable(
                Key, DisplayName, "°C", Source, "CpuHardwareNotDetected"));
        }

        try
        {
            _cpuHardware.Update();
            var temperature = _cpuHardware.Sensors
                .Where(sensor => sensor.SensorType == SensorType.Temperature &&
                    sensor.Value.HasValue &&
                    float.IsFinite(sensor.Value.Value))
                .OrderByDescending(sensor => sensor.Name.Contains("Package", StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();

            if (temperature?.Value is not float value)
            {
                return Task.FromResult(MetricSnapshot.Unavailable(
                    Key, DisplayName, "°C", Source, "TemperatureSensorNotDetected"));
            }

            return Task.FromResult(new MetricSnapshot(
                Key,
                DisplayName,
                Math.Round(value, 1),
                "°C",
                DateTime.UtcNow,
                MetricStatus.Active,
                $"{Source} ({_cpuHardware.Name})"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new MetricSnapshot(
                Key, DisplayName, 0, "°C", DateTime.UtcNow, MetricStatus.Error, Source, ex.GetType().Name, "N/A"));
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _computer?.Close();
            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }
}
