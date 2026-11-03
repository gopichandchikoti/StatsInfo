namespace StatsInfo.Models;

public record MetricSnapshot(
    string Key,
    string DisplayName,
    double Value,
    string Unit,
    DateTime Timestamp,
    MetricStatus Status,
    string Source,
    string? ErrorCode = null,
    string? FormattedOverride = null,
    double? SecondaryValue = null,
    string? SecondaryUnit = null,
    MetricStatus? SecondaryStatus = null
)
{
    public string FormattedValue => FormattedOverride ?? $"{Value:F1} {Unit}".Trim();

    public string? SecondaryFormattedValue =>
        SecondaryValue.HasValue ? $"{SecondaryValue.Value:F0} {SecondaryUnit}".Trim() : null;

    public bool IsFresh(TimeSpan maxAge) =>
        DateTime.UtcNow - Timestamp <= maxAge && Status == MetricStatus.Active;

    public static MetricSnapshot Empty(string key, string displayName, string unit, string source) =>
        new(key, displayName, 0.0, unit, DateTime.UtcNow, MetricStatus.Initializing, source);

    public static MetricSnapshot Unavailable(string key, string displayName, string unit, string source, string reason) =>
        new(key, displayName, 0.0, unit, DateTime.UtcNow, MetricStatus.Unavailable, source, reason, "N/A");
}
