using System.Diagnostics;
using System.Net.NetworkInformation;
using StatsInfo.Models;

namespace StatsInfo.Services;

public sealed class LatencyMetricsSource : IMetricSource
{
    private readonly Queue<double?> _probes = new();
    private long _lastProbe;
    private string _endpoint = "";
    private IReadOnlyList<MetricSample> _cached = [];
    public string Category => "Network";
    public IReadOnlyList<string> MetricIds { get; } = ["network.ping", "network.packet_loss", "network.jitter"];

    public async Task<IReadOnlyList<MetricSample>> ReadAsync(AppSettings settings, IReadOnlySet<string> requested,
        CancellationToken token)
    {
        string endpoint = settings.LatencyEndpoint.Trim();
        if (!settings.LatencyEnabled || Uri.CheckHostName(endpoint) == UriHostNameType.Unknown)
        {
            _probes.Clear();
            _endpoint = "";
            return MetricIds.Select(id => MetricSample.Missing(id,
                "Periodic ICMP probes are off. Choose an endpoint and explicitly enable probing in Network & FPS.")).ToArray();
        }
        if (_endpoint != endpoint) { _probes.Clear(); _lastProbe = 0; _endpoint = endpoint; }
        if (_cached.Count > 0 && Stopwatch.GetElapsedTime(_lastProbe).TotalSeconds < settings.LatencyIntervalSeconds)
            return _cached;
        _lastProbe = Stopwatch.GetTimestamp();
        double? rtt = null;
        string failure = "Probe timeout or endpoint did not reply (ICMP may be blocked).";
        using var ping = new Ping();
        try
        {
            var reply = await ping.SendPingAsync(endpoint, 2000).WaitAsync(token);
            if (reply.Status == IPStatus.Success) rtt = reply.RoundtripTime;
            else failure = $"ICMP probe result: {reply.Status}";
        }
        catch (PingException) { failure = "ICMP probe failed; check endpoint, DNS and permissions."; }
        _probes.Enqueue(rtt);
        while (_probes.Count > 60) _probes.Dequeue();
        var stats = Calculate(_probes.ToArray());
        string source = $"ICMP to chosen endpoint; last {_probes.Count}/60 attempts";
        _cached = [rtt is double value ? MetricSample.Number("network.ping", value, source)
                : MetricSample.Missing("network.ping", failure),
            MetricSample.Number("network.packet_loss", stats.Loss, source),
            stats.Jitter is double jitter ? MetricSample.Number("network.jitter", jitter, source)
                : MetricSample.Missing("network.jitter", "At least two consecutive successful probes are required.",
                    SampleQuality.WarmingUp)];
        return _cached;
    }

    public static (double Loss, double? Jitter) Calculate(IReadOnlyList<double?> probes)
    {
        if (probes.Count == 0) return (0, null);
        var changes = new List<double>();
        for (int i = 1; i < probes.Count; i++)
            if (probes[i - 1] is double a && probes[i] is double b) changes.Add(Math.Abs(b - a));
        return (100.0 * probes.Count(p => !p.HasValue) / probes.Count,
            changes.Count > 0 ? changes.Average() : null);
    }
    public void Dispose() => _probes.Clear();
}
