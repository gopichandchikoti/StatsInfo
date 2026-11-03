using StatsInfo.Models;

namespace StatsInfo.Services;

public static class MetricFormatter
{
    public static string Format(MetricSample sample, MetricPreference preference, AppSettings settings,
        IReadOnlyDictionary<string, MetricSample>? samples = null)
    {
        if (sample.Quality == SampleQuality.Stale) return "Stale";
        if (sample.Quality == SampleQuality.WarmingUp) return "Warming up";
        if (sample.Quality != SampleQuality.Valid) return "N/A";
        string format = preference.Decimals is >= 0 and <= 3 ? $"F{preference.Decimals}" : "0.##";
        string unit = string.IsNullOrEmpty(preference.Unit) || preference.Unit == "Default" ? sample.Unit : preference.Unit;
        if (sample.Unit == "Mbps" && preference.Unit is "" or "Default" && sample.Id.StartsWith("network."))
            unit = settings.NetworkUnit;
        if (sample.Values is { Count: > 0 })
            return string.Join(" · ", sample.Values.Take(4).Select(p => $"{p.Key}: {ConvertValue(p.Value, sample.Unit, unit).ToString(format)} {unit}"))
                + (sample.Values.Count > 4 ? $" (+{sample.Values.Count - 4})" : "");
        if (sample.Text != null && preference.Unit is "" or "Default") return sample.Text;
        if (sample.Value is not double value) return sample.Text ?? "N/A";
        if (unit == "%" && sample.Unit == "GiB" && samples != null)
        {
            string totalId = sample.Id == "memory.physical.used" ? "memory.physical.total" : "gpu.vram.total";
            if (!samples.TryGetValue(totalId, out var total) || !total.IsNumeric || total.Value <= 0) return "N/A";
            return (100 * value / total.Value)!.Value.ToString(format) + "%";
        }
        return $"{ConvertValue(value, sample.Unit, unit).ToString(format)} {unit}".Trim();
    }

    public static double ConvertValue(double value, string from, string to) => (from, to) switch
    {
        ("°C", "°F") => value * 9 / 5 + 32,
        ("Mbps", "MB/s") => value / 8,
        ("GiB", "GB") => value * 1073741824 / 1000000000,
        ("MHz", "GHz") => value / 1000,
        ("GHz", "MHz") => value * 1000,
        _ => value
    };
}
