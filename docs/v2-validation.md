# StatsInfo 2 Windows validation

Date: 2026-11-03. This is a **local preview**, with partial hardware/game acceptance, not a claim that
every supplied specification acceptance case has been completed. Nothing was committed, tagged or pushed.

## Environment and changes

- Windows 11 Home, version 10.0.26300; AMD Ryzen 7 7840HS / Radeon 780M, 8 cores / 16 logical processors.
- Active display: 3440 × 1440, configured 180 Hz, 100% DPI. This is not a measured game frame rate.
- Usable RAM: approximately 31.2 GiB. No battery was exposed by Windows on this device.
- .NET SDK 8.0.427 at `%LOCALAPPDATA%\dotnet`; Inno Setup 6.7.3.
- Existing WPF/MVVM/JSON stack and user modifications were retained. The original v1 portable and
  installer remain alongside v2 outputs. [Implementation record](v2-implementation.md) explains changes;
  the [70-metric matrix](v2-feature-matrix.md) distinguishes implemented, sensor-dependent and deferred sources.

## Automated and static verification

Baseline: **35 tests passed** before edits. Final: **80 tests passed**, none skipped.

| Check | Actual result |
| --- | --- |
| Release build with `-warnaserror` | Passed; 0 warnings, 0 errors |
| `dotnet format StatsInfo.sln --verify-no-changes --no-restore` | Passed |
| Full test suite and retained 90% line gate | Passed; 91.08% lines, 76.27% branches, 91.82% methods |
| NuGet audit, including transitive dependencies | No vulnerable packages reported after pinning System.Text.Json 8.0.6 |
| PowerShell local-run helper parser | Passed |
| `git diff --check` | No whitespace errors; Git reported normal LF/CRLF conversion notices |

The configured CI gate measures **lines**. The universal rulebook's 90% branch aspiration is **not met**;
statement coverage is not separately reported by this configuration. Coverage exclusions from the
existing project were retained; new source/registry/UI/runtime code was not added to exclusions.
SonarQube, duplication/complexity tooling and a complete accessibility audit were not performed.

Tests cover legacy/empty/corrupt settings, unknown root settings, all 70 toggle round trips, order,
unit/rounding/label preferences, presets and Custom restoration, immediate toggles versus detached
settings drafts, invalid numeric input and visible write failures, real Windows CPU/RAM/display/disk/
process reads, GPU engine aggregation, DXGI adapter matching, sensor semantic mappings, local loopback
ICMP opt-in, counter resets/adapter switches/resume gaps, bounded history, gap quality, JSON/CSV metadata,
locked/corrupt history files, sustained alerts/cooldown/hysteresis/quiet hours, collector timeouts without
overlapping reads, cancellation and disposal. WPF tests render all sections and exercise tray recovery,
show/hide, pause, layouts, 70 enabled metrics, settings reopen and history windows.

PresentMon lifecycle/parsing tests compile a clearly marked synthetic CSV producer under a temporary
test directory. It verifies PID and swap-chain filtering, timestamps, actual interval math, sample
minimums, stop/pause/end cleanup and unique-session arguments. **It is not an actual ETW/game FPS test.**
The fixture is never published with the app.

