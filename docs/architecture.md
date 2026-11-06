# StatsInfo architecture

## Purpose and product boundary

StatsInfo is a native Windows 10/11 x64 desktop utility. It puts user-selected live PC readings in a movable, resizable overlay, keeping useful information visible without switching to a monitoring dashboard. It is designed for everyday desktop work, gaming, and diagnosing resource usage.

The main surface is a transparent, optionally always-on-top WPF window. A separate Settings window controls metrics and behavior; a tray menu and registered hotkeys provide recovery when the overlay is hidden or click-through is enabled. It does not run a web server or use localhost ports. Distribution uses a Windows executable, portable ZIP, and per-user installer.

## Stack and repository structure

| Area | Implementation and reason |
| --- | --- |
| Runtime/UI | C# and .NET 8 WPF for native windows, bindings, transparent rendering, DPI-aware layout and Windows API access |
| View models | CommunityToolkit.Mvvm for observable properties and commands |
| Composition | Microsoft.Extensions.Hosting and dependency injection in `App.xaml.cs` |
| Windows integration | Read-only performance counters, Win32, DXGI, DisplayConfig, WLAN, power and process APIs |
| Optional sensors | Pinned LibreHardwareMonitor library; hardware access requires explicit opt-in |
| Tray | Hardcodet.NotifyIcon.Wpf |
| Persistence | System.Text.Json settings; bounded local history and explicit CSV/JSON export |
| Packaging | Self-contained single-file Windows x64 publish; Inno Setup installer; GitHub Actions release |
| Tests | xUnit, WPF STA dispatcher tests, controlled source fixtures, Coverlet |

`src/StatsInfo/Models` contains the catalog, settings, samples and alert contracts. `Services` contains collection, scheduling, formatting, persistence and validation. `ViewModels` prepares settings and overlay state for binding. `Views` contains Settings, graphs and the responsive panel; `MainWindow` partial files handle appearance, movement, resizing and the tray shell. `Helpers` isolates Windows interop. `tests/StatsInfo.Tests` covers behavior and `installer`/`.github/workflows` describe distribution.

## Startup and data flow

```text
App → dependency-injection host → SettingsService
                              → MetricRuntime → independent source loops
                              → OverlayViewModel → MainWindow
                              → SettingsViewModel → SettingsWindow on demand

MetricRegistry → demand selection → IMetricSource.ReadAsync
                                   ↓
                         typed MetricSample + quality/reason
                                   ↓
             latest snapshot ───────┼──── bounded MetricHistory
                    ↓              └──── AlertEngine → overlay/tray/sound
              overlay and settings
```

Startup reads and normalizes settings, constructs sources, starts background sampling, and shows the overlay. Settings opens on demand and reuses the existing window until it closes. The UI refreshes snapshots on its dispatcher, rather than performing sensor reads on the UI thread.

`MetricRegistry` defines 70 stable metric identifiers, labels, groups, units, defaults and presets. `MetricSample` carries nullable numeric, text or structured values, canonical units, a timestamp, source, quality and an explanation. Missing readings remain N/A/Unknown; they are never fabricated as zero. Valid readings that exceed their expected update age become stale.

The legacy `IMetricCollector`, `MetricSnapshot`, and `SamplingCoordinator` contracts remain for compatibility and regression tests. The expanded overlay uses `MetricRuntime` and `IMetricSource` implementations.

## Sampling and failure isolation

Each source has an independent cancellable loop. Demand comes from visible metrics, enabled alerts, diagnostics, and open history views. Hidden metrics can still be sampled when an alert or history view needs them. Category-specific intervals override the default; low-impact mode uses at least three seconds per category.

Source reads run in background tasks. A five-second timeout produces an explicit error quality and retains the pending task, preventing overlapping reads against the same source. A source failure does not stop other sources. Pausing stops new polling and active frame capture; cancellation and disposal release resources. Network rates use monotonic counter deltas and reject resets and invalid gaps.

Optional hardware choices in Settings use `OptionalDiscovery`: an inaccessible adapter, missing disk performance category, denied access, or unavailable volume leaves fallback choices and an explanatory hint. Optional discovery must not take down Settings. Expected Windows/resource construction failures are reported while the overlay remains running.

## Responsive overlay and interaction

`ResponsiveMetricPanel` measures complete metric tiles and wraps them in saved order. Narrow available widths create more rows; wide widths create fewer rows. Labels, formatted values, spacing, fonts and optional sparklines participate in measurement.

