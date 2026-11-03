namespace StatsInfo.Models;

public class AppSettings
{
    public double Opacity { get; set; } = 0.85;
    public double Scale { get; set; } = 1.0;
    public string Theme { get; set; } = "Dark"; // Dark, Light, System
    public string AccentColor { get; set; } = "#3B82F6";
    public int UpdateIntervalMs { get; set; } = 1000;
    public bool AlwaysOnTop { get; set; } = true;
    public bool ClickThrough { get; set; } = false;
    public bool HideInFullscreen { get; set; } = false;
    public bool StartWithWindows { get; set; } = false;
    public string Position { get; set; } = "TopCenter"; // TopLeft, TopCenter, TopRight, Custom
    public double CustomX { get; set; } = 100;
    public double CustomY { get; set; } = 20;
    public int TargetMonitorIndex { get; set; } = 0;

    // Per-metric visibility
    public bool ShowCpuUsage { get; set; } = true;
    public bool ShowCpuTemp { get; set; } = true;
    public bool ShowGpuUsage { get; set; } = true;
    public bool ShowGpuTemp { get; set; } = true;
    public bool ShowRam { get; set; } = true;
    public bool ShowNetwork { get; set; } = true;
    public bool ShowDisplayHz { get; set; } = true;
    public bool ShowFps { get; set; } = false;

    // Units
    public string NetworkUnit { get; set; } = "Mbps"; // Mbps or MB/s
}
