using LibreHardwareMonitor.Hardware;
using StatsInfo.Models;

namespace StatsInfo.Services;

public sealed class HardwareSensorSource : IMetricSource
{
    private Computer? _computer;
    private readonly Func<AppSettings, IHardware[]>? _hardwareProvider;
    private readonly Func<SensorAccessIssue?> _cpuAccessIssue;
    public HardwareSensorSource(Func<AppSettings, IHardware[]>? hardwareProvider = null,
        Func<SensorAccessIssue?>? cpuAccessIssue = null)
    {
        _hardwareProvider = hardwareProvider;
        _cpuAccessIssue = cpuAccessIssue ?? (hardwareProvider == null ? HardwareSensorAccess.CpuIssue : () => null);
    }
    private readonly object _gate = new();
    public bool IsOpen => _computer != null;
    public string Category => "Sensors";
    public IReadOnlyList<string> MetricIds { get; } = ["cpu.temperature.package", "cpu.power.package",
        "cpu.clock.effective", "cpu.temperature.per_core", "cpu.voltage", "cpu.throttling", "cpu.fan.rpm",
        "gpu.temperature.core", "gpu.temperature.hotspot", "gpu.power.board", "gpu.clock.core", "gpu.clock.memory",
        "gpu.vram.total", "gpu.fan.rpm", "gpu.fan.percent", "gpu.throttling", "storage.temperature",
        "storage.endurance.used", "system.motherboard.temperature", "system.fans",
        "gpu.usage.total", "gpu.active_adapter", "gpu.vram.used"];
    public string[] GpuNames { get; private set; } = [];
    public string[] DriveNames { get; private set; } = [];
    public IReadOnlyDictionary<string, string> SensorInventory { get; private set; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, string> CpuTemperatureSensors { get; private set; } = new Dictionary<string, string>();
    public IReadOnlyDictionary<string, string> GpuTemperatureSensors { get; private set; } = new Dictionary<string, string>();

    public Task<IReadOnlyList<MetricSample>> ReadAsync(AppSettings settings, IReadOnlySet<string> requested,
        CancellationToken token)
    {
        lock (_gate)
        {
            if (!settings.HardwareSensorsEnabled)
            {
                _computer?.Close();
                _computer = null;
                return Task.FromResult<IReadOnlyList<MetricSample>>(MetricIds.Where(id =>
                    id is not "gpu.usage.total" and not "gpu.active_adapter" and not "gpu.vram.used" and not "gpu.vram.total")
                    .Select(id => MetricSample.Missing(id,
                        "Temperature and hardware sensors are disabled. In Settings, click Enable temperature sensors " +
                        "or enable hardware monitoring in Hardware & Diagnostics."))
                    .ToArray());
            }
            var roots = _hardwareProvider?.Invoke(settings) ?? ReadHardware(requested);
            foreach (var hardware in roots) Update(hardware, token);
            var all = roots.SelectMany(Flatten).ToArray();
            GpuNames = all.Where(IsGpu).Select(h => h.Name).ToArray();
            DriveNames = all.Where(h => h.HardwareType == HardwareType.Storage).Select(h => h.Name).ToArray();
            SensorInventory = all.SelectMany(h => h.Sensors).ToDictionary(s => s.Identifier.ToString(),
                s => $"{s.Hardware.Name} / {s.Name} ({s.SensorType})");
            var cpu = all.FirstOrDefault(h => h.HardwareType == HardwareType.Cpu);
            var gpus = all.Where(IsGpu).ToArray();
            var gpu = settings.SensorGpuName == "Auto" ? gpus.FirstOrDefault()
                : gpus.FirstOrDefault(h => h.Name == settings.SensorGpuName);
            CpuTemperatureSensors = TemperatureChoices(cpu);
            GpuTemperatureSensors = TemperatureChoices(gpu);
            var drives = all.Where(h => h.HardwareType == HardwareType.Storage).ToArray();
            var drive = settings.StorageSensorName == "Auto" ? drives.FirstOrDefault()
                : drives.FirstOrDefault(h => h.Name == settings.StorageSensorName);
            var samples = ReadSensors(cpu, gpu, drive, all, settings);
            if (cpu != null && _cpuAccessIssue() is { } issue)
                for (int i = 0; i < samples.Count; i++)
                    if (samples[i].Id.StartsWith("cpu.") && samples[i].Id != "cpu.fan.rpm")
                        samples[i] = MetricSample.Missing(samples[i].Id, issue.Reason, issue.Quality);
            return Task.FromResult<IReadOnlyList<MetricSample>>(samples);
        }
    }

    private IHardware[] ReadHardware(IReadOnlySet<string> requested)
    {
        _computer ??= new Computer();
        _computer.IsCpuEnabled = requested.Any(id => id.StartsWith("cpu.") && id != "cpu.fan.rpm");
        _computer.IsGpuEnabled = requested.Any(id => id.StartsWith("gpu."));
        _computer.IsStorageEnabled = requested.Any(id => id.StartsWith("storage."));
        _computer.IsMotherboardEnabled = requested.Any(id => id is "cpu.fan.rpm" or "system.fans"
            or "system.motherboard.temperature");
        _computer.Open();
        return _computer.Hardware.ToArray();
    }

    private List<MetricSample> ReadSensors(IHardware? cpu, IHardware? gpu, IHardware? drive,
        IHardware[] all, AppSettings settings)
    {
        var result = new List<MetricSample>
        {
            Temperature("cpu.temperature.package", cpu, settings,
                "CPU Package", "Package", "Core (Tdie)", "Core (Tctl/Tdie)", "Core (Tctl)"),
            Read("cpu.power.package", cpu, SensorType.Power, "Package"),
            Read("cpu.voltage", cpu, SensorType.Voltage, "CPU"),
            Read("gpu.usage.total", gpu, SensorType.Load, "Core"),
            Temperature("gpu.temperature.core", gpu, settings, "GPU Core", "GPU Temperature", "GPU Die", "Core"),
            Read("gpu.temperature.hotspot", gpu, SensorType.Temperature, "Hot Spot", "Hotspot"),
            Read("gpu.power.board", gpu, SensorType.Power, "Board", "Total"),
            Read("gpu.clock.core", gpu, SensorType.Clock, "Core"),
            Read("gpu.clock.memory", gpu, SensorType.Clock, "Memory"),
            Read("gpu.fan.rpm", gpu, SensorType.Fan, "Fan"),
            Read("gpu.fan.percent", gpu, SensorType.Control, "Fan"),
            Read("storage.temperature", drive, SensorType.Temperature, "Temperature", "Composite"),
            Read("storage.endurance.used", drive, SensorType.Level, "Percentage Used")
        };
        var effective = Valid(cpu, SensorType.Clock).Where(s => s.Name.Contains("Effective", StringComparison.OrdinalIgnoreCase)).ToArray();
        result.Add(effective.Length > 0 ? MetricSample.Number("cpu.clock.effective",
            effective.Average(s => (double)s.Value!.Value) / 1000, "LibreHardwareMonitor effective clock sensors (average)")
            : MetricSample.Missing("cpu.clock.effective", "No explicitly effective clock sensor exposed; requested clocks are not substituted."));
        result.Add(Structured("cpu.temperature.per_core", Valid(cpu, SensorType.Temperature)
            .Where(s => s.Name.Contains("Core", StringComparison.OrdinalIgnoreCase))));
        var board = all.Where(h => h.HardwareType is HardwareType.Motherboard or HardwareType.SuperIO).ToArray();
        var fans = board.SelectMany(h => Valid(h, SensorType.Fan)).ToArray();
        result.Add(Chosen("cpu.fan.rpm", fans, settings));
        result.Add(Chosen("system.motherboard.temperature", board.SelectMany(h => Valid(h, SensorType.Temperature)), settings));
        result.Add(Structured("system.fans", fans));
        result.Add(MetricSample.Missing("cpu.throttling", "Unknown — reliable CPU thermal/power-limit flags are not exposed by this adapter."));
        result.Add(MetricSample.Missing("gpu.throttling", "Unknown — vendor performance-limit decoder is not enabled."));
        result.Add(gpu == null ? MetricSample.Missing("gpu.active_adapter", "No sensor-supported GPU detected.")
            : MetricSample.State("gpu.active_adapter", gpu.Name, "LibreHardwareMonitor selected GPU (not process attribution)"));
        result.Add(Memory("gpu.vram.used", gpu, "Memory Used"));
        result.Add(Memory("gpu.vram.total", gpu, "Memory Total"));
        return result;
    }

    private static MetricSample Memory(string id, IHardware? gpu, string name)
    {
        var sensor = Valid(gpu, SensorType.SmallData).FirstOrDefault(s => s.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
        return sensor?.Value is float value ? MetricSample.Number(id, value / 1024.0,
            $"LibreHardwareMonitor / {gpu!.Name} / {sensor.Name} (MiB)")
            : MetricSample.Missing(id, "No dedicated VRAM sensor exposed; shared memory is never substituted.");
    }

    private static MetricSample Read(string id, IHardware? hardware, SensorType type, params string[] names)
    {
        var sensor = Valid(hardware, type).FirstOrDefault(s => names.Any(n => s.Name.Contains(n, StringComparison.OrdinalIgnoreCase)));
        return FromSensor(id, sensor);
    }

    private static IReadOnlyDictionary<string, string> TemperatureChoices(IHardware? hardware) =>
        hardware?.Sensors.Where(s => s.SensorType == SensorType.Temperature)
            .ToDictionary(s => s.Identifier.ToString(), s => s.Name) ?? new Dictionary<string, string>();

    private static MetricSample Temperature(string id, IHardware? hardware, AppSettings settings, params string[] automaticNames)
    {
        string selected = settings.SensorSelections.GetValueOrDefault(id, "") ?? "";
        var sensors = Valid(hardware, SensorType.Temperature).ToArray();
        var sensor = selected.Length > 0
            ? sensors.FirstOrDefault(s => s.Identifier.ToString() == selected)
            : automaticNames.Select(name => sensors.FirstOrDefault(s => s.Name.Equals(name,
                StringComparison.OrdinalIgnoreCase))).FirstOrDefault(s => s != null);
        if (sensor == null)
        {
            string choices = string.Join(", ", sensors.Select(s => s.Name));
            return MetricSample.Missing(id, selected.Length > 0
                ? "The selected temperature sensor is unavailable on the selected device. Choose another sensor in Hardware & Diagnostics."
                : $"No matching package/die or GPU core sensor is exposed by {hardware?.Name ?? "the selected device"}. " +
                  (choices.Length > 0 ? $"Available temperature sensors: {choices}. Choose one in Hardware & Diagnostics; its native name will be shown."
                      : "Check sensor setup and device support in Hardware & Diagnostics."));
        }
        if (sensor.Value == 0)
            return MetricSample.Missing(id, "The sensor returned zero before hardware access was ready. Check sensor setup in Hardware & Diagnostics.",
                SampleQuality.WarmingUp);
        var sample = FromSensor(id, sensor);
        return selected.Length > 0 ? sample with { LabelOverride = sensor.Name } : sample;
    }

    private static MetricSample Chosen(string id, IEnumerable<ISensor> sensors, AppSettings settings)
    {
        string? key = settings.SensorSelections.GetValueOrDefault(id);
        return FromSensor(id, sensors.FirstOrDefault(s => s.Identifier.ToString() == key));
    }

    private static MetricSample FromSensor(string id, ISensor? sensor) => sensor?.Value is float value
        ? MetricSample.Number(id, value, $"LibreHardwareMonitor / {sensor.Hardware.Name} / {sensor.Name}")
        : MetricSample.Missing(id, "Matching sensor unavailable; permission, firmware or device support may be required. Choose a sensor in Diagnostics where applicable.");

    private static MetricSample Structured(string id, IEnumerable<ISensor> sensors)
    {
        var values = sensors.GroupBy(s => $"{s.Hardware.Name} / {s.Name}")
            .ToDictionary(g => g.Key, g => (double)g.First().Value!.Value);
        return values.Count == 0 ? MetricSample.Missing(id, "No matching hardware sensors exposed.")
            : new(id, null, MetricRegistry.ById[id].Unit, DateTimeOffset.UtcNow, "LibreHardwareMonitor labeled sensors", Values: values);
    }
    private static IEnumerable<ISensor> Valid(IHardware? h, SensorType type) => h?.Sensors
        .Where(s => s.SensorType == type && s.Value is float v && float.IsFinite(v)) ?? [];
    private static bool IsGpu(IHardware h) => h.HardwareType is HardwareType.GpuNvidia or HardwareType.GpuAmd or HardwareType.GpuIntel;
    private static IEnumerable<IHardware> Flatten(IHardware hardware) => new[] { hardware }.Concat(hardware.SubHardware.SelectMany(Flatten));
    private static void Update(IHardware hardware, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        hardware.Update();
        foreach (var child in hardware.SubHardware) Update(child, token);
    }
    public void Dispose() { lock (_gate) { _computer?.Close(); _computer = null; } }
}
