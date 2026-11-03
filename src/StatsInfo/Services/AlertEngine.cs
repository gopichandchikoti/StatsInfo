using StatsInfo.Models;

namespace StatsInfo.Services;

public sealed record AlertEvent(string MetricId, string Severity, double Value, string Unit, DateTimeOffset At);

public sealed class AlertEngine
{
    private sealed class State
    {
        public DateTimeOffset? Since;
        public DateTimeOffset? CriticalSince;
        public DateTimeOffset LastSent = DateTimeOffset.MinValue;
        public string? ActiveSeverity;
    }
    private readonly Dictionary<string, State> _states = new();
    public DateTimeOffset SnoozedUntil { get; set; } = DateTimeOffset.MinValue;

    public AlertEvent? Evaluate(MetricSample sample, AlertRule rule, bool suppressed = false)
    {
        if (!_states.TryGetValue(sample.Id, out var state)) _states[sample.Id] = state = new();
        if (!rule.Enabled || !sample.IsNumeric)
        {
            state.Since = state.CriticalSince = null;
            state.ActiveSeverity = null;
            return null;
        }
        bool below = rule.Comparison == "<=";
        double value = sample.Value!.Value;
        bool warning = below ? value <= rule.Warning : value >= rule.Warning;
        bool critical = below ? value <= rule.Critical : value >= rule.Critical;
        bool cleared = below ? value >= rule.Clear : value <= rule.Clear;
        if (cleared)
        {
            state.Since = state.CriticalSince = null;
            state.ActiveSeverity = null;
            return null;
        }
        if (!warning) state.Since = null;
        else state.Since ??= sample.SampledAt;
        if (!critical) state.CriticalSince = null;
        else state.CriticalSince ??= sample.SampledAt;
        var sustained = TimeSpan.FromSeconds(rule.SustainedSeconds);
        string? severity = state.CriticalSince is { } c && sample.SampledAt - c >= sustained ? "Critical" : null;
        if (severity == null && state.Since is { } w && sample.SampledAt - w >= sustained) severity = "Warning";
        if (severity == null || suppressed || sample.SampledAt < SnoozedUntil) return null;
        if (state.ActiveSeverity == severity) return null;
        if (sample.SampledAt - state.LastSent < TimeSpan.FromSeconds(rule.CooldownSeconds)) return null;
        state.LastSent = sample.SampledAt;
        state.ActiveSeverity = severity;
        return new(sample.Id, severity, value, sample.Unit, sample.SampledAt);
    }

    public static bool IsQuietHour(int hour, int start, int end) => start >= 0 && end >= 0 && start != end &&
        (start < end ? hour >= start && hour < end : hour >= start || hour < end);
}
