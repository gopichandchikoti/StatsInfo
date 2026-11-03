using System.Management;
using StatsInfo.Helpers;

namespace StatsInfo.Services;

public static class HardwareInventory
{
    public static Task<string> ReadAsync() => Task.Run(() =>
    {
        var lines = new List<string>();
        Query(lines, "Win32_VideoController", "Name,DriverVersion", row =>
            $"GPU: {row["Name"]} · driver {row["DriverVersion"]} (name only; counter LUID attribution is separate)");
        Query(lines, "Win32_DiskDrive", "Index,Model,Size", row =>
            $"Physical disk {row["Index"]}: {row["Model"]} · {Convert.ToDouble(row["Size"]) / 1073741824.0:0.0} GiB");
        foreach (var adapter in NetworkMetricsSource.Adapters().Where(a => a.Id is not "Auto" and not "All"))
            lines.Add($"Interface: {adapter.Name}");
        foreach (var display in DisplayInterop.Monitors())
            lines.Add($"Display: {display.Label} · {display.DpiScale * 100:0}% scaling");
        lines.Add($"Inventory read: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}. No serial-number fields queried.");
        return string.Join("\n", lines);
    });

    private static void Query(List<string> lines, string type, string fields, Func<ManagementObject, string> format)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("root\\CIMV2", $"SELECT {fields} FROM {type}");
            searcher.Options.Timeout = TimeSpan.FromSeconds(3);
            using var results = searcher.Get();
            foreach (ManagementObject item in results)
            {
                using (item) lines.Add(format(item));
            }
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or
            System.Runtime.InteropServices.COMException or FormatException)
        { lines.Add($"{type} inventory unavailable ({ex.GetType().Name})."); }
    }
}
