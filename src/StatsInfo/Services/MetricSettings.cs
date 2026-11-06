using StatsInfo.Models;

namespace StatsInfo.Services;

public static class MetricSettings
{
    public static void Normalize(AppSettings s)
    {
        s.Metrics ??= new();
        s.CategoryIntervals ??= new();
        s.Alerts ??= new();
        s.SavedCustomVisibility ??= new();
        s.SensorSelections ??= new();
        s.MonitorPositions ??= new();
        s.SensorSelections.TryAdd("cpu.fan.rpm", "");
        s.SensorSelections.TryAdd("system.motherboard.temperature", "");
        foreach (var d in MetricRegistry.All)
        {
            if (!s.Metrics.TryGetValue(d.Id, out var pref) || pref == null)
                s.Metrics[d.Id] = new() { Visible = LegacyVisibility(s, d), Order = d.SortOrder };
            s.Metrics[d.Id].Unit ??= "";
            s.Metrics[d.Id].Label ??= "";
            s.Metrics[d.Id].Decimals = Math.Clamp(s.Metrics[d.Id].Decimals, -1, 3);
        }
        s.SchemaVersion = 2;
        s.Opacity = FiniteClamp(s.Opacity, 0.2, 1, 0.85);
        s.Scale = FiniteClamp(s.Scale, 0.75, 2, 1);
        s.OverlayWidth = FiniteClamp(s.OverlayWidth, 0, 20000, 0);
        s.OverlayHeight = FiniteClamp(s.OverlayHeight, 0, 20000, 0);
        s.FontSize = FiniteClamp(s.FontSize, 10, 24, 12);
        s.MetricSpacing = FiniteClamp(s.MetricSpacing, 0, 24, 6);
        s.UpdateIntervalMs = Math.Clamp(s.UpdateIntervalMs, 500, 10000);
        s.HistorySeconds = Math.Clamp(s.HistorySeconds, 60, 3600);
        s.HistoryRetentionDays = Math.Clamp(s.HistoryRetentionDays, 1, 30);
        s.LatencyIntervalSeconds = Math.Clamp(s.LatencyIntervalSeconds, 2, 300);
        s.CaptureTimeoutSeconds = Math.Clamp(s.CaptureTimeoutSeconds, 10, 3600);
        foreach (var key in s.CategoryIntervals.Keys.ToArray())
            s.CategoryIntervals[key] = Math.Clamp(s.CategoryIntervals[key], 500, 60000);
        foreach (var a in s.Alerts)
        {
            a.SustainedSeconds = Math.Clamp(a.SustainedSeconds, 1, 3600);
            a.CooldownSeconds = Math.Clamp(a.CooldownSeconds, 1, 86400);
        }
    }

    private static double FiniteClamp(double value, double min, double max, double fallback) =>
        double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;

    private static bool LegacyVisibility(AppSettings s, MetricDefinition d) => d.Id switch
    {
        "cpu.usage.total" => s.ShowCpuUsage,
        "cpu.temperature.package" => s.ShowCpuTemp,
        "gpu.usage.total" => s.ShowGpuUsage,
        "gpu.temperature.core" => s.ShowGpuTemp,
        "memory.physical.used" => s.ShowRam,
        "network.download.rate" or "network.upload.rate" => s.ShowNetwork,
        "display.refresh.configured" => s.ShowDisplayHz,
        "game.fps" => s.ShowFps,
        _ => d.DefaultVisibility
    };

    public static void SetVisible(AppSettings s, string id, bool visible)
    {
        if (!s.Metrics.ContainsKey(id)) Normalize(s);
        s.Metrics[id].Visible = visible;
        switch (id)
        {
            case "cpu.usage.total": s.ShowCpuUsage = visible; break;
            case "cpu.temperature.package": s.ShowCpuTemp = visible; break;
            case "gpu.usage.total": s.ShowGpuUsage = visible; break;
            case "gpu.temperature.core": s.ShowGpuTemp = visible; break;
            case "memory.physical.used": s.ShowRam = visible; break;
            case "display.refresh.configured": s.ShowDisplayHz = visible; break;
            case "game.fps": s.ShowFps = visible; break;
        }
        s.ShowNetwork = s.Metrics["network.download.rate"].Visible || s.Metrics["network.upload.rate"].Visible;
    }

    public static void ApplyPreset(AppSettings s, string preset)
    {
        Normalize(s);
        if (preset == "Custom")
        {
            foreach (var pair in s.SavedCustomVisibility) SetVisible(s, pair.Key, pair.Value);
        }
        else if (MetricRegistry.Presets.TryGetValue(preset, out var ids))
        {
            if (s.Preset == "Custom")
                s.SavedCustomVisibility = s.Metrics.ToDictionary(p => p.Key, p => p.Value.Visible);
            foreach (var d in MetricRegistry.All) SetVisible(s, d.Id, ids.Contains(d.Id));
        }
        s.Preset = preset;
    }
}
