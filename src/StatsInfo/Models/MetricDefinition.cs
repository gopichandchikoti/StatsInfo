namespace StatsInfo.Models;

public sealed record MetricDefinition(
    string Id, string Group, string Label, string ShortLabel, string Unit, string Source,
    string Description, int SortOrder, bool DefaultVisibility = false)
{
    public string Capability => "Runtime source discovery; unavailable readings retain the visibility preference.";
    public string SamplingCost => Group is "Process" or "GPU" or "Storage" ? "Moderate" : "Low";
    public string PrivacyLevel => Group == "Process" ? "Local process name only" : "Local device statistics";
    public IReadOnlyList<string> SupportedPresets => MetricRegistry.Presets
        .Where(p => p.Value.Contains(Id)).Select(p => p.Key).ToArray();
}

public sealed class MetricPreference
{
    public bool Visible { get; set; }
    public int Order { get; set; }
    public string Unit { get; set; } = "";
    public string Label { get; set; } = "";
    public int Decimals { get; set; } = -1;
    public bool Sparkline { get; set; }
}

public enum SampleQuality { Valid, Stale, Unavailable, PermissionDenied, Error, WarmingUp }

public sealed record MetricSample(
    string Id, double? Value, string Unit, DateTimeOffset SampledAt, string Source,
    SampleQuality Quality = SampleQuality.Valid, string? Reason = null,
    string? Text = null, IReadOnlyDictionary<string, double>? Values = null)
{
    public bool IsNumeric => Quality == SampleQuality.Valid && Value is double value && double.IsFinite(value);
    public static MetricSample Missing(string id, string reason, SampleQuality quality = SampleQuality.Unavailable) =>
        new(id, null, MetricRegistry.ById[id].Unit, DateTimeOffset.UtcNow,
            MetricRegistry.ById[id].Source, quality, reason);
    public static MetricSample Number(string id, double value, string source, string? text = null) =>
        double.IsFinite(value)
            ? new(id, value, MetricRegistry.ById[id].Unit, DateTimeOffset.UtcNow, source, Text: text)
            : Missing(id, "The source returned a non-finite value.", SampleQuality.Error);
    public static MetricSample State(string id, string text, string source) =>
        new(id, null, MetricRegistry.ById[id].Unit, DateTimeOffset.UtcNow, source, Text: text);
}
