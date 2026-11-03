# StatsInfo — Windows Performance Overlay

A lightweight, native Windows 10/11 desktop overlay displaying live CPU, GPU, RAM, network throughput, and display refresh rate statistics.

## Download and install

**[Download the latest StatsInfo Windows installer](https://github.com/gopichandchikoti/StatsInfo/releases/latest/download/StatsInfo-Setup.exe)**

Browse all versions on the [GitHub Releases page](https://github.com/gopichandchikoti/StatsInfo/releases).

The installer supports 64-bit Windows 10/11. Download and open `StatsInfo-Setup.exe`, follow the setup wizard, and launch StatsInfo from the Start Menu. A desktop shortcut is optional. To remove the app, use **Settings → Apps → Installed apps** in Windows.

The download becomes available after the first versioned release is published. To publish a release, push a version tag such as `v1.0.0`; GitHub Actions builds the self-contained app, packages it with Inno Setup, and attaches `StatsInfo-Setup.exe` to the release. Future releases use the same download link.

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

## Publish a Windows executable
Create a self-contained, single-file x64 executable that does not require the .NET runtime to be installed:
```powershell
dotnet publish src\StatsInfo\StatsInfo.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts\publish\win-x64
```

Run `artifacts\publish\win-x64\StatsInfo.exe` on a 64-bit Windows 10/11 computer for the portable version.

## Create a Windows installer
Install Inno Setup 6, publish the executable as above, then compile the installer from the repository root:
```powershell
ISCC.exe installer\StatsInfo.iss
```

This creates `artifacts\installer\StatsInfo-Setup-1.0.0.exe`. The installer adds a Start Menu shortcut, offers an optional desktop shortcut, and supports uninstalling StatsInfo from Windows Settings. The GitHub release workflow creates the stable `StatsInfo-Setup.exe` download asset from this versioned installer.

## Publish a GitHub release

Push a new semantic version tag to the GitHub repository to build and publish an installer:
```powershell
git tag v1.0.0
git push origin v1.0.0
```

The release workflow derives the application and installer versions from the tag. Ensure the tag has not already been used; use the next version for each release.

## Running Tests
```powershell
dotnet test
```
