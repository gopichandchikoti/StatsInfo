# StatsInfo 2 feature and device matrix

All 70 catalog entries have independent persisted Visible Metrics controls. Implemented means the source/presentation exists; it does not certify hardware accuracy or compatibility on every PC.

The device column records the local all-metrics capture in `artifacts/qa/final-dxgi/live-metrics.json`: 35 valid, 35 unavailable, zero errors. Sensors, probes and game capture were deliberately off. No unavailable value was replaced with zero.

| Metric ID | Before v2 | Implementation | Local device result | Source |
| --- | --- | --- | --- | --- |
| `cpu.usage.total` | Existing | Implemented | Valid local reading | Windows Processor counter |
| `cpu.temperature.package` | Existing | Implemented; sensor opt-in and physical validation required | N/A: Hardware sensors are off. Explicitly enable LibreHardwareMonitor in Diagnostics; it may load a kernel driver. | LibreHardwareMonitor |
| `cpu.power.package` | Missing | Implemented; sensor opt-in and physical validation required | N/A: Hardware sensors are off. Explicitly enable LibreHardwareMonitor in Diagnostics; it may load a kernel driver. | LibreHardwareMonitor |
| `cpu.clock.effective` | Missing | Implemented; sensor opt-in and physical validation required | N/A: Hardware sensors are off. Explicitly enable LibreHardwareMonitor in Diagnostics; it may load a kernel driver. | LibreHardwareMonitor |
| `cpu.usage.per_core` | Missing | Implemented | Valid local reading | Windows Processor counters |
| `cpu.temperature.per_core` | Missing | Implemented; sensor opt-in and physical validation required | N/A: Hardware sensors are off. Explicitly enable LibreHardwareMonitor in Diagnostics; it may load a kernel driver. | LibreHardwareMonitor |
| `cpu.voltage` | Missing | Implemented; sensor opt-in and physical validation required | N/A: Hardware sensors are off. Explicitly enable LibreHardwareMonitor in Diagnostics; it may load a kernel driver. | LibreHardwareMonitor |
| `cpu.throttling` | Missing | Deferred source; selectable N/A/Unknown | N/A: Hardware sensors are off. Explicitly enable LibreHardwareMonitor in Diagnostics; it may load a kernel driver. | LibreHardwareMonitor |
| `cpu.fan.rpm` | Missing | Implemented; sensor opt-in and physical validation required | N/A: Hardware sensors are off. Explicitly enable LibreHardwareMonitor in Diagnostics; it may load a kernel driver. | LibreHardwareMonitor |
| `cpu.logical_processors` | Missing | Implemented | Valid local reading | Environment.ProcessorCount |
| `gpu.usage.total` | Existing | Implemented | Valid local reading | Windows GPU Engine / LibreHardwareMonitor |
| `gpu.temperature.core` | Existing | Implemented; sensor opt-in and physical validation required | N/A: Hardware sensors are off. Explicitly enable LibreHardwareMonitor in Diagnostics; it may load a kernel driver. | LibreHardwareMonitor |
| `gpu.temperature.hotspot` | Missing | Implemented; sensor opt-in and physical validation required | N/A: Hardware sensors are off. Explicitly enable LibreHardwareMonitor in Diagnostics; it may load a kernel driver. | LibreHardwareMonitor |
| `gpu.power.board` | Missing | Implemented; sensor opt-in and physical validation required | N/A: Hardware sensors are off. Explicitly enable LibreHardwareMonitor in Diagnostics; it may load a kernel driver. | LibreHardwareMonitor |
| `gpu.clock.core` | Missing | Implemented; sensor opt-in and physical validation required | N/A: Hardware sensors are off. Explicitly enable LibreHardwareMonitor in Diagnostics; it may load a kernel driver. | LibreHardwareMonitor |
| `gpu.clock.memory` | Missing | Implemented; sensor opt-in and physical validation required | N/A: Hardware sensors are off. Explicitly enable LibreHardwareMonitor in Diagnostics; it may load a kernel driver. | LibreHardwareMonitor |
| `gpu.vram.used` | Missing | Implemented | Valid local reading | Windows GPU Adapter Memory / LibreHardwareMonitor |
| `gpu.vram.total` | Missing | Implemented: native DXGI or opt-in sensors | Valid local reading (DXGI-reported dedicated capacity) | Windows DXGI / LibreHardwareMonitor |
| `gpu.fan.rpm` | Missing | Implemented; sensor opt-in and physical validation required | N/A: Hardware sensors are off. Explicitly enable LibreHardwareMonitor in Diagnostics; it may load a kernel driver. | LibreHardwareMonitor |
| `gpu.fan.percent` | Missing | Implemented; sensor opt-in and physical validation required | N/A: Hardware sensors are off. Explicitly enable LibreHardwareMonitor in Diagnostics; it may load a kernel driver. | LibreHardwareMonitor |
| `gpu.engines` | Missing | Implemented | Valid local reading | Windows GPU Engine counters |
| `gpu.throttling` | Missing | Deferred source; selectable N/A/Unknown | N/A: Hardware sensors are off. Explicitly enable LibreHardwareMonitor in Diagnostics; it may load a kernel driver. | LibreHardwareMonitor |
| `gpu.active_adapter` | Missing | Implemented | Valid local reading | Windows GPU counters / LibreHardwareMonitor |
| `memory.physical.used` | Existing | Implemented | Valid local reading | GlobalMemoryStatusEx |
| `memory.physical.total` | Partial: combined display | Implemented | Valid local reading | GlobalMemoryStatusEx |
| `memory.physical.available` | Missing | Implemented | Valid local reading | GlobalMemoryStatusEx |
| `memory.commit.used` | Missing | Implemented | Valid local reading | GetPerformanceInfo |
| `memory.commit.limit` | Missing | Implemented | Valid local reading | GetPerformanceInfo |
| `memory.pressure` | Missing | Implemented | Valid local reading | GlobalMemoryStatusEx |
| `storage.temperature` | Missing | Implemented; sensor opt-in and physical validation required | N/A: Hardware sensors are off. Explicitly enable LibreHardwareMonitor in Diagnostics; it may load a kernel driver. | LibreHardwareMonitor |
| `storage.read.rate` | Missing | Implemented | Valid local reading | Windows PhysicalDisk counters |
| `storage.write.rate` | Missing | Implemented | Valid local reading | Windows PhysicalDisk counters |
| `storage.active_time` | Missing | Implemented | Valid local reading | Windows PhysicalDisk counters |
| `storage.capacity.free` | Missing | Implemented | Valid local reading | DriveInfo.AvailableFreeSpace |
| `storage.health.status` | Missing | Deferred source; selectable N/A/Unknown | N/A: Unknown — no reliable SMART/NVMe health-warning decoder is enabled; absent data is not healthy. | SMART/NVMe telemetry |
| `storage.endurance.used` | Missing | Implemented; sensor opt-in and physical validation required | N/A: Hardware sensors are off. Explicitly enable LibreHardwareMonitor in Diagnostics; it may load a kernel driver. | LibreHardwareMonitor |
| `network.download.rate` | Partial: combined display | Implemented | Valid local reading | NetworkInterface byte delta |
| `network.upload.rate` | Partial: combined display | Implemented | Valid local reading | NetworkInterface byte delta |
| `network.ping` | Missing | Implemented | N/A: Periodic ICMP probes are off. Choose an endpoint and explicitly enable probing in Network & FPS. | System.Net.NetworkInformation.Ping |
| `network.packet_loss` | Missing | Implemented | N/A: Periodic ICMP probes are off. Choose an endpoint and explicitly enable probing in Network & FPS. | ICMP probe window |
| `network.jitter` | Missing | Implemented | N/A: Periodic ICMP probes are off. Choose an endpoint and explicitly enable probing in Network & FPS. | ICMP probe window |
| `wifi.signal` | Missing | Implemented | Valid local reading | Windows WLAN API |
| `wifi.link_speed` | Missing | Implemented | Valid local reading | Windows WLAN API |
| `wifi.band` | Missing | Implemented | N/A: Connected center frequency unavailable or access denied; channel alone is ambiguous for 6 GHz. | Windows WLAN API |
| `wifi.channel` | Missing | Implemented | Valid local reading | Windows WLAN API |
| `network.adapter` | Missing | Implemented | Valid local reading | NetworkInterface |
| `network.daily.received` | Missing | Implemented | Valid local reading | Persisted adapter byte deltas |
| `network.daily.sent` | Missing | Implemented | Valid local reading | Persisted adapter byte deltas |
| `display.refresh.configured` | Existing | Implemented | Valid local reading | QueryDisplayConfig |
| `game.fps` | Missing | Optional adapter implemented; real game unverified | N/A: N/A — no compatible frame source. Select a process and explicitly start capture. Frames may be blocked by permissions, protection, API support or fullscreen behavior. | PresentMon ETW console |
| `game.frame_time` | Missing | Optional adapter implemented; real game unverified | N/A: N/A — no compatible frame source. Select a process and explicitly start capture. Frames may be blocked by permissions, protection, API support or fullscreen behavior. | PresentMon MsBetweenPresents |
| `game.fps.low1` | Missing | Optional adapter implemented; real game unverified | N/A: N/A — no compatible frame source. Select a process and explicitly start capture. Frames may be blocked by permissions, protection, API support or fullscreen behavior. | PresentMon frame window |
| `game.fps.low01` | Missing | Optional adapter implemented; real game unverified | N/A: N/A — no compatible frame source. Select a process and explicitly start capture. Frames may be blocked by permissions, protection, API support or fullscreen behavior. | PresentMon frame window |
| `game.frame_time.graph` | Missing | Optional adapter implemented; real game unverified | N/A: N/A — no compatible frame source. Select a process and explicitly start capture. Frames may be blocked by permissions, protection, API support or fullscreen behavior. | PresentMon frame window |
| `display.resolution` | Missing | Implemented | Valid local reading | EnumDisplaySettingsEx |
| `display.hdr` | Missing | Implemented | Valid local reading | DisplayConfigGetDeviceInfo |
| `display.vrr` | Missing | Deferred source; selectable N/A/Unknown | N/A: Unknown — no reliable current VRR-state source; configured Hz is not instantaneous VRR. | Windows display configuration |
| `display.scaling` | Missing | Implemented | Valid local reading | GetDpiForMonitor |
| `game.render_gpu` | Missing | Deferred source; selectable N/A/Unknown | N/A: N/A — no compatible frame source. Select a process and explicitly start capture. Frames may be blocked by permissions, protection, API support or fullscreen behavior. | Process-specific frame attribution |
| `system.uptime` | Existing | Implemented | Valid local reading | GetTickCount64 |
| `system.power.total` | Missing | Deferred source; selectable N/A/Unknown | N/A: No whole-system power meter is connected; CPU/GPU power is partial. | External power meter |
| `system.motherboard.temperature` | Missing | Implemented; sensor opt-in and physical validation required | N/A: Hardware sensors are off. Explicitly enable LibreHardwareMonitor in Diagnostics; it may load a kernel driver. | LibreHardwareMonitor |
| `system.fans` | Missing | Implemented; sensor opt-in and physical validation required | N/A: Hardware sensors are off. Explicitly enable LibreHardwareMonitor in Diagnostics; it may load a kernel driver. | LibreHardwareMonitor |
| `battery.percent` | Missing | Implemented | N/A: No battery detected or charge is unknown. | GetSystemPowerStatus |
| `battery.discharge.rate` | Missing | Implemented | N/A: No battery detected. | Windows BatteryStatus WMI |
| `battery.health` | Missing | Implemented | N/A: No battery detected. | Windows battery capacity WMI |
| `process.top_cpu` | Missing | Implemented | Valid local reading | Process.TotalProcessorTime |
| `process.top_ram` | Missing | Implemented | Valid local reading | Process.WorkingSet64 |
| `process.top_gpu` | Missing | Implemented | Valid local reading | Windows GPU Engine counters |
| `system.power_mode` | Missing | Implemented | Valid local reading | PowerGetEffectiveOverlayScheme |

