# Architecture Document — StatsInfo

## 1. Overview
StatsInfo is a native Windows 10/11 desktop overlay showing live CPU, GPU, RAM, network throughput, and display refresh rate statistics in a compact floating bar.

## 2. Component Diagram
```
WPF UI Layer
  ├── MainWindow (Overlay)
  ├── SettingsWindow
  └── System Tray (NotifyIcon)
          │ MVVM data binding
          ▼
ViewModels (CommunityToolkit.Mvvm)
  ├── OverlayViewModel
  └── SettingsViewModel
          │
          ▼
MetricRegistry (70 stable IDs) + SettingsService (compatible schema 2 JSON)
          │
          ▼
MetricRuntime (independent, demand-driven, cancellable source loops)
  ├── CPU / memory / GPU-engine counters / disk / process statistics
  ├── Adapter byte deltas / WLAN / explicitly opted-in ICMP probes
  ├── DisplayConfig / power and battery APIs
  ├── Explicitly opted-in LibreHardwareMonitor sensors
  └── Optional process-scoped PresentMon stdout capture
          │
          ▼
MetricSample (nullable numeric/text/structured values, units, timestamp, source, quality)
  ├── Overlay / settings / hardware diagnostics
  ├── Bounded MetricHistory / graphs / explicit CSV and JSON export
  └── AlertEngine / local notifications and privacy-safe logs

Legacy IMetricCollector, MetricSnapshot and SamplingCoordinator remain for compatibility and regression tests.
```

## 3. Resilience and Performance
- **Zero UI-Thread Blocking:** All hardware sampling happens asynchronously in background threads.
- **Fail-Soft Architecture:** If any collector fails (e.g. no dedicated GPU or missing admin rights), that specific metric displays "N/A" without affecting the others.
- **Monotonic Timers:** Network throughput is measured using monotonic high-precision timestamps to calculate delta bytes reliably.
- **Measured overhead:** See `v2-validation.md` for actual process CPU and working-set measurements.
  No fixed memory or CPU guarantee is asserted. Sensor/game overhead requires separate device validation.
