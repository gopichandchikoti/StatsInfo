# Feature log

## [2026-11-03] Native VRAM inventory and final verification fixes

**Type**: Fix
**Author**: agent

### What changed

Added read-only DXGI adapter names and 64-bit dedicated-capacity mapping, with linked-node ambiguity
kept unavailable. Narrowed collector failures to requested metrics, retained warm GPU counters while
pruning exited instances, preserved frame-event timestamps and rolling frame spikes, rejected reserved
F12 shortcuts, and exposed numeric-input/save/history failures. Corrupt persisted history entries are
ignored and locked history files report an error instead of crashing clear-history.

### Why

Complete core VRAM capability without driver opt-in and resolve issues found during live Windows QA.

### Files touched

- `Helpers/DxgiAdapters.cs`, GPU/sampling/history/frame/settings services and related WPF views
- Collector, settings, frame, runtime and UI tests; installer and validation documentation

### Validation

80 tests passed; 91.08% line coverage, 76.27% branch coverage, 91.82% method coverage. The retained
90% line gate, full formatting verification and warning-as-error build passed. Final live all-metrics
capture: 35 valid readings, 35 unavailable states, zero errors. Isolated v1 install, v2 upgrade, matching
payload hash, uninstall and owned startup/registry/executable cleanup passed. Physical sensors and live
game FPS remain unverified. Detailed evidence is in `v2-validation.md` and `artifacts/qa`.

### Migration / Setup notes

Schema 2 uses the existing JSON. Recovery defaults to Ctrl+Shift+F10; saved strings remain intact.
No commit, tag, push, telemetry upload, external latency probe or privileged sensor initialization.

## [2026-11-03] StatsInfo v2 local preview

**Type**: Feature
**Author**: agent

### What changed

Extended the existing WPF app with a 70-metric catalog, typed quality-aware samples, compatible
settings migration, independent local collectors, expanded Visible Metrics, presets/ordering/units,
layouts/themes/placement/tray recovery, bounded history/export, alerts and an optional PresentMon adapter.
Added Windows behavior/integration tests, local rendered verification, v2 packaging and license notices.

### Why

Implement the supplied v2 specification while preserving existing application behavior and user edits.

### Files touched

- `src/StatsInfo/Models`, `Services`, `Helpers`, `ViewModels`, `Views`, overlay/startup/project files
- `tests/StatsInfo.Tests` and the explicitly synthetic `tests/Fixtures/PresentMonFixture.cs`
- `installer/StatsInfo.iss`, `README.md`, `docs`, `licenses`, `THIRD_PARTY_NOTICES.md`

### Validation

Baseline: 35 tests passed. New Windows collectors, migration, all 70 toggles, WPF sections, bounded
history, alert lifecycle, timeout isolation and test-only FPS capture parsing were exercised. The
configured 90% line gate passed. Final commands/results and runtime measurements are recorded in
`v2-validation.md`; hardware/game limitations remain explicit in `v2-implementation.md`.

### Migration / Setup notes

Uses existing JSON with schema 2 fields and `.pre-v2.bak` on first save. No reset or push.
Use the local .NET 8 SDK if PATH still resolves .NET 6. Sensors/probes/history persistence remain opt-in.
