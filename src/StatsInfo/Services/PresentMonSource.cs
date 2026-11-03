using System.Diagnostics;
using System.Globalization;
using System.IO;
using StatsInfo.Models;

namespace StatsInfo.Services;

public sealed class PresentMonSource : IMetricSource
{
    private readonly object _gate = new();
    private readonly FrameWindow _frames = new();
    private Process? _process;
    private string _session = "";
    private string _executable = "";
    private string _swapChain = "";
    private int _target;
    private long _lastFrame;
    public string Status { get; private set; } = "N/A — no compatible frame source. Select a process and explicitly start capture.";
    public bool IsCapturing => _process is { HasExited: false };
    public string Category => "FPS";
    public IReadOnlyList<string> MetricIds { get; } = ["game.fps", "game.frame_time", "game.fps.low1",
        "game.fps.low01", "game.frame_time.graph", "game.render_gpu"];

    public void Start(string path, int processId, int timeoutSeconds)
    {
        Stop();
        if (!File.Exists(path) || !string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Choose the official PresentMon 2.x console executable (.exe).");
        using var target = Process.GetProcessById(processId);
        if (target.HasExited || processId == Environment.ProcessId) throw new ArgumentException("Select a running game/process.");
        _target = processId;
        _executable = Path.GetFullPath(path);
        _session = $"StatsInfo-{Environment.ProcessId}-{Guid.NewGuid():N}";
        var info = new ProcessStartInfo(_executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (string arg in new[] { "--process_id", processId.ToString(CultureInfo.InvariantCulture),
            "--output_stdout", "--v1_metrics", "--no_console_stats", "--no_track_input", "--session_name", _session,
            "--terminate_on_proc_exit", "--timed", Math.Clamp(timeoutSeconds, 10, 3600).ToString(CultureInfo.InvariantCulture),
            "--terminate_after_timed" }) info.ArgumentList.Add(arg);
        _process = Process.Start(info) ?? throw new InvalidOperationException("PresentMon did not start.");
        Status = $"Capturing PID {processId}; waiting for a compatible swap chain. No elevation or injection.";
        _ = PumpAsync(_process, processId);
    }

    private async Task PumpAsync(Process process, int pid)
    {
        var errorRead = process.StandardError.ReadToEndAsync();
        string[]? header = null;
        try
        {
            while (await process.StandardOutput.ReadLineAsync() is { } line)
            {
                if (line.Length > 16384) continue;
                var fields = FrameWindow.ParseCsv(line.TrimStart('\uFEFF'));
                if (fields.Contains("ProcessID") && fields.Contains("MsBetweenPresents")) { header = fields; continue; }
                if (header == null || !FrameWindow.TryInterval(header, fields, pid, out var swap, out var ms)) continue;
                int timestampColumn = Array.IndexOf(header, "TimeInSeconds");
                if (timestampColumn < 0 || fields.Length <= timestampColumn ||
                    !double.TryParse(fields[timestampColumn], NumberStyles.Float, CultureInfo.InvariantCulture,
                        out double frameSeconds) || !double.IsFinite(frameSeconds) || frameSeconds < 0) continue;
                lock (_gate)
                {
                    if (process != _process) break;
                    if (_swapChain.Length == 0) _swapChain = swap;
                    if (_swapChain != swap) continue;
                    _lastFrame = Stopwatch.GetTimestamp();
                    _frames.Add(frameSeconds, ms);
                    Status = $"Capturing PID {pid}, swap chain {_swapChain}; {_frames.Count} frames / rolling 60 s.";
                }
            }
            await process.WaitForExitAsync();
            await errorRead;
            if (process == _process) Status = $"Capture ended (code {process.ExitCode}); no compatible live frame source.";
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or ObjectDisposedException)
        { if (process == _process) Status = $"Capture stopped: {ex.GetType().Name}"; }
    }

    public Task<IReadOnlyList<MetricSample>> ReadAsync(AppSettings settings, IReadOnlySet<string> requested,
        CancellationToken token)
    {
        if (settings.Paused && IsCapturing) Stop();
        lock (_gate)
        {
            if (!IsCapturing || _frames.Count == 0 || Stopwatch.GetElapsedTime(_lastFrame).TotalSeconds > 3)
                return Task.FromResult<IReadOnlyList<MetricSample>>(MetricIds.Select(id => MetricSample.Missing(id,
                    Status + " Frames may be blocked by permissions, protection, API support or fullscreen behavior.")).ToArray());
            var stats = _frames.Statistics();
            string source = $"PresentMon 2.x ETW MsBetweenPresents / PID {_target} / swap {_swapChain}; rolling 60 s lows";
            var result = new List<MetricSample>();
            foreach (var pair in new[] { ("game.fps", stats.Fps), ("game.frame_time", stats.FrameTime),
                ("game.fps.low1", stats.Low1), ("game.fps.low01", stats.Low01), ("game.frame_time.graph", stats.Peak) })
                result.Add(pair.Item2 is double value ? MetricSample.Number(pair.Item1, value, source)
                    : MetricSample.Missing(pair.Item1, "Not enough captured frames (100 for 1%, 1000 for 0.1% low).",
                        SampleQuality.WarmingUp));
            result.Add(MetricSample.Missing("game.render_gpu", "Unknown — frame source does not reliably attribute the rendering GPU."));
            return Task.FromResult<IReadOnlyList<MetricSample>>(result);
        }
    }

    public void Stop()
    {
        var process = _process;
        _process = null;
        if (process != null)
        {
            try { if (!process.HasExited) { process.Kill(); process.WaitForExit(2000); } }
            catch (InvalidOperationException) { /* It exited between the state check and stop. */ }
            process.Dispose();
            CleanupSession();
        }
        lock (_gate) { _frames.Clear(); _swapChain = ""; }
        Status = "N/A — capture stopped; no compatible frame source.";
    }

    private void CleanupSession()
    {
        var info = new ProcessStartInfo(_executable) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var arg in new[] { "--session_name", _session, "--terminate_existing_session" }) info.ArgumentList.Add(arg);
        try
        {
            using var cleanup = Process.Start(info);
            if (cleanup != null && !cleanup.WaitForExit(2000)) cleanup.Kill();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        { Status = "Capture stopped; owned ETW session cleanup could not be confirmed."; }
    }
    public void Dispose() => Stop();
}
