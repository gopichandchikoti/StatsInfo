namespace StatsInfo.Models;

public class AppSettings
{
    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? AdditionalSettings { get; set; }
    public int SchemaVersion { get; set; } = 1;
    public Dictionary<string, MetricPreference> Metrics { get; set; } = new();
    public string Layout { get; set; } = "Horizontal";
    public bool LockPosition { get; set; }
    public double FontSize { get; set; } = 12;
    public string FontFamily { get; set; } = "Segoe UI";
    public double MetricSpacing { get; set; } = 6;
    public string Density { get; set; } = "Comfortable";
    public string Preset { get; set; } = "Custom";
    public Dictionary<string, bool> SavedCustomVisibility { get; set; } = new();
    public bool Paused { get; set; }
    public bool LowImpactMode { get; set; }
    public bool AutoPauseOnBattery { get; set; }
    public Dictionary<string, int> CategoryIntervals { get; set; } = new();
    public int HistorySeconds { get; set; } = 60;
    public bool PersistHistory { get; set; }
    public int HistoryRetentionDays { get; set; } = 7;
    public string AdapterId { get; set; } = "Auto";
    public string GpuAdapter { get; set; } = "Auto";
    public string DiskInstance { get; set; } = "Auto";
    public string Volume { get; set; } = "C:\\";
    public bool LatencyEnabled { get; set; }
    public string LatencyEndpoint { get; set; } = "";
    public int LatencyIntervalSeconds { get; set; } = 5;
    public bool HardwareSensorsEnabled { get; set; }
    public string SensorGpuName { get; set; } = "Auto";
    public string StorageSensorName { get; set; } = "Auto";
    public Dictionary<string, string> SensorSelections { get; set; } = new();
    public string PresentMonPath { get; set; } = "";
    public int CaptureTimeoutSeconds { get; set; } = 300;
    public List<AlertRule> Alerts { get; set; } = new();
    public bool TrayNotifications { get; set; }
    public bool AlertSound { get; set; }
    public bool SuppressAlertsFullscreen { get; set; } = true;
    public int QuietStartHour { get; set; } = -1;
    public int QuietEndHour { get; set; } = -1;
    public string ToggleHotkey { get; set; } = "Ctrl+Shift+F11";
    public string RecoveryHotkey { get; set; } = "Ctrl+Shift+F10";
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
    public Dictionary<string, OverlayPosition> MonitorPositions { get; set; } = new();

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