Commands actually used (the local SDK path replaces PATH's old .NET 6 host):

```powershell
$sdk = "$env:LOCALAPPDATA\dotnet\dotnet.exe"
& $sdk test StatsInfo.sln -c Release /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:Threshold=90 /p:ThresholdType=line /p:ThresholdStat=total
& $sdk format StatsInfo.sln --verify-no-changes --no-restore
& $sdk build StatsInfo.sln -c Release --no-restore -warnaserror
& $sdk list src/StatsInfo/StatsInfo.csproj package --vulnerable --include-transitive
& $sdk publish src/StatsInfo/StatsInfo.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts/publish/win-x64-v2
```

## Live readings and rendered UI

The final all-metrics capture recorded **35 valid readings, 35 explicit unavailable states, zero
collector errors**. Sensors, external probes and frame capture remained off. DXGI returned the monitored
GPU name and 64-bit dedicated capacity without opening LibreHardwareMonitor. Capacity is explicitly
driver-reported, not shared GPU memory or a truncated WMI `AdapterRAM` value.

Artifacts, all local and ignored by Git:

- `artifacts/qa/final-dxgi/live-metrics.json`: all 70 values/statuses, units, timestamps, sources and reasons.
- `artifacts/qa/final-dxgi/overlay.png`: all metrics enabled, with bounded scrolling.
- `artifacts/qa/final-dxgi/settings.png` and `settings-00.png` through `settings-07.png`: native WPF sections.
- `artifacts/qa/upgrade-final`: installed app capture using a legacy settings fixture.
- `artifacts/qa/install-results.json`: installation/upgrade/uninstall checks and payload hashes.

These are renders from running WPF windows, not a static mockup. Process/window titles were checked.
The native computer-use helper could not connect to its pipe, so no click-level desktop automation or
recording is claimed. Real Windows source calls and WPF interaction tests supply the runtime evidence.

No independent Task Manager/Windows Display Settings/hardware-monitor comparison with recorded
tolerances was performed. Controlled external traffic, disk-load accuracy comparison, sleep/wake,
mixed monitors, fractional 59.94 Hz and all DPI/refresh combinations remain unverified physically.

## Measured overhead

CPU is normalized to all 16 logical processors; memory is working set, not managed heap or private bytes.
Each sample lasted approximately 15.2 seconds, under the machine's existing desktop workload. These are
short observations, not performance guarantees or independent idle-machine benchmarks.

| Mode | Machine CPU | Average / peak working set | Evidence |
| --- | --- | --- | --- |
| Overlay only, default visible metrics, Settings closed | 0.86% | 184.7 / 187.2 MiB | `artifacts/qa/final-idle-dxgi/overhead.json` |
| All 70 enabled, Settings open, sensors/probes/capture off | 1.47% | 225.4 / 229.8 MiB | `artifacts/qa/final-dxgi/overhead.json` |
| Installed legacy layout, Settings open | 1.18% | 220.4 / 226.6 MiB | `artifacts/qa/upgrade-profile/overhead.json` |

The overlay-only and all-metrics rows include the final DXGI source. Collector milliseconds are
read wall time, not CPU cost; shared categories can show the most recent
source in that category rather than a sum. FPS capture CPU/memory cost and game frame-rate impact were
**not measured**. No capture or hardware benchmark values have been invented.

## Packaging, installation and cleanup

Actual unsigned outputs:

- `artifacts/publish/win-x64-v2/StatsInfo.exe` — self-contained Windows x64 portable app, version 2.0.0.0.
- `artifacts/installer/StatsInfo-Setup-2.0.0.exe` — unsigned per-user installer. No signing certificate was supplied.
- License notices are included alongside the portable build and in the installer.

Installer compilation used the installed Inno compiler with an explicit v2 payload directory:

```powershell
& "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe" "/DPublishDirectory=$PWD\artifacts\publish\win-x64-v2" installer\StatsInfo.iss
```

Clean install/upgrade/uninstall used **separate QA AppName/AppId overrides** with the same installer
script, never the real StatsInfo identity. A QA v1 wrapper packaged the preserved v1 portable, then the
QA v2 installer upgraded it. Results: v1 install exit 0; v2 upgrade exit 0; installed binary version
2.0.0.0 and SHA-256 matched the v2 portable; uninstall exit 0; QA executable, uninstall registry entry
and the owned QA startup value were removed. The real installation, startup value and shortcuts were
not replaced. The original production v1 installer was not executed.

The hand-authored legacy-format settings fixture retained its exact SHA-256 through launch and
uninstall. Its capture preserves CPU/network hidden, GPU/RAM/Hz/uptime visible, opacity, scale and
custom placement. No real personal pre-v2 settings file was available; compatibility tests and this
fixture are the upgrade evidence. First actual save preserves `.pre-v2.bak`.

The generic process close API did not reach the tray-style overlay after Settings closed, so QA
process termination used an exact owned PID. Native external graceful Exit remains **unverified**;
the actual Close/Exit handlers and cancellation/disposal paths passed WPF/unit tests. One PowerShell
missing-registry-value query errored during checking; direct Registry API verification confirmed that
cleanup succeeded. The raw result records that distinction.

For a user install: exit the current overlay using its × or tray Exit, run the v2 installer and choose
the per-user location. Existing production AppId and `%APPDATA%\StatsInfo\settings.json` are preserved.
For portable use, launch the executable directly. Uninstall via Windows Settings → Apps; preferences
remain for reinstall. Startup cleanup removes this app's Run entry only when it points inside the
uninstalled application directory. No elevation is requested by the installer.

## Remaining acceptance gaps

CPU/GPU reliable throttle flags, SMART/NVMe health-warning decoding, current VRR, game rendering-GPU
attribution and external whole-system power-meter sources remain deferred and explicitly N/A/Unknown.
Physical temperatures, powers, clocks, fans and endurance require sensor opt-in and device validation.
Battery health/rate could not be tested on a battery-equipped device. PresentMon is user-supplied and
unverified with a live game, permissions, anti-cheat or exclusive fullscreen. Windows 10, mixed DPI/
hotplug, Explorer restart, full screen-reader operation and Windows toast delivery remain unverified
or deferred. See the matrix and implementation record for exact scope; do not label full-spec QA complete.
