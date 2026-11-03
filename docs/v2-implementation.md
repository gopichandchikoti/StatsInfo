# StatsInfo 2 implementation record

Audit began 2026-11-02; Windows verification continued 2026-11-03. This enhances the existing repository.
No new application, framework rewrite, commit, branch, push, driver installation or external probe was performed.

## Baseline and decisions

The working tree already contained user changes in the overlay, temperature collectors, settings,
tests, README and CI/release workflows. These were preserved and extended. The existing stack is
.NET 8, WPF, CommunityToolkit.Mvvm, dependency injection, local JSON settings, LibreHardwareMonitor
0.9.3 and Hardcodet.NotifyIcon. All **35 baseline tests passed** using the installed local SDK.
`dotnet` on PATH originally found only the .NET 6 host; the SDK used was
`%LOCALAPPDATA%\dotnet\dotnet.exe` (8.0.427).

Existing readings: CPU usage/package temperature, GPU usage/core temperature, RAM used/total,
combined network throughput, primary-display integer refresh, and always-visible uptime. The seven
existing Visible Metrics controls persisted booleans. Several appearance/monitor/click-through
settings existed in the model without complete runtime wiring. Network timing used wall clock.
The baseline launch remained alive without exposing a usable window during inspection; sensor
initialization was synchronous. The computer-use helper later reported its native pipe unavailable.
There is no claimed baseline screenshot or click-level desktop automation evidence.

The chosen direction retains the compact HUD, assets, native controls and palette, extending Settings
with eight sections. A full-screen hardware dashboard was considered and rejected because it would
interfere with everyday desktop use and games. Native WPF, finite scrolling, existing JSON and local
collection remain appropriate; no web UI or animation framework was introduced.

The new recovery default is `Ctrl+Shift+F10`; show/hide is `Ctrl+Shift+F11`. F12 is rejected because
Windows reserves it for debuggers ([Microsoft RegisterHotKey documentation](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey)). Existing saved strings are retained; invalid/conflicting combinations are explained rather than silently replaced.

## Plan, hypotheses and checks

| Slice | Hypothesis and focused check | Result |
| --- | --- | --- |
| M0 | Existing solution builds/tests before edits | 35 tests passed |
| M1 | Every catalog ID can persist without resetting legacy choices or enabling new metrics | Registry/migration/toggle/preset tests passed; all 70 IDs covered |
| M2 | Sensor mappings distinguish package/core, effective/requested clocks, board/chip and dedicated/shared memory | Mocked sensor contract tests passed; physical sensors remain opt-in and unverified |
| M3 | Counter resets, adapter changes and suspend gaps cannot produce false rates | Monotonic delta/probe-window tests and real local Windows collector checks passed |
| M4 | Absent batteries and protected/exited processes produce explicit limitations | Real process checks and no-battery states verified; battery-equipped hardware pending |
| M5 | Registry-driven controls preserve immediate visibility, draft Apply semantics and bounded overlay size | WPF tests render eight sections and all 70 metrics; presets/search/order/tray recovery tested |
| M6 | Invalid samples do not trigger alerts or become graph values; retention/export stays bounded | History, JSON/CSV, sustained threshold, cooldown, quiet hours and timeout tests passed |
| M7 | Optional process-scoped CSV capture can reject wrong processes and clean up its own session | Controlled synthetic **test-only** executable verifies adapter lifecycle and parsing; actual game/ETW validation pending |
| M8 | Existing Windows packaging can produce a v2 portable app and unsigned installer | See `v2-validation.md` for final commands, artifacts and installation evidence |

A slice with pending real-device acceptance is **partial**, even when its code and unit checks are
implemented. Full acceptance of the supplied specification is not claimed.

## Source and reference review

Checked 2026-11-02/03; references informed compatibility and organization, not copied layouts/assets.

