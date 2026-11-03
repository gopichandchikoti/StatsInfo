using System.Net.NetworkInformation;
using StatsInfo.Models;

namespace StatsInfo.Services;

public class NetworkCollector : IMetricCollector
{
    private long _lastBytesReceived;
    private long _lastBytesSent;
    private DateTime _lastSampleTime;
    private bool _firstSample = true;

    public string Key => "NETWORK_THROUGHPUT";
    public string DisplayName => "NET";
    public string Source => "NetworkInterface (IPv4/IPv6 Delta)";
    public bool IsAvailable => true;

    public Task<MetricSnapshot> CollectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            long currentBytesRecv = 0;
            long currentBytesSent = 0;

            var interfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (var ni in interfaces)
            {
                if (ni.OperationalStatus == OperationalStatus.Up &&
                    ni.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    ni.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                {
                    var stats = ni.GetIPStatistics();
                    currentBytesRecv += stats.BytesReceived;
                    currentBytesSent += stats.BytesSent;
                }
            }

            var now = DateTime.UtcNow;

            if (_firstSample)
            {
                _lastBytesReceived = currentBytesRecv;
                _lastBytesSent = currentBytesSent;
                _lastSampleTime = now;
                _firstSample = false;

                return Task.FromResult(new MetricSnapshot(
                    Key: Key,
                    DisplayName: DisplayName,
                    Value: 0.0,
                    Unit: "Mbps",
                    Timestamp: now,
                    Status: MetricStatus.Initializing,
                    Source: Source,
                    FormattedOverride: "↓ 0.0 Mbps ↑ 0.0 Mbps"
                ));
            }

            double elapsedSec = (now - _lastSampleTime).TotalSeconds;
            if (elapsedSec <= 0) elapsedSec = 1.0;

            long deltaRecv = Math.Max(0, currentBytesRecv - _lastBytesReceived);
            long deltaSent = Math.Max(0, currentBytesSent - _lastBytesSent);

            _lastBytesReceived = currentBytesRecv;
            _lastBytesSent = currentBytesSent;
            _lastSampleTime = now;

            // Convert to Mbps: (bytes * 8) / (1,000,000 * seconds)
            double downMbps = (deltaRecv * 8.0) / (1_000_000.0 * elapsedSec);
            double upMbps = (deltaSent * 8.0) / (1_000_000.0 * elapsedSec);

            string formatted = $"↓ {downMbps:F1} Mbps ↑ {upMbps:F1} Mbps";

            return Task.FromResult(new MetricSnapshot(
                Key: Key,
                DisplayName: DisplayName,
                Value: Math.Round(downMbps + upMbps, 1),
                Unit: "Mbps",
                Timestamp: now,
                Status: MetricStatus.Active,
                Source: Source,
                FormattedOverride: formatted
            ));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new MetricSnapshot(
                Key: Key,
                DisplayName: DisplayName,
                Value: 0.0,
                Unit: "Mbps",
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
