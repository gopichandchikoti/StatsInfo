# ADR 001: Selection of .NET 8 WPF for Desktop Performance Overlay

## Context
StatsInfo requires low-overhead access to native Win32 hardware and performance APIs, transparent floating window rendering, and always-on-top positioning without Electron overhead.

## Decision
We chose C# with .NET 8 and WPF:
1. Native Windows integration with Win32 APIs (kernel32, user32)
2. Low memory footprint (<50MB vs >250MB for Electron)
3. Direct access to LibreHardwareMonitorLib for hardware sensors
4. Modern MVVM architecture via CommunityToolkit.Mvvm

## Consequences
- Requires Windows 10/11 x64 environment
- Fast startup and near-zero CPU idle overhead