| Primary reference | Observed use and tradeoff | License/accessibility notes |
| --- | --- | --- |
| https://github.com/microsoft/PowerToys | Native utilities with organized feature/navigation documentation; appropriate reference for a utility, not a marketing site | No code/assets reused; license not relied on; native semantic controls retained |
| https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/tree/v0.9.3 | Typed sensor tree, sensor/device names and missing-device behavior; hardware coverage varies | Pinned library MPL-2.0; license included; `Computer.Open()` calls `Ring0.Open()`, so explicit sensor opt-in is required |
| https://github.com/GameTechDev/PresentMon/blob/main/README-ConsoleApplication.md | Process filter, stdout CSV, v1 metric schema, unique sessions, timed shutdown; ETW privileges and game compatibility vary | MIT upstream, not bundled; no injection or auto-elevation; first active swap chain is explicitly identified |
| https://learn.microsoft.com/en-us/windows/win32/api/psapi/nf-psapi-getperformanceinfo | Real system commit accounting, distinct from physical RAM | Documented Windows API |
| https://learn.microsoft.com/en-us/windows/win32/api/wlanapi/nf-wlanapi-wlanqueryinterface | Connected Wi-Fi quality/link/channel; Windows can deny access | Permission denial is displayed; no Windows privacy setting is changed |
| https://learn.microsoft.com/en-us/windows/win32/api/dxgi/ns-dxgi-dxgi_adapter_desc1 | Adapter LUID/name and 64-bit dedicated capacity, excluding shared memory | Native read-only API; linked-node ambiguity remains N/A |

This is an extension of an existing design, not a new UI design-research exercise. No uninspected
web gallery is presented as a verified design reference.

## Architecture and compatibility

`MetricRegistry` is the catalog; `MetricSample` carries nullable numeric/text/structured values,
canonical units, timestamp, source, quality and reason. `MetricRuntime` independently schedules sources,
limits polling, isolates failures/timeouts without overlapping reads, and feeds bounded history and
the alert engine. The legacy collector/snapshot/coordinator interfaces remain available with their
regression tests. The running app uses the expanded sources instead of initializing sensor collectors
on the UI thread.

The original `%APPDATA%\StatsInfo\settings.json` remains authoritative. Schema 1 booleans migrate
to schema 2 preferences, including both upload/download and the RAM composite. Newly introduced
metrics remain hidden. Uptime remains visible because it was already visible before v2. Existing
appearance/monitor/startup/hotkey values and unknown root JSON properties survive. First save keeps
`settings.json.pre-v2.bak`; saves replace a temporary file atomically. Corrupt originals are retained.
Tests use real legacy-format JSON; no existing personal pre-v2 settings file was available to claim
a real-user-file upgrade test.

Visibility, order, labels, units and rounding persist immediately. Other settings use a detached draft
until Apply. Presets confirm replacement, save Custom visibility for restoration, and preserve monitor,
appearance, order and units. Settings never remove toggles for unsupported devices. RAM's legacy
used/total composite declares its Windows memory dependency; percent VRAM requires a valid total.

History defaults to 60 seconds, caps each metric at 7200 entries, and supports 1/5/15/60 minutes.
Persistence is opt-in, capped at 20000 samples/32 MiB per daily snapshot, retained 1–30 days. Exports
include structured values as well as quality/source metadata. Alert logs contain only metric/type,
threshold, numeric value, unit and timestamp, rotate at 2 MiB, and never include process command lines.

## Explicit remaining limitations

- CPU/GPU throttling flags, SMART/NVMe health-warning decoding, current VRR, target game's rendering
  GPU and external whole-system power-meter integration remain deferred sources. Their entries
  are selectable and explain N/A/Unknown; missing data is never reported as healthy or zero.
- Hardware sensor mappings are implemented but not physically validated here; opening the driver
  backend requires the user's explicit opt-in. GPU/drive selection does not silently fall back when
  an explicitly selected device is absent. Effective clock and board power require matching sensors.
- PresentMon is user-supplied and experimental. No actual game FPS, anti-cheat compatibility,
  capture CPU/memory cost or FPS impact has been measured. The synthetic fixture never ships.
- Charts show one selected numeric metric per window. Optional overlay sparklines and the frame-time
  trend are implemented. Simultaneous multi-series legends, Windows toast delivery, blur and automatic updates are deferred.
- Per-monitor saved positions and safe-area recovery are implemented, but mixed DPI/hotplug,
  fullscreen/protected games, Explorer restart and Windows 10 require additional physical QA.
- Native controls have accessible names and keyboard support. Full screen-reader, contrast audit
  and all display-scaling combinations are not certified. No decorative motion is used.
- Configured CI enforces 90% **line** coverage and is retained. Branch coverage remains below the
  universal rulebook's 90% aspiration; see the validation report. Sonar/duplication and full four-metric
  coverage gates were not added or claimed. Catalog data rows and compact XAML have documented
  exceptions to 120-column formatting; long UI scenarios are kept together as readable behavior tests.

Next milestone: validate physical sensors and a user-approved PresentMon/game combination, complete
the deferred source adapters, then expand physical Windows 10/multi-monitor/accessibility QA and
branch coverage before describing this as full specification acceptance.