## Capabilities

| Capability | Status |
| --- | --- |
| Legacy settings, toggles and custom properties | Implemented and tested; schema 1 JSON migrates without resetting visibility, appearance or placement |
| Search, availability filters, grouping, ordering, labels, units, rounding and presets | Implemented and WPF-tested; replacement confirms and Custom visibility can be restored |
| Horizontal, vertical, single-line gaming and expanded layouts; theme, font, spacing, opacity | Implemented; bounded scrolling and 70 enabled metrics render without unbounded window size |
| Tray show/hide, settings, pause, presets, click-through, recovery and exit | Implemented and WPF-tested; native hotkey registration detects conflicts; F12 is reserved and rejected |
| Monitor selection, per-monitor saved positions, DPI and off-screen recovery | Implemented; actual mixed-DPI/hotplug/fullscreen physical QA pending |
| Per-category polling, low-impact, pause and auto-pause on battery | Implemented; timeout/cancellation and no overlapping reads tested; battery-equipped system pending |
| History, numeric graphs, sparklines, CSV/JSON and opt-in persistence | Implemented and tested; one metric per history window, multiple windows retain collection dependencies |
| Per-metric sustained alerts, hysteresis, cooldown, quiet hours, snooze, delivery channels | Implemented and tested; overlay/tray and opt-in sound; Windows toast delivery deferred |
| Explicit adapter, automatic adapter and aggregate network modes | Implemented; controlled loopback probes tested; external probes never enabled during QA |
| Named hardware inventory and diagnostics | Implemented; GPU/drive model queries omit serial fields; sensors and counters explain timestamps and quality |
| Actual FPS and anti-cheat/game compatibility | Adapter and synthetic test lifecycle verified; actual ETW/game/permissions/overhead and frame-rate impact unverified |
| Portable and installer | Actual outputs; detailed install/upgrade/uninstall results in v2-validation.md |
| Windows 10, all DPI/refresh combinations, protected fullscreen and screen readers | Unverified on physical devices; local host is Windows 11 build 26300 |
| Blur, automatic updates, simultaneous multi-series plot legends | Deferred optional capabilities |