`MainWindow.Resize.cs` owns sizing. The bottom-right `Thumb` tracks pointer movement in screen coordinates, converts it using current window DPI, and passes requested dimensions to the fit calculation. It keeps enough width for the widest tile plus controls, padding and scale. The minimum height is the packed row height for that width. Shrinking width increases the necessary height; shrinking height expands width when needed. A CPU-only selection therefore has a smaller minimum than several stacked readings. Settings and close controls remain available.

The selected monitor's work area limits both dimensions. If the selected metrics cannot fit within that area, the metric region scrolls; controls remain outside it. This prevents an all-metrics configuration from growing off screen. Normal configurations display all selected readings without scrolling.

`OverlayWidth` and `OverlayHeight` are saved in device-independent pixels when resizing finishes. Zero uses the layout's starting size. Horizontal, Vertical, Compact Gaming and Expanded supply initial dimensions; all support automatic wrapping. Choosing another layout clears saved dimensions. Applying unrelated settings preserves dimensions, including a resize made while Settings is open. Resizing and monitor changes re-evaluate content constraints.

Metric-area dragging saves custom placement and per-monitor positions. Anchors clamp the window to the monitor work area. Tray recovery and Ctrl+Shift+F10 restore an accessible position and disable click-through. Ctrl+Shift+F11 toggles visibility. Native hotkey conflicts are reported; click-through requires a registered recovery shortcut.

## Settings and compatibility

The default path is `%APPDATA%\StatsInfo\settings.json`; `--settings <path>` selects an isolated profile. Settings use schema 2. Legacy visibility booleans migrate into catalog preferences without enabling newly introduced metrics. Appearance, monitor placement and unknown root JSON properties are retained. First save keeps `settings.json.pre-v2.bak`. Writes replace a temporary file; corrupt originals are retained with a visible read error.

Visibility, order, labels, units, rounding and sparklines save immediately. Appearance and sampling controls edit a detached draft until Apply. Validation rejects invalid fields before saving. Applying merges the current metric selection and resize dimensions, preventing stale Settings state from reverting them. Presets preserve Custom visibility for restoration and retain unrelated configuration.

The eight Settings sections are Visible Metrics, Overlay & Appearance, Sampling & Performance, Alerts, Network & FPS, History & Export, Hardware & Diagnostics, and General. Unsupported metrics remain selectable and explain availability.

## Metric sources and limitations

| Source | Readings and constraints |
| --- | --- |
| `CpuMemorySource` | Windows CPU counters, per-core usage, logical processor count, physical and committed memory; counters need warm-up samples |
| `GpuEngineSource` | Adapter-specific engine counters, process GPU usage, dedicated VRAM usage, DXGI identity and 64-bit dedicated capacity; shared memory is not substituted for dedicated VRAM |
| `HardwareSensorSource` | CPU/GPU temperatures, powers, clocks, voltages, fans and supported drive sensors; explicit LibreHardwareMonitor opt-in and device-specific support required. CPU sensors also need the separately installed signed PawnIO 2.2+ driver (`HardwareSensorAccess` checks it and reports missing driver, denied access or unavailable device as distinct reasons); a user-selected temperature sensor is labeled with its native name |
| `StorageMetricsSource` | Disk throughput and active time; selected-volume free space; depends on Windows counter availability |
| `NetworkMetricsSource` | Selected/automatic/aggregate adapter deltas, daily traffic and WLAN; aggregate mode may double-count VPN traffic |
| `LatencyMetricsSource` | Opt-in ICMP latency, loss and jitter to the user's chosen endpoint; no default external probe |
| `DisplayMetricsSource` | Configured refresh, mode, resolution, scaling and available HDR; configured Hz is distinct from game FPS |
| `PowerMetricsSource` | Power mode, uptime and battery information where Windows exposes it; desktops may have no battery |
| `ProcessMetricsSource` | Process CPU and working-set rankings; inaccessible/exited processes are skipped safely |
| `PresentMonSource` | Optional process-scoped stdout CSV adapter for a compatible user-supplied PresentMon console; no bundling, injection, automatic download or elevation |

CPU/GPU throttling flags, SMART/NVMe health-warning decoding, current VRR state, game rendering-GPU attribution, and external whole-system power-meter integration remain deferred. Their catalog entries explain N/A/Unknown. Effective clocks, board power, battery health and storage endurance require matching source data rather than inferred substitutes.

PresentMon lifecycle/parsing are checked with a synthetic test-only producer. Actual game/ETW capture, anti-cheat compatibility, exclusive fullscreen, frame-rate impact and capture overhead require real-game validation. Physical sensor accuracy, battery-equipped devices, Windows 10, mixed DPI/hotplug, Explorer restart and full screen-reader behavior require additional device testing. Blur, automatic updates, native Windows toasts and simultaneous multi-series chart legends are not implemented.

## History, alerts and local data

