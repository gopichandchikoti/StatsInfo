namespace StatsInfo.Models;

public static class MetricRegistry
{
    private const string Catalog = """
cpu.usage.total|CPU|Total CPU utilization|CPU|%|Windows Processor counter|Sampled utilization across logical processors.
cpu.temperature.package|CPU|CPU package temperature|CPU temp|°C|LibreHardwareMonitor|Package sensor only; no per-core substitution.
cpu.power.package|CPU|CPU package power|CPU power|W|LibreHardwareMonitor|Package power, not whole-system power.
cpu.clock.effective|CPU|Effective CPU clock|CPU clock|GHz|LibreHardwareMonitor|Average of explicitly effective core clocks, not requested clock.
cpu.usage.per_core|CPU|Logical processor utilization|Cores|%|Windows Processor counters|Individual logical processor busy time.
cpu.temperature.per_core|CPU|Per-core CPU temperatures|Core temps|°C|LibreHardwareMonitor|Individual exposed core temperature sensors.
cpu.voltage|CPU|CPU voltage|CPU volts|V|LibreHardwareMonitor|Sensor identity is included in the source.
cpu.throttling|CPU|CPU throttling flags|CPU limit||LibreHardwareMonitor|Requires explicit thermal or power throttling flags.
cpu.fan.rpm|CPU|CPU cooling fan|CPU fan|RPM|LibreHardwareMonitor|Requires a clearly identified CPU fan sensor.
cpu.logical_processors|CPU|Logical processor count|Threads|count|Environment.ProcessorCount|Processor count available to this application.
gpu.usage.total|GPU|GPU utilization|GPU|%|Windows GPU Engine / LibreHardwareMonitor|Busiest engine, never a sum of parallel engines; selected adapter only.
gpu.temperature.core|GPU|GPU core temperature|GPU temp|°C|LibreHardwareMonitor|Core temperature; not inferred hotspot.
gpu.temperature.hotspot|GPU|GPU hotspot temperature|Hotspot|°C|LibreHardwareMonitor|Explicit hotspot sensor required.
gpu.power.board|GPU|GPU board power|GPU power|W|LibreHardwareMonitor|Explicit board or total GPU power sensor; not chip-only power.
gpu.clock.core|GPU|GPU core clock|GPU clock|MHz|LibreHardwareMonitor|Exposed core clock for the selected adapter.
gpu.clock.memory|GPU|GPU memory clock|VRAM clock|MHz|LibreHardwareMonitor|Reported memory clock; no effective-rate multiplier.
gpu.vram.used|GPU|Dedicated VRAM used|VRAM|GiB|Windows GPU Adapter Memory / LibreHardwareMonitor|Dedicated usage, excluding shared GPU memory.
gpu.vram.total|GPU|Dedicated VRAM capacity|VRAM total|GiB|Windows DXGI / LibreHardwareMonitor|64-bit dedicated capacity; shared memory and truncated WMI AdapterRAM are not substituted.
gpu.fan.rpm|GPU|GPU fan speed|GPU fan|RPM|LibreHardwareMonitor|Actual fan RPM; unavailable for fanless devices.
gpu.fan.percent|GPU|GPU fan duty|Fan duty|%|LibreHardwareMonitor|Fan control percentage, which may differ from actual RPM.
gpu.engines|GPU|GPU engine breakdown|Engines|%|Windows GPU Engine counters|Maximum per physical engine; parallel engines are not summed.
gpu.throttling|GPU|GPU performance limit reason|GPU limit||LibreHardwareMonitor|Only explicit vendor limit sensors; absence is unknown.
gpu.active_adapter|GPU|Monitored GPU identity|Adapter||Windows GPU counters / LibreHardwareMonitor|Selected monitored adapter; does not imply foreground rendering attribution.
memory.physical.used|Memory|Physical RAM used|RAM|GiB|GlobalMemoryStatusEx|Usable physical RAM minus available RAM; legacy used/total composite.
memory.physical.total|Memory|Usable physical RAM|RAM total|GiB|GlobalMemoryStatusEx|Usable physical capacity, not installed DIMM capacity.
memory.physical.available|Memory|Available physical RAM|RAM free|GiB|GlobalMemoryStatusEx|Available physical memory reported by Windows.
memory.commit.used|Memory|Committed memory|Commit|GiB|GetPerformanceInfo|CommitTotal multiplied by page size; not physical RAM used.
memory.commit.limit|Memory|Commit limit|Commit max|GiB|GetPerformanceInfo|System commit limit multiplied by page size.
memory.pressure|Memory|Calculated memory pressure|Pressure||GlobalMemoryStatusEx|App classification: below 80% normal, 80–89% elevated, 90%+ high physical usage.
storage.temperature|Storage|Drive temperature|Disk temp|°C|LibreHardwareMonitor|Selected physical drive sensor, when exposed.
storage.read.rate|Storage|Disk read throughput|Read|MB/s|Windows PhysicalDisk counters|Selected physical disk read bytes per second, decimal MB/s.
storage.write.rate|Storage|Disk write throughput|Write|MB/s|Windows PhysicalDisk counters|Selected physical disk write bytes per second, decimal MB/s.
storage.active_time|Storage|Disk active time|Disk busy|%|Windows PhysicalDisk counters|100 minus idle time, bounded 0–100%; not capacity used.
storage.capacity.free|Storage|Volume free space|Disk free|GiB|DriveInfo.AvailableFreeSpace|Free space available to this user on the selected volume.
storage.health.status|Storage|Drive health warning|Health||SMART/NVMe telemetry|No good-health inference from absent SMART data.
storage.endurance.used|Storage|NVMe percentage used|Life used|%|LibreHardwareMonitor|Explicit percentage-used sensor only; vendor-defined endurance estimate.
network.download.rate|Network|Download throughput|↓|Mbps|NetworkInterface byte delta|Selected adapter receive bytes over a monotonic interval; not a speed test.
network.upload.rate|Network|Upload throughput|↑|Mbps|NetworkInterface byte delta|Selected adapter send bytes over a monotonic interval; not a speed test.
network.ping|Network|Probe round-trip latency|Ping|ms|System.Net.NetworkInformation.Ping|Opt-in ICMP probes to the chosen endpoint.
network.packet_loss|Network|Probe packet loss|Loss|%|ICMP probe window|Failed probes divided by the last 60 attempts; not global network loss.
network.jitter|Network|Probe latency variation|Jitter|ms|ICMP probe window|Mean absolute difference of consecutive successful RTTs in last 60 attempts; gaps break pairs.
wifi.signal|Network|Wi-Fi signal quality|Wi-Fi|%|Windows WLAN API|Driver signal quality percentage; not throughput or dBm.
wifi.link_speed|Network|Wi-Fi negotiated receive link|Wi-Fi link|Mbps|Windows WLAN API|Negotiated receive PHY rate, not measured internet speed.
wifi.band|Network|Wi-Fi frequency band|Band||Windows WLAN API|Band determined from current connected frequency where available.
wifi.channel|Network|Wi-Fi channel|Channel|channel|Windows WLAN API|Current channel reported by the adapter.
network.adapter|Network|Monitored network adapter|Network||NetworkInterface|Auto or explicit adapter; aggregate mode may double-count VPN traffic.
network.daily.received|Network|Downloaded today while monitoring|Today ↓|GiB|Persisted adapter byte deltas|Local-day total observed by this app; paused/offline periods are excluded.
network.daily.sent|Network|Uploaded today while monitoring|Today ↑|GiB|Persisted adapter byte deltas|Local-day total observed by this app; counters reset safely.
display.refresh.configured|Display/Gaming|Configured monitor refresh|Display|Hz|QueryDisplayConfig|Selected monitor configured rational refresh; never game FPS or instantaneous VRR.
game.fps|Display/Gaming|Captured game FPS|FPS|FPS|PresentMon ETW console|Explicitly selected process and swap chain; capture must be started by the user.
game.frame_time|Display/Gaming|Presented frame interval|Frame|ms|PresentMon MsBetweenPresents|Mean actual present interval in the latest second; not display Hz.
game.fps.low1|Display/Gaming|1% low FPS|1% low|FPS|PresentMon frame window|1000 divided by mean of slowest 1% intervals in 60 seconds; at least 100 frames.
game.fps.low01|Display/Gaming|0.1% low FPS|0.1% low|FPS|PresentMon frame window|1000 divided by mean of slowest 0.1% intervals in 60 seconds; at least 1000 frames.
game.frame_time.graph|Display/Gaming|Frame-time trend|Frame peak|ms|PresentMon frame window|Trend of maximum actual interval in each latest-second window, retaining frame spikes.
display.resolution|Display/Gaming|Selected display resolution|Resolution|px|EnumDisplaySettingsEx|Active mode of the selected monitor.
display.hdr|Display/Gaming|Current HDR state|HDR||DisplayConfigGetDeviceInfo|Current advanced-color enabled flag; not panel capability alone.
display.vrr|Display/Gaming|VRR state|VRR||Windows display configuration|Unknown unless a reliable current-state source is available.
display.scaling|Display/Gaming|Per-monitor scaling|DPI|%|GetDpiForMonitor|Effective Windows DPI for selected monitor divided by 96.
game.render_gpu|Display/Gaming|Game rendering GPU|Render GPU||Process-specific frame attribution|Unknown without reliable target-process GPU attribution.
system.uptime|System/Cooling|Time since Windows boot|UP|h|GetTickCount64|Windows uptime may include Fast Startup behavior.
system.power.total|System/Cooling|Measured total system power|System power|W|External power meter|Requires a reliable whole-system meter; never sums partial CPU/GPU sensors.
system.motherboard.temperature|System/Cooling|Motherboard temperature|Board temp|°C|LibreHardwareMonitor|Selected motherboard sensor; device dependent.
system.fans|System/Cooling|System fan speeds|Fans|RPM|LibreHardwareMonitor|Exposed motherboard fan sensors, each labeled individually.
battery.percent|Battery|Battery charge|Battery|%|GetSystemPowerStatus|Windows aggregate charge; unavailable without a battery.
battery.discharge.rate|Battery|Battery discharge power|Battery W|W|Windows BatteryStatus WMI|Positive discharge, negative charge; driver-reported rate.
battery.health|Battery|Estimated battery health|Health|%|Windows battery capacity WMI|Full-charge/design capacity ratio; estimate rather than a safety diagnosis.
process.top_cpu|Process|Highest sampled CPU process|Top CPU|%|Process.TotalProcessorTime|CPU delta divided by elapsed time and logical processor count; inaccessible processes excluded.
process.top_ram|Process|Largest process working set|Top RAM|GiB|Process.WorkingSet64|Working set, not private bytes; process name only.
process.top_gpu|Process|Highest sampled GPU process|Top GPU|%|Windows GPU Engine counters|Maximum engine usage per process; inaccessible names are omitted.
system.power_mode|System/Cooling|Windows effective power mode|Power mode||PowerGetEffectiveOverlayScheme|Effective power overlay, distinct from the active power plan.
""";

