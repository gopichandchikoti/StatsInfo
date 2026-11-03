using System.Diagnostics;
using System.Runtime.InteropServices;
using StatsInfo.Helpers;
using StatsInfo.Models;

namespace StatsInfo.Services;

public sealed class CpuMemorySource(string category) : IMetricSource
{
    private readonly CounterReader _counters = new();
    public string Category => category;
    public IReadOnlyList<string> MetricIds { get; } = MetricRegistry.All
        .Where(d => category == "Memory" ? d.Group == "Memory" : d.Id is "cpu.usage.total"
            or "cpu.usage.per_core" or "cpu.logical_processors").Select(d => d.Id).ToArray();

    public Task<IReadOnlyList<MetricSample>> ReadAsync(AppSettings settings, IReadOnlySet<string> requested,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<MetricSample>>(category == "Memory" ? ReadMemory() : ReadCpu(requested));
    }

    private List<MetricSample> ReadCpu(IReadOnlySet<string> requested)
    {
        var result = new List<MetricSample>();
        if (requested.Contains("cpu.usage.total"))
        {
            var value = _counters.Read("Processor", "% Processor Time", "_Total");
            result.Add(value is double v ? MetricSample.Number("cpu.usage.total", Math.Clamp(v, 0, 100),
                "PDH Processor/% Processor Time/_Total") : MetricSample.Missing("cpu.usage.total",
                "Waiting for the second counter sample.", SampleQuality.WarmingUp));
        }
        result.Add(MetricSample.Number("cpu.logical_processors", Environment.ProcessorCount, "Environment.ProcessorCount"));
        if (!requested.Contains("cpu.usage.per_core")) return result;
        var values = new Dictionary<string, double>();
        for (int i = 0; i < Environment.ProcessorCount; i++)
        {
            var value = _counters.Read("Processor", "% Processor Time", i.ToString());
            if (value is double v) values[$"CPU {i}"] = Math.Clamp(v, 0, 100);
        }
        result.Add(values.Count == 0 ? MetricSample.Missing("cpu.usage.per_core", "Counters warming up.",
            SampleQuality.WarmingUp) : new("cpu.usage.per_core", null, "%", DateTimeOffset.UtcNow,
                "PDH Processor/% Processor Time per logical processor", Values: values));
        return result;
    }

    private static List<MetricSample> ReadMemory()
    {
        var memory = new NativeInterop.MEMORYSTATUSEX();
        if (!NativeInterop.GlobalMemoryStatusEx(memory)) throw new System.ComponentModel.Win32Exception();
        const double gib = 1073741824.0;
        double used = (memory.ullTotalPhys - memory.ullAvailPhys) / gib;
        double total = memory.ullTotalPhys / gib;
        var result = new List<MetricSample>
        {
            MetricSample.Number("memory.physical.used", used, "GlobalMemoryStatusEx",
                $"{used:F1}/{total:F1} GiB ({memory.dwMemoryLoad}%)"),
            MetricSample.Number("memory.physical.total", total, "GlobalMemoryStatusEx"),
            MetricSample.Number("memory.physical.available", memory.ullAvailPhys / gib, "GlobalMemoryStatusEx"),
            MetricSample.State("memory.pressure", memory.dwMemoryLoad >= 90 ? "High (≥90%)" :
                memory.dwMemoryLoad >= 80 ? "Elevated (≥80%)" : "Normal (<80%)", "App classification of physical RAM usage")
        };
        var info = new PerformanceInfo { Size = (uint)Marshal.SizeOf<PerformanceInfo>() };
        bool ok = GetPerformanceInfo(ref info, info.Size);
        foreach (var pair in new[] { ("memory.commit.used", info.CommitTotal), ("memory.commit.limit", info.CommitLimit) })
            result.Add(ok ? MetricSample.Number(pair.Item1, (double)pair.Item2 * info.PageSize / gib,
                "GetPerformanceInfo system commit pages × page size")
                : MetricSample.Missing(pair.Item1, "GetPerformanceInfo failed."));
        return result;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PerformanceInfo
    {
        public uint Size;
        public nuint CommitTotal, CommitLimit, CommitPeak, PhysicalTotal, PhysicalAvailable;
        public nuint SystemCache, KernelTotal, KernelPaged, KernelNonpaged, PageSize;
        public uint HandleCount, ProcessCount, ThreadCount;
    }
    [DllImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPerformanceInfo(ref PerformanceInfo info, uint size);
    public void Dispose() => _counters.Dispose();
}
