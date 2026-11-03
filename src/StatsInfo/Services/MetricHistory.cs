using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using StatsInfo.Models;

namespace StatsInfo.Services;

public sealed class MetricHistory
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Queue<MetricSample>> _series = new();
    public const int MaxSamplesPerMetric = 7200;
    public int RetentionSeconds { get; set; } = 60;

    public void Add(MetricSample sample)
    {
        lock (_gate)
        {
            if (!_series.TryGetValue(sample.Id, out var queue)) _series[sample.Id] = queue = new();
            queue.Enqueue(sample);
            var cutoff = sample.SampledAt.AddSeconds(-Math.Clamp(RetentionSeconds, 60, 3600));
            while (queue.Count > MaxSamplesPerMetric || queue.Peek().SampledAt < cutoff) queue.Dequeue();
        }
    }

    public MetricSample[] Get(string id)
    {
        lock (_gate) return _series.TryGetValue(id, out var queue) ? queue.ToArray() : [];
    }

    public MetricSample[] GetAll()
    {
        lock (_gate) return _series.Values.SelectMany(q => q).OrderBy(s => s.SampledAt).ToArray();
    }

    public void Clear() { lock (_gate) _series.Clear(); }

    public async Task ExportAsync(string path, bool json, CancellationToken token = default)
    {
        var samples = GetAll();
        if (json)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(samples, options), token);
            return;
        }
        var builder = new StringBuilder("metric_id,timestamp,value,unit,quality,source,reason,text,structured_values\r\n");
        foreach (var s in samples)
            builder.AppendLine(string.Join(",", new[] { s.Id, s.SampledAt.ToString("O"),
                s.Value?.ToString("R", CultureInfo.InvariantCulture) ?? "", s.Unit,
                JsonNamingPolicy.SnakeCaseLower.ConvertName(s.Quality.ToString()),
                s.Source, s.Reason ?? "", s.Text ?? "", s.Values == null ? "" : JsonSerializer.Serialize(s.Values) }.Select(Csv)));
        await File.WriteAllTextAsync(path, builder.ToString(), token);
    }

    private static string Csv(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
}
