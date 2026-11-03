using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using StatsInfo.Models;

namespace StatsInfo.Services;

public static class WifiReader
{
    private static readonly string[] Ids = ["wifi.signal", "wifi.link_speed", "wifi.channel", "wifi.band"];
    public static IReadOnlyList<MetricSample> Read(NetworkInterface[] selected)
    {
        var wifi = selected.Where(n => n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211).ToArray();
        if (wifi.Length != 1) return Missing("Select one connected Wi-Fi adapter to read link information.");
        if (!Guid.TryParse(wifi[0].Id, out var guid)) return Missing("Adapter does not expose a WLAN interface GUID.");
        uint error = WlanOpenHandle(2, IntPtr.Zero, out _, out var handle);
        if (error != 0) return Missing($"WLAN service unavailable (Windows error {error}).");
        try { return ReadConnection(handle, guid); }
        finally { WlanCloseHandle(handle, IntPtr.Zero); }
    }

    private static IReadOnlyList<MetricSample> ReadConnection(IntPtr handle, Guid guid)
    {
        uint error = WlanQueryInterface(handle, ref guid, 7, IntPtr.Zero, out var size, out var data, out _);
        if (error != 0) return Missing(error == 5
            ? "Windows denied WLAN access; review Windows location/Wi-Fi permission manually."
            : $"Current Wi-Fi connection unavailable (Windows error {error}).",
            error == 5 ? SampleQuality.PermissionDenied : SampleQuality.Unavailable);
        var result = new List<MetricSample>();
        try
        {
            // WLAN_CONNECTION_ATTRIBUTES: 520-byte header; ASSOCIATION signal/RX fields at +56/+60.
            // SSID, BSSID and profile name are deliberately never marshalled or retained.
            if (size < 588) return Missing("WLAN returned an incomplete connection structure.");
            uint quality = unchecked((uint)Marshal.ReadInt32(data, 576));
            uint rxKbps = unchecked((uint)Marshal.ReadInt32(data, 580));
            result.Add(quality <= 100 ? MetricSample.Number("wifi.signal", quality, "WlanQueryInterface signal quality")
                : MetricSample.Missing("wifi.signal", "Invalid signal quality from WLAN driver."));
            result.Add(rxKbps > 0 ? MetricSample.Number("wifi.link_speed", rxKbps / 1000.0, "WLAN negotiated receive PHY rate")
                : MetricSample.Missing("wifi.link_speed", "Driver did not expose negotiated receive rate."));
            string? band = ConnectedBand(handle, guid, data, size);
            result.Add(band != null ? MetricSample.State("wifi.band", band, "WLAN cached connected BSS center frequency")
                : MetricSample.Missing("wifi.band", "Connected center frequency unavailable or access denied; channel alone is ambiguous for 6 GHz."));
        }
        finally { WlanFreeMemory(data); }
        error = WlanQueryInterface(handle, ref guid, 8, IntPtr.Zero, out size, out data, out _);
        if (error == 0)
        {
            try { result.Add(MetricSample.Number("wifi.channel", Marshal.ReadInt32(data), "WLAN channel_number")); }
            finally { WlanFreeMemory(data); }
        }
        else result.Add(MetricSample.Missing("wifi.channel", $"WLAN channel query failed ({error})."));
        return result;
    }

    private static string? ConnectedBand(IntPtr handle, Guid guid, IntPtr connection, uint connectionSize)
    {
        if (connectionSize < 604) return null;
        bool secure = Marshal.ReadInt32(connection, 588) != 0;
        uint error = WlanGetNetworkBssList(handle, ref guid, IntPtr.Add(connection, 520), 3, secure,
            IntPtr.Zero, out var list);
        if (error != 0) return null;
        try
        {
            int bytes = Marshal.ReadInt32(list), count = Marshal.ReadInt32(list, 4);
            if (count < 0 || count > 4096 || bytes < 8L + count * 360L) return null;
            for (int i = 0; i < count; i++)
            {
                var row = IntPtr.Add(list, 8 + i * 360);
                bool match = Enumerable.Range(0, 6).All(n => Marshal.ReadByte(row, 40 + n) ==
                    Marshal.ReadByte(connection, 560 + n));
                if (match) return FrequencyBand(unchecked((uint)Marshal.ReadInt32(row, 92)));
            }
        }
        finally { WlanFreeMemory(list); }
        return null;
    }

    public static string? FrequencyBand(uint khz) => khz switch
    {
        >= 2400000 and <= 2500000 => "2.4 GHz",
        >= 4900000 and < 5925000 => "5 GHz",
        >= 5925000 and <= 7125000 => "6 GHz",
        _ => null
    };

    private static MetricSample[] Missing(string reason, SampleQuality quality = SampleQuality.Unavailable) =>
        Ids.Select(id => MetricSample.Missing(id, reason, quality)).ToArray();
    [DllImport("wlanapi.dll")]
    private static extern uint WlanOpenHandle(uint version, IntPtr reserved, out uint negotiated, out IntPtr handle);
    [DllImport("wlanapi.dll")]
    private static extern uint WlanCloseHandle(IntPtr handle, IntPtr reserved);
    [DllImport("wlanapi.dll")]
    private static extern uint WlanQueryInterface(IntPtr handle, ref Guid guid, uint opcode, IntPtr reserved,
        out uint size, out IntPtr data, out uint valueType);
    [DllImport("wlanapi.dll")]
    private static extern void WlanFreeMemory(IntPtr data);
    [DllImport("wlanapi.dll")]
    private static extern uint WlanGetNetworkBssList(IntPtr handle, ref Guid guid, IntPtr ssid, uint bssType,
        [MarshalAs(UnmanagedType.Bool)] bool securityEnabled, IntPtr reserved, out IntPtr list);
}