    public static IReadOnlyList<MetricDefinition> All { get; } = Catalog.Split('\n', StringSplitOptions.RemoveEmptyEntries)
        .Select((line, index) => Parse(line.Trim(), index)).ToArray();
    public static IReadOnlyDictionary<string, MetricDefinition> ById { get; } = All.ToDictionary(d => d.Id);
    public static IReadOnlyDictionary<string, string[]> Presets { get; } = new Dictionary<string, string[]>
    {
        ["Minimal"] = ["cpu.usage.total", "gpu.usage.total", "memory.physical.used",
            "network.download.rate", "network.upload.rate", "display.refresh.configured"],
        ["Everyday"] = ["cpu.usage.total", "cpu.temperature.package", "gpu.usage.total", "gpu.temperature.core",
            "memory.physical.used", "gpu.vram.used", "display.refresh.configured", "network.download.rate",
            "network.upload.rate", "network.ping", "storage.read.rate", "storage.write.rate"],
        ["Gaming"] = ["game.fps", "game.frame_time", "game.frame_time.graph", "game.fps.low1", "gpu.usage.total", "gpu.temperature.core",
            "gpu.power.board", "gpu.vram.used", "cpu.usage.total", "cpu.temperature.package",
            "display.refresh.configured", "network.ping"],
        ["Network"] = ["network.download.rate", "network.upload.rate", "network.ping", "network.packet_loss",
            "network.jitter", "wifi.signal", "wifi.link_speed", "network.adapter"],
        ["Hardware Health"] = ["cpu.temperature.package", "gpu.temperature.core", "gpu.temperature.hotspot",
            "cpu.power.package", "gpu.power.board", "cpu.throttling", "gpu.throttling", "storage.temperature",
            "system.fans", "battery.health"]
    };

    private static MetricDefinition Parse(string line, int order)
    {
        var p = line.Split('|');
        var visible = p[0] is "cpu.usage.total" or "cpu.temperature.package" or "gpu.usage.total"
            or "gpu.temperature.core" or "memory.physical.used" or "network.download.rate"
            or "network.upload.rate" or "display.refresh.configured" or "system.uptime";
        return new(p[0], p[1], p[2], p[3], p[4], p[5], p[6], order, visible);
    }
}
