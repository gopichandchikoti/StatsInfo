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
SamplingCoordinator (Bounded background loop with cancellation)
  ├── CpuCollector (System.Diagnostics.PerformanceCounter)
  ├── MemoryCollector (GlobalMemoryStatusEx)
  ├── NetworkCollector (NetworkInterface statistics)
  ├── GpuCollector (LibreHardwareMonitorLib)
  └── DisplayModeCollector (EnumDisplaySettings User32)
          │
          ▼
MetricSnapshot (Immutable normalized records)
          │
          ▼
SettingsService (Local JSON configuration)
```

## 3. Resilience and Performance
- **Zero UI-Thread Blocking:** All hardware sampling happens asynchronously in background threads.
- **Fail-Soft Architecture:** If any collector fails (e.g. no dedicated GPU or missing admin rights), that specific metric displays "N/A" without affecting the others.
- **Monotonic Timers:** Network throughput is measured using monotonic high-precision timestamps to calculate delta bytes reliably.
- **Memory Footprint:** Less than 45 MB working set on standard Windows 10/11 installations.
