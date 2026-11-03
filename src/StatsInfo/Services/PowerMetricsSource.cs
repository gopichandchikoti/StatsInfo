using System.Management;
using System.Runtime.InteropServices;
using StatsInfo.Models;

namespace StatsInfo.Services;

public sealed class PowerMetricsSource : IMetricSource
{
    public string Category => "System/Cooling";
    public IReadOnlyList<string> MetricIds { get; } = ["system.uptime", "system.power_mode", "system.power.total",
        "battery.percent", "battery.discharge.rate", "battery.health"];
    public static bool IsOnBattery() => GetSystemPowerStatus(out var status) && status.AcLineStatus == 0;

    public Task<IReadOnlyList<MetricSample>> ReadAsync(AppSettings settings, IReadOnlySet<string> requested,
        CancellationToken token)
    {
        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        var result = new List<MetricSample>
        {
            MetricSample.Number("system.uptime", uptime.TotalHours, "GetTickCount64",
                $"{uptime.Days}d {uptime.Hours:D2}h {uptime.Minutes:D2}m"),
            MetricSample.Missing("system.power.total", "No whole-system power meter is connected; CPU/GPU power is partial.")
        };
        if (requested.Contains("system.power_mode")) result.Add(ReadPowerMode());
        bool hasBattery = GetSystemPowerStatus(out var power) && power.BatteryFlag != 128 && power.BatteryFlag != 255;
        result.Add(hasBattery && power.BatteryLifePercent <= 100
            ? MetricSample.Number("battery.percent", power.BatteryLifePercent, "GetSystemPowerStatus")
            : MetricSample.Missing("battery.percent", "No battery detected or charge is unknown."));
        if (requested.Contains("battery.discharge.rate") || requested.Contains("battery.health"))
            ReadBattery(result, hasBattery);
        return Task.FromResult<IReadOnlyList<MetricSample>>(result);
    }

    private static void ReadBattery(List<MetricSample> result, bool hasBattery)
    {
        if (!hasBattery)
        {
            result.Add(MetricSample.Missing("battery.health", "No battery detected."));
            result.Add(MetricSample.Missing("battery.discharge.rate", "No battery detected."));
            return;
        }
        var statuses = Query("BatteryStatus", "InstanceName,Discharging,Charging,DischargeRate,ChargeRate");
        var full = Query("BatteryFullChargedCapacity", "InstanceName,FullChargedCapacity");
        var design = Query("BatteryStaticData", "InstanceName,DesignedCapacity");
        var status = statuses.FirstOrDefault();
        double? rate = null;
        if (status != null && Convert.ToBoolean(status["Discharging"])) rate = ValidCapacity(status["DischargeRate"]);
        if (status != null && Convert.ToBoolean(status["Charging"])) rate = -ValidCapacity(status["ChargeRate"]);
        result.Add(rate is double r ? MetricSample.Number("battery.discharge.rate", r / 1000, "Windows BatteryStatus WMI")
            : MetricSample.Missing("battery.discharge.rate", "Battery driver did not expose a charge/discharge rate."));
        double? f = full.Select(o => ValidCapacity(o["FullChargedCapacity"])).FirstOrDefault();
        double? d = design.Where(o => full.Count > 0 && Equals(o["InstanceName"], full[0]["InstanceName"]))
            .Select(o => ValidCapacity(o["DesignedCapacity"])).FirstOrDefault();
        result.Add(f is > 0 && d is > 0
            ? MetricSample.Number("battery.health", 100 * f.Value / d.Value, "WMI full-charge/design capacity estimate")
            : MetricSample.Missing("battery.health", "Matching design/full-charge capacities were not exposed."));
        foreach (var item in statuses.Concat(full).Concat(design)) item.Dispose();
    }

    private static double? ValidCapacity(object? value)
    {
        double number = Convert.ToDouble(value);
        return number > 0 && number < uint.MaxValue ? number : null;
    }

    private static List<ManagementObject> Query(string type, string fields)
    {
        using var query = new ManagementObjectSearcher("root\\WMI", $"SELECT {fields} FROM {type}");
        using var results = query.Get();
        return results.Cast<ManagementObject>().ToList();
    }

    private static MetricSample ReadPowerMode()
    {
        if (PowerGetEffectiveOverlayScheme(out var guid) != 0)
            return MetricSample.Missing("system.power_mode", "Effective power overlay unavailable (not the power plan).");
        string mode = guid.ToString() switch
        {
            "ded574b5-45a0-4f42-8737-46345c09c238" => "Best efficiency",
            "00000000-0000-0000-0000-000000000000" => "Balanced",
            "3af9b8d9-7c97-431d-ad78-34a8bfea439f" => "Best performance",
            _ => "Custom effective overlay"
        };
        return MetricSample.State("system.power_mode", mode, "PowerGetEffectiveOverlayScheme");
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PowerStatus
    {
        public byte AcLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag;
        public uint BatteryLifeTime, BatteryFullLifeTime;
    }
    [DllImport("kernel32.dll")]
    private static extern bool GetSystemPowerStatus(out PowerStatus status);
    [DllImport("powrprof.dll")]
    private static extern uint PowerGetEffectiveOverlayScheme(out Guid scheme);
    public void Dispose() { }
}
