using System.Diagnostics;
using System.IO;
using System.Text.Json;
using StatsInfo.Models;

namespace StatsInfo.Services;

public sealed class HistoryStore
{
    private readonly string _directory;
    private readonly object _gate = new();
    private long _lastWrite;
    public string? Error { get; private set; }
    public HistoryStore(string settingsPath) => _directory = Path.Combine(Path.GetDirectoryName(settingsPath)!, "history");

    public void Restore(MetricHistory history, AppSettings settings)
    {
        if (!settings.PersistHistory || !Directory.Exists(_directory)) return;
        try
        {
            var cutoff = DateTimeOffset.UtcNow.AddSeconds(-settings.HistorySeconds);
            foreach (string path in Files().Order().TakeLast(2))
            {
                if (new FileInfo(path).Length > 32 * 1024 * 1024) continue;
                var samples = JsonSerializer.Deserialize<MetricSample[]>(File.ReadAllText(path)) ?? [];
                foreach (var sample in samples.Where(s => s is { Id: not null, Unit: not null, Source: not null } &&
                    Enum.IsDefined(s.Quality) && MetricRegistry.ById.ContainsKey(s.Id) && s.SampledAt >= cutoff))
                    history.Add(sample);
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        { Error = $"History restore failed ({ex.GetType().Name})."; }
    }

    public void Save(MetricHistory history, AppSettings settings, bool force = false)
    {
        if (!settings.PersistHistory || !Monitor.TryEnter(_gate)) return;
        try
        {
            if (!force && Stopwatch.GetElapsedTime(_lastWrite).TotalSeconds < 30) return;
            _lastWrite = Stopwatch.GetTimestamp();
            Directory.CreateDirectory(_directory);
            var samples = history.GetAll().TakeLast(20000).ToArray();
            string data = JsonSerializer.Serialize(samples);
            if (System.Text.Encoding.UTF8.GetByteCount(data) > 32 * 1024 * 1024)
            { Error = "History snapshot exceeds the 32 MiB budget; skipped persistence."; return; }
            string path = Path.Combine(_directory, $"history-{DateTime.Today:yyyy-MM-dd}.json");
            File.WriteAllText(path + ".tmp", data);
            File.Move(path + ".tmp", path, true);
            foreach (string old in Files().Where(p => File.GetLastWriteTime(p) < DateTime.Now.AddDays(-settings.HistoryRetentionDays)))
                File.Delete(old);
            Error = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        { Error = $"History persistence failed ({ex.GetType().Name})."; }
        finally { Monitor.Exit(_gate); }
    }

    public void Clear()
    {
        lock (_gate)
        {
            try
            {
                foreach (string path in Files()) File.Delete(path);
                Error = null;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            { Error = $"History could not be cleared ({ex.GetType().Name})."; }
        }
    }
    private IEnumerable<string> Files() => Directory.Exists(_directory)
        ? Directory.EnumerateFiles(_directory, "history-????-??-??.json", SearchOption.TopDirectoryOnly) : [];
}
