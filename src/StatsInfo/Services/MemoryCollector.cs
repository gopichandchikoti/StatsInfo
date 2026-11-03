using StatsInfo.Helpers;
using StatsInfo.Models;

namespace StatsInfo.Services;

public class MemoryCollector : IMetricCollector
{
    public string Key => "RAM_USAGE";
    public string DisplayName => "RAM";
    public string Source => "GlobalMemoryStatusEx (Windows Kernel32)";
    public bool IsAvailable => true;

    public Task<MetricSnapshot> CollectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var memStatus = new NativeInterop.MEMORYSTATUSEX();
            if (NativeInterop.GlobalMemoryStatusEx(memStatus))
            {
                double totalPhysGb = memStatus.ullTotalPhys / (1024.0 * 1024.0 * 1024.0);
                double availPhysGb = memStatus.ullAvailPhys / (1024.0 * 1024.0 * 1024.0);
                double usedPhysGb = totalPhysGb - availPhysGb;
                double percent = memStatus.dwMemoryLoad;

                string formatted = $"{usedPhysGb:F1}/{totalPhysGb:F1} GB ({percent:F0}%)";

                return Task.FromResult(new MetricSnapshot(
                    Key: Key,
                    DisplayName: DisplayName,
                    Value: Math.Round(usedPhysGb, 1),
                    Unit: "GB",
                    Timestamp: DateTime.UtcNow,
                    Status: MetricStatus.Active,
                    Source: Source,
                    FormattedOverride: formatted
                ));
            }

            return Task.FromResult(MetricSnapshot.Unavailable(Key, DisplayName, "GB", Source, "Win32CallFailed"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(new MetricSnapshot(
                Key: Key,
                DisplayName: DisplayName,
                Value: 0.0,
                Unit: "GB",
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
