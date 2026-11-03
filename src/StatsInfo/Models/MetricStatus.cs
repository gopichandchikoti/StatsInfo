namespace StatsInfo.Models;

public enum MetricStatus
{
    Active,
    Initializing,
    Unavailable,
    Degraded,
    Error,
    Stale
}
