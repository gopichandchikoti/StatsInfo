using System.Globalization;
using System.Text;

namespace StatsInfo.Services;

public sealed class FrameWindow
{
    private readonly Queue<(double At, double Milliseconds)> _frames = new();
    public int Count => _frames.Count;
    public void Add(double at, double milliseconds)
    {
        if (!double.IsFinite(milliseconds) || milliseconds <= 0 || !double.IsFinite(at)) return;
        _frames.Enqueue((at, milliseconds));
        while (_frames.Count > 120000 || _frames.Peek().At < at - 60) _frames.Dequeue();
    }
    public (double? Fps, double? FrameTime, double? Low1, double? Low01, double? Peak) Statistics()
    {
        if (_frames.Count == 0) return (null, null, null, null, null);
        double latest = _frames.Last().At;
        var current = _frames.Where(f => f.At >= latest - 1).Select(f => f.Milliseconds).ToArray();
        var sorted = _frames.Select(f => f.Milliseconds).OrderDescending().ToArray();
        double frame = current.Average();
        return (1000 / frame, frame, Low(sorted, 0.01, 100), Low(sorted, 0.001, 1000), current.Max());
    }
    private static double? Low(double[] sorted, double fraction, int minimum) => sorted.Length >= minimum
        ? 1000 / sorted.Take((int)Math.Ceiling(sorted.Length * fraction)).Average() : null;
    public void Clear() => _frames.Clear();

    public static string[] ParseCsv(string line)
    {
        var result = new List<string>();
        var text = new StringBuilder();
        bool quoted = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"' && quoted && i + 1 < line.Length && line[i + 1] == '"') { text.Append('"'); i++; }
            else if (c == '"') quoted = !quoted;
            else if (c == ',' && !quoted) { result.Add(text.ToString()); text.Clear(); }
            else text.Append(c);
        }
        result.Add(text.ToString());
        return result.ToArray();
    }
    public static bool TryInterval(string[] columns, string[] values, int pid, out string swapChain, out double interval)
    {
        swapChain = "";
        interval = 0;
        int p = Array.IndexOf(columns, "ProcessID"), s = Array.IndexOf(columns, "SwapChainAddress");
        int ms = Array.IndexOf(columns, "MsBetweenPresents");
        if (p < 0 || s < 0 || ms < 0 || values.Length <= Math.Max(ms, Math.Max(p, s))) return false;
        if (!int.TryParse(values[p], out int actual) || actual != pid) return false;
        swapChain = values[s];
        return double.TryParse(values[ms], NumberStyles.Float, CultureInfo.InvariantCulture, out interval) &&
            double.IsFinite(interval) && interval > 0;
    }
}
