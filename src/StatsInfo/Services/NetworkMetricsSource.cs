using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Text.Json;
using StatsInfo.Models;

namespace StatsInfo.Services;

public sealed record AdapterChoice(string Id, string Name);

public sealed class NetworkMetricsSource : IMetricSource
{
    private readonly CounterDelta _delta = new();
    private readonly string _dailyPath;
    private DailyTraffic _daily = new();
    private long _lastSaved;
    public string? PersistenceError { get; private set; }
    public string Category => "Network";
    public IReadOnlyList<string> MetricIds { get; } = ["network.download.rate", "network.upload.rate",
        "network.adapter", "network.daily.received", "network.daily.sent", "wifi.signal", "wifi.link_speed",
        "wifi.band", "wifi.channel"];

    public NetworkMetricsSource(string settingsPath)
    {
        _dailyPath = Path.Combine(Path.GetDirectoryName(settingsPath)!, "traffic-today.json");
        try
        {
            if (File.Exists(_dailyPath) && new FileInfo(_dailyPath).Length < 16384)
                _daily = JsonSerializer.Deserialize<DailyTraffic>(File.ReadAllText(_dailyPath)) ?? new();
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        { PersistenceError = ex.GetType().Name; }
    }

    public static AdapterChoice[] Adapters() => [new("Auto", "Auto: active gateway adapter"),
        new("All", "All adapters (VPN traffic may be counted twice)"),
        .. NetworkInterface.GetAllNetworkInterfaces().Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .Select(n => new AdapterChoice(n.Id, $"{n.Name} ({n.NetworkInterfaceType})"))];

    public Task<IReadOnlyList<MetricSample>> ReadAsync(AppSettings settings, IReadOnlySet<string> requested,
        CancellationToken token)
    {
        var candidates = NetworkInterface.GetAllNetworkInterfaces().Where(n => n.OperationalStatus == OperationalStatus.Up &&
            n.NetworkInterfaceType is not NetworkInterfaceType.Loopback and not NetworkInterfaceType.Tunnel).ToArray();
        var chosen = Select(candidates, settings.AdapterId);
        if (chosen.Length == 0)
        {
            _delta.Reset();
            return Task.FromResult<IReadOnlyList<MetricSample>>(MetricIds.Select(id => MetricSample.Missing(id,
                "Selected adapter is disconnected, disabled, or absent.")).ToArray());
        }
        long received = 0, sent = 0;
        foreach (var adapter in chosen)
        {
            var stats = adapter.GetIPStatistics();
            received += stats.BytesReceived;
            sent += stats.BytesSent;
        }
        string identity = string.Join(";", chosen.Select(n => n.Id).Order());
        var delta = _delta.Update(identity, received, sent, (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency);
        var result = new List<MetricSample>
        {
            MetricSample.State("network.adapter", string.Join(" + ", chosen.Select(n => n.Name)),
                settings.AdapterId == "All" ? "All adapters; VPN/virtual traffic may double-count" : "NetworkInterface selection")
        };
        foreach (var id in new[] { "network.download.rate", "network.upload.rate" })
            result.Add(delta is { } d ? MetricSample.Number(id,
                id == "network.download.rate" ? d.DownMbps : d.UpMbps, "NetworkInterface / monotonic byte delta")
                : MetricSample.Missing(id, "New adapter, counter reset, resume, or initial interval; warming up.",
                    SampleQuality.WarmingUp));
        if (_daily.Day != DateTime.Today) _daily = new() { Day = DateTime.Today };
        if (delta is { } changes) { _daily.Received += changes.Received; _daily.Sent += changes.Sent; }
        result.Add(MetricSample.Number("network.daily.received", _daily.Received / 1073741824.0,
            "Observed local-day byte deltas (only while this app monitors)"));
        result.Add(MetricSample.Number("network.daily.sent", _daily.Sent / 1073741824.0,
            "Observed local-day byte deltas (only while this app monitors)"));
        if (Stopwatch.GetElapsedTime(_lastSaved).TotalSeconds >= 30) SaveDaily();
        if (requested.Any(id => id.StartsWith("wifi."))) result.AddRange(WifiReader.Read(chosen));
        return Task.FromResult<IReadOnlyList<MetricSample>>(result);
    }

    private static NetworkInterface[] Select(NetworkInterface[] candidates, string id)
    {
        if (id == "All") return candidates;
        if (id != "Auto") return candidates.Where(n => n.Id == id).ToArray();
        var selected = candidates.OrderByDescending(n => n.GetIPProperties().GatewayAddresses.Count > 0)
            .ThenBy(n => n.NetworkInterfaceType == NetworkInterfaceType.Ethernet ? 0 : 1)
            .ThenBy(n => n.Id).FirstOrDefault();
        return selected == null ? [] : [selected];
    }

    private void SaveDaily()
    {
        _lastSaved = Stopwatch.GetTimestamp();
        try
        {
            File.WriteAllText(_dailyPath + ".tmp", JsonSerializer.Serialize(_daily));
            File.Move(_dailyPath + ".tmp", _dailyPath, true);
            PersistenceError = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { PersistenceError = $"Daily traffic persistence: {ex.GetType().Name}"; }
    }
    public void Dispose() => SaveDaily();
    public sealed class DailyTraffic
    {
        public DateTime Day { get; set; } = DateTime.Today;
        public long Received { get; set; }
        public long Sent { get; set; }
    }
}
