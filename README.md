# StatsInfo — Windows Performance Overlay

A lightweight, native Windows 10/11 desktop overlay displaying live CPU, GPU, RAM, network throughput, and display refresh rate statistics.

## Features
- **Always-on-top floating HUD:** Translucent, draggable, customizable bar
- **Hardware sensors:** Real-time CPU%, GPU load & temp, physical RAM consumption
- **Network throughput:** Live download/upload bandwidth deltas
- **Display Hz:** Active monitor configured refresh rate detection
- **Settings:** Custom opacity, update rates, and per-metric toggles

## Build & Run
```powershell
dotnet restore
dotnet build -c Release
dotnet run --project src/StatsInfo
```

## Running Tests
```powershell
dotnet test
```