History defaults to 60 seconds and supports up to one hour, with at most 7,200 samples per metric. Invalid/unavailable samples remain graph gaps. Each open history window maintains its sampling dependency. CSV/JSON exports include source/quality metadata and are saved only to a chosen destination.

Persistence is opt-in: daily snapshots are bounded to 20,000 samples/32 MiB, retained for 1–30 days. Alert rules use canonical units with sustained duration, warning/critical thresholds, hysteresis, cooldown, quiet hours and snoozing. Errors never count as valid threshold samples. Delivery supports overlay highlighting, tray balloons and optional sound. Alert logs rotate at 2 MiB and contain metric/threshold/value/time fields.

The app has no telemetry or cloud account. Process names can appear in explicitly enabled process metrics and persisted history. Sensor access may load the library's bundled kernel driver and requires explicit opt-in. Windows access denial is reported; the app does not change Windows permissions or automatically elevate.

## Building, verifying and distributing

Use .NET 8 SDK on Windows. `global.json` selects the 8.0.4xx SDK feature band, including installed patches, so hosted runners use the same SDK as local verification. `tools/run-local.ps1` builds Release and launches Settings, or just the overlay with `-OverlayOnly`. `-ProfilePath` isolates preferences; `-VerifyDirectory` captures running windows, metrics and a short overhead sample. It supports `%LOCALAPPDATA%\dotnet` when PATH points to an older runtime.

```powershell
dotnet test StatsInfo.sln -c Release /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:Threshold=90 /p:ThresholdType=line /p:ThresholdStat=total
dotnet publish src/StatsInfo/StatsInfo.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts/publish/win-x64
ISCC.exe installer/StatsInfo.iss
```

Publish outputs include the executable and third-party notices. The installer retains the per-user application identity and settings, creates shortcuts and provides uninstall support. The stable asset is `StatsInfo-Setup.exe`; a versioned local installer identifies the build. The portable release ZIP includes notices.

The release workflow runs on semantic version tags, tests with a 90% line-coverage gate, publishes the single-file executable, compiles the installer and runs the published executable with `--smoke-test <directory>`. This mode uses its own settings, opens/reopens Settings, checks its embedded icon, verifies wide/stacked/CPU-only sizing, writes screenshots and a JSON receipt, and exits. Failed/timed-out verification blocks publishing. CI retains the evidence as an artifact.

The README installer URL follows the latest published release; a source push alone does not update it. The original v1.0.1 download used older Settings XAML with a relative icon path. The current absolute embedded pack URI is checked in WPF tests and the published application. The packaged smoke check also guards against duplicate Settings windows before WPF completes loading.

Tests cover migration, source algorithms, unavailable samples, timeout isolation, alerts/history, Settings open/reopen, tray recovery, responsive bounds, scale and saved dimensions. Sensor/native startup integrations and generated WPF files are excluded from deterministic coverage. Short local performance observations are not resource-use guarantees; overhead varies with configuration and hardware.

## Metric catalog

The table preserves durable source coverage from the earlier implementation notes. It describes code paths, not universal hardware support or a guarantee that each metric is readable on this PC.


