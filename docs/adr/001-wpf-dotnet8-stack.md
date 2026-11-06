# ADR 001: Selection of .NET 8 WPF for Desktop Performance Overlay

## Context
StatsInfo requires low-overhead access to native Win32 hardware and performance APIs, transparent floating window rendering, and always-on-top positioning without Electron overhead.

## Decision
We chose C# with .NET 8 and WPF:
1. Native Windows integration with Win32 APIs (kernel32, user32)
2. A single native desktop UI without a bundled browser runtime
3. Direct access to LibreHardwareMonitorLib for hardware sensors
4. Modern MVVM architecture via CommunityToolkit.Mvvm

## Consequences
- Requires Windows 10/11 x64 environment
- Collection overhead depends on selected metrics, polling and hardware; measure the packaged app rather than assuming a fixed CPU or memory budget
