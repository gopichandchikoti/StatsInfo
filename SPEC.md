# Stats Info — Windows Desktop Performance Overlay

**Type:** Agent-ready application development specification  
**Companion:** `UNIVERSAL_APPLICATION_AGENT_RULEBOOK.md`  
**Objective:** Build a real, installable or deployable product, not a static prototype.

Create a **native Windows 10/11 desktop utility** with a compact always-on-top, optional transparent overlay showing live hardware and network statistics. The user must be able to install it on their own Windows machine. No backend or cloud account is required. All telemetry remains local unless the user explicitly enables export.

## A. Agent execution contract and universal standards

Read `UNIVERSAL_APPLICATION_AGENT_RULEBOOK.md` alongside this specification.

## B. Platform and technology decision
- **Preferred:** C# + .NET 8 or newer supported LTS + **WPF** (native Windows overlay, window transparency, Win32 interop, packaging, accessible settings). MVVM with CommunityToolkit.Mvvm; use dependency injection for collectors and settings.
- **Hardware sensors:** LibreHardwareMonitor library with capability detection.
- **Refresh rate:** Enumerate displays with Win32 EnumDisplaySettingsEx or QueryDisplayConfig.
- **CPU utilization:** System.Diagnostics performance APIs or platform counters.
- **GPU utilization/temperature:** Hardware monitor sensors where supported, graceful fallback.
- **RAM:** Total/available/used memory using Windows native APIs (GlobalMemoryStatusEx).
- **Network:** Live upload/download throughput in Mbps or MB/s.
- **FPS:** Advanced opt-in frame rate capture adapter interface.

## C. UX and visual specification
- Slim horizontal floating bar anchored top-center of selected monitor, resizable, draggable.
- Theme: dark/light/system, opacity slider, background blur, clear typography.
- Always-on-top toggle, click-through mode with hotkey escape, monitor selector, tray icon.
- Diagnostics page with source, timestamp, permission status.

## D. Architecture
WPF Overlay / Settings / Tray -> SamplingCoordinator -> IMetricCollectors -> Normalized MetricSnapshots