| Metric ID | Implementation | Source |
| --- | --- | --- |
| `cpu.usage.total` | Implemented | Windows Processor counter |
| `cpu.temperature.package` | Implemented; sensor opt-in and physical validation required | LibreHardwareMonitor |
| `cpu.power.package` | Implemented; sensor opt-in and physical validation required | LibreHardwareMonitor |
| `cpu.clock.effective` | Implemented; sensor opt-in and physical validation required | LibreHardwareMonitor |
| `cpu.usage.per_core` | Implemented | Windows Processor counters |
| `cpu.temperature.per_core` | Implemented; sensor opt-in and physical validation required | LibreHardwareMonitor |
| `cpu.voltage` | Implemented; sensor opt-in and physical validation required | LibreHardwareMonitor |
| `cpu.throttling` | Deferred source; selectable N/A/Unknown | LibreHardwareMonitor |
| `cpu.fan.rpm` | Implemented; sensor opt-in and physical validation required | LibreHardwareMonitor |
| `cpu.logical_processors` | Implemented | Environment.ProcessorCount |
| `gpu.usage.total` | Implemented | Windows GPU Engine / LibreHardwareMonitor |
| `gpu.temperature.core` | Implemented; sensor opt-in and physical validation required | LibreHardwareMonitor |
| `gpu.temperature.hotspot` | Implemented; sensor opt-in and physical validation required | LibreHardwareMonitor |
| `gpu.power.board` | Implemented; sensor opt-in and physical validation required | LibreHardwareMonitor |
| `gpu.clock.core` | Implemented; sensor opt-in and physical validation required | LibreHardwareMonitor |
| `gpu.clock.memory` | Implemented; sensor opt-in and physical validation required | LibreHardwareMonitor |
| `gpu.vram.used` | Implemented | Windows GPU Adapter Memory / LibreHardwareMonitor |
| `gpu.vram.total` | Implemented: native DXGI or opt-in sensors | Windows DXGI / LibreHardwareMonitor |
| `gpu.fan.rpm` | Implemented; sensor opt-in and physical validation required | LibreHardwareMonitor |
| `gpu.fan.percent` | Implemented; sensor opt-in and physical validation required | LibreHardwareMonitor |
| `gpu.engines` | Implemented | Windows GPU Engine counters |
| `gpu.throttling` | Deferred source; selectable N/A/Unknown | LibreHardwareMonitor |
| `gpu.active_adapter` | Implemented | Windows GPU counters / LibreHardwareMonitor |
| `memory.physical.used` | Implemented | GlobalMemoryStatusEx |
| `memory.physical.total` | Implemented | GlobalMemoryStatusEx |
| `memory.physical.available` | Implemented | GlobalMemoryStatusEx |
| `memory.commit.used` | Implemented | GetPerformanceInfo |
| `memory.commit.limit` | Implemented | GetPerformanceInfo |
| `memory.pressure` | Implemented | GlobalMemoryStatusEx |
| `storage.temperature` | Implemented; sensor opt-in and physical validation required | LibreHardwareMonitor |
| `storage.read.rate` | Implemented | Windows PhysicalDisk counters |
| `storage.write.rate` | Implemented | Windows PhysicalDisk counters |
| `storage.active_time` | Implemented | Windows PhysicalDisk counters |
| `storage.capacity.free` | Implemented | DriveInfo.AvailableFreeSpace |
| `storage.health.status` | Deferred source; selectable N/A/Unknown | SMART/NVMe telemetry |
| `storage.endurance.used` | Implemented; sensor opt-in and physical validation required | LibreHardwareMonitor |
| `network.download.rate` | Implemented | NetworkInterface byte delta |
| `network.upload.rate` | Implemented | NetworkInterface byte delta |
| `network.ping` | Implemented | System.Net.NetworkInformation.Ping |
| `network.packet_loss` | Implemented | ICMP probe window |
| `network.jitter` | Implemented | ICMP probe window |
| `wifi.signal` | Implemented | Windows WLAN API |
| `wifi.link_speed` | Implemented | Windows WLAN API |
| `wifi.band` | Implemented | Windows WLAN API |
| `wifi.channel` | Implemented | Windows WLAN API |
| `network.adapter` | Implemented | NetworkInterface |
| `network.daily.received` | Implemented | Persisted adapter byte deltas |
| `network.daily.sent` | Implemented | Persisted adapter byte deltas |
| `display.refresh.configured` | Implemented | QueryDisplayConfig |
| `game.fps` | Optional adapter implemented; real game unverified | PresentMon ETW console |
| `game.frame_time` | Optional adapter implemented; real game unverified | PresentMon MsBetweenPresents |
| `game.fps.low1` | Optional adapter implemented; real game unverified | PresentMon frame window |
| `game.fps.low01` | Optional adapter implemented; real game unverified | PresentMon frame window |
| `game.frame_time.graph` | Optional adapter implemented; real game unverified | PresentMon frame window |
| `display.resolution` | Implemented | EnumDisplaySettingsEx |
| `display.hdr` | Implemented | DisplayConfigGetDeviceInfo |
| `display.vrr` | Deferred source; selectable N/A/Unknown | Windows display configuration |
| `display.scaling` | Implemented | GetDpiForMonitor |
| `game.render_gpu` | Deferred source; selectable N/A/Unknown | Process-specific frame attribution |
| `system.uptime` | Implemented | GetTickCount64 |
| `system.power.total` | Deferred source; selectable N/A/Unknown | External power meter |
| `system.motherboard.temperature` | Implemented; sensor opt-in and physical validation required | LibreHardwareMonitor |
| `system.fans` | Implemented; sensor opt-in and physical validation required | LibreHardwareMonitor |
| `battery.percent` | Implemented | GetSystemPowerStatus |
| `battery.discharge.rate` | Implemented | Windows BatteryStatus WMI |
| `battery.health` | Implemented | Windows battery capacity WMI |
| `process.top_cpu` | Implemented | Process.TotalProcessorTime |
| `process.top_ram` | Implemented | Process.WorkingSet64 |
| `process.top_gpu` | Implemented | Windows GPU Engine counters |
| `system.power_mode` | Implemented | PowerGetEffectiveOverlayScheme |
