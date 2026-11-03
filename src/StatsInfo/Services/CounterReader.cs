using System.Diagnostics;

namespace StatsInfo.Services;

public class CounterReader : IDisposable
{
    private readonly Dictionary<string, PerformanceCounter> _counters = new();
    public virtual string[] Instances(string category) => new PerformanceCounterCategory(category).GetInstanceNames();
    public virtual double? Read(string category, string counter, string instance)
    {
        string key = $"{category}/{counter}/{instance}";
        if (!_counters.TryGetValue(key, out var reader))
        {
            if (_counters.Count >= 4096) throw new InvalidOperationException("Counter budget exceeded.");
            reader = new PerformanceCounter(category, counter, instance, true);
            _counters[key] = reader;
            reader.NextValue();
            return null;
        }
        double value = reader.NextValue();
        return double.IsFinite(value) ? value : null;
    }
    public virtual void Retain(string category, IReadOnlySet<string> instances)
    {
        foreach (string key in _counters.Keys.Where(k => k.StartsWith(category + "/", StringComparison.Ordinal) &&
            !instances.Contains(k[(k.LastIndexOf('/') + 1)..])).ToArray())
        {
            _counters[key].Dispose();
            _counters.Remove(key);
        }
    }
    public virtual void Dispose()
    {
        foreach (var counter in _counters.Values) counter.Dispose();
        _counters.Clear();
    }
}
