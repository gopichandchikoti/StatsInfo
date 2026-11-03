namespace StatsInfo.Models;

public sealed class AlertRule
{
    public string MetricId { get; set; } = "cpu.usage.total";
    public bool Enabled { get; set; }
    public string Comparison { get; set; } = ">=";
    public double Warning { get; set; } = 90;
    public double Critical { get; set; } = 98;
    public double Clear { get; set; } = 85;
    public int SustainedSeconds { get; set; } = 15;
    public int CooldownSeconds { get; set; } = 120;
    public string DeliveryChannel { get; set; } = "Default";
}
