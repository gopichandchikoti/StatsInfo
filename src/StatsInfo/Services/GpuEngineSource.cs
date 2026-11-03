using System.Diagnostics;
using System.Text.RegularExpressions;
using StatsInfo.Models;
using StatsInfo.Helpers;

namespace StatsInfo.Services;

public sealed partial class GpuEngineSource : IMetricSource
{
    private readonly CounterReader _counters;
    public GpuEngineSource(CounterReader? counters = null) => _counters = counters ?? new();
    private string[] _instances = [];
    private long _lastEnumerated;
    private DxgiAdapterInfo[] _adapters = [];
    public string Category => "GPU";
    public IReadOnlyList<string> MetricIds { get; } = ["gpu.usage.total", "gpu.engines", "gpu.active_adapter",
        "gpu.vram.used", "gpu.vram.total", "process.top_gpu"];
    public string[] AdapterTokens { get; private set; } = [];
    public AdapterChoice[] AdapterChoices => [new("Auto", "Auto: first available GPU"),
        .. AdapterTokens.Select(token => new AdapterChoice(token, _adapters.FirstOrDefault(a => a.Matches(token))
            is { } adapter ? $"{adapter.Name} ({token[(token.LastIndexOf('_') + 1)..]})" : token))];

    public Task<IReadOnlyList<MetricSample>> ReadAsync(AppSettings settings, IReadOnlySet<string> requested,
        CancellationToken token)
    {
        if (_instances.Length == 0 || Stopwatch.GetElapsedTime(_lastEnumerated).TotalSeconds > 15)
        {
            _instances = _counters.Instances("GPU Engine");
            _adapters = DxgiAdapters.Read();
            _counters.Retain("GPU Engine", _instances.ToHashSet());
            _lastEnumerated = Stopwatch.GetTimestamp();
        }
        if (_instances.Length > 2048)
            return Task.FromResult<IReadOnlyList<MetricSample>>(MetricIds.Select(id => MetricSample.Missing(id,
                "GPU counter instance budget exceeded (2048); partial engine totals are not reported.")).ToArray());
        var rows = _instances.Select(n => (Name: n, Match: EnginePattern().Match(n)))
            .Where(r => r.Match.Success).ToArray();
        AdapterTokens = rows.Select(r => r.Match.Groups["adapter"].Value).Distinct().Order().ToArray();
        string? adapter = settings.GpuAdapter == "Auto" ? AdapterTokens.FirstOrDefault() : settings.GpuAdapter;
        if (adapter == null || !AdapterTokens.Contains(adapter))
            return Task.FromResult<IReadOnlyList<MetricSample>>(MetricIds.Select(id =>
                MetricSample.Missing(id, "Selected GPU is absent or Windows GPU Engine counters are unavailable.")).ToArray());
        var engines = new Dictionary<string, double>();
        var processes = new Dictionary<int, Dictionary<string, double>>();
        foreach (var row in rows.Where(r => r.Match.Groups["adapter"].Value == adapter))
        {
            token.ThrowIfCancellationRequested();
            double? value = ReadCounter(row.Name);
            if (value == null) continue;
            string engine = $"{row.Match.Groups["engine"].Value} ({row.Match.Groups["type"].Value})";
            engines[engine] = engines.GetValueOrDefault(engine) + Math.Max(0, value.Value);
            int pid = int.Parse(row.Match.Groups["pid"].Value);
            if (!processes.TryGetValue(pid, out var usage)) processes[pid] = usage = new();
            usage[engine] = usage.GetValueOrDefault(engine) + Math.Max(0, value.Value);
        }
        var result = new List<MetricSample>();
        var normalized = engines.ToDictionary(p => p.Key, p => Math.Clamp(p.Value, 0, 100));
        string source = $"PDH GPU Engine, {adapter}; busiest physical engine; parallel engines not summed";
        if (!settings.HardwareSensorsEnabled)
        {
            var identity = _adapters.FirstOrDefault(a => a.Matches(adapter));
            result.Add(MetricSample.State("gpu.active_adapter", identity?.Name ?? adapter,
                $"Selected Windows GPU adapter / {adapter}; not foreground-process attribution"));
            result.Add(normalized.Count > 0 ? MetricSample.Number("gpu.usage.total", normalized.Values.Max(), source)
                : MetricSample.Missing("gpu.usage.total", "GPU counters warming up.", SampleQuality.WarmingUp));
            result.Add(ReadVram(adapter));
            bool singleNode = identity != null && AdapterTokens.Count(t => identity.Matches(t)) == 1;
            result.Add(singleNode ? MetricSample.Number("gpu.vram.total", identity!.DedicatedBytes / 1073741824.0,
                $"DXGI_ADAPTER_DESC1.DedicatedVideoMemory / {identity.Name} / {adapter}")
                : MetricSample.Missing("gpu.vram.total", "No unambiguous DXGI dedicated capacity for this physical node; shared memory is not substituted."));
        }
        result.Add(normalized.Count > 0
            ? new("gpu.engines", null, "%", DateTimeOffset.UtcNow, source, Values: normalized)
            : MetricSample.Missing("gpu.engines", "GPU counters warming up.", SampleQuality.WarmingUp));
        if (requested.Contains("process.top_gpu")) result.Add(TopProcess(processes, source));
        return Task.FromResult<IReadOnlyList<MetricSample>>(result);
    }

    private double? ReadCounter(string instance)
    {
        try { return _counters.Read("GPU Engine", "Utilization Percentage", instance); }
        catch (InvalidOperationException) { return null; } // Process exited after enumeration.
    }

    private MetricSample ReadVram(string adapter)
    {
        try
        {
            var names = _counters.Instances("GPU Adapter Memory");
            string? name = names.FirstOrDefault(n => n.Contains(adapter, StringComparison.OrdinalIgnoreCase));
            double? value = name == null ? null : _counters.Read("GPU Adapter Memory", "Dedicated Usage", name);
            return value is double bytes ? MetricSample.Number("gpu.vram.used", bytes / 1073741824.0,
                $"PDH GPU Adapter Memory/Dedicated Usage/{name}")
                : MetricSample.Missing("gpu.vram.used", "Dedicated GPU memory counter warming up or unsupported.");
        }
        catch (InvalidOperationException) { return MetricSample.Missing("gpu.vram.used", "Dedicated memory counter unavailable."); }
    }

    private static MetricSample TopProcess(Dictionary<int, Dictionary<string, double>> processes, string source)
    {
        foreach (var row in processes.OrderByDescending(p => p.Value.Values.Max()))
        {
            try
            {
                using var process = Process.GetProcessById(row.Key);
                double value = Math.Clamp(row.Value.Values.Max(), 0, 100);
                return MetricSample.Number("process.top_gpu", value, source, $"{process.ProcessName} {value:F1}%");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
            { /* The process exited or is inaccessible. */ }
        }
        return MetricSample.Missing("process.top_gpu", "No accessible process with GPU engine attribution.");
    }

    [GeneratedRegex(@"pid_(?<pid>\d+)_(?<adapter>luid_[0-9a-fx]+_[0-9a-fx]+_phys_\d+)_eng_(?<engine>\d+)_engtype_(?<type>.+)",
        RegexOptions.IgnoreCase)]
    private static partial Regex EnginePattern();
    public void Dispose() => _counters.Dispose();
}
