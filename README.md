# StatsInfo — Windows Performance Overlay

A lightweight, native Windows 10/11 desktop overlay displaying live CPU, GPU, RAM, network throughput, and display refresh rate statistics.

## Version 2 local preview

The existing WPF app now has all **70 specified metrics** in Visible Metrics, with source/quality/N/A
reasons, preserved visibility settings, ordering, units, rounding, presets and optional sparklines.
It adds local Windows collectors, layouts and monitor placement, tray/hotkey recovery, bounded
history/export, sustained alerts, and an experimental user-supplied PresentMon adapter.

Launch the source build locally with Settings open:

```powershell
powershell -ExecutionPolicy Bypass -File tools\run-local.ps1
```

The script uses the local .NET 8 SDK when PATH finds an older .NET host. It does not push or install
anything. To run a separate settings profile, add `-ProfilePath artifacts\qa\review\settings.json`.

Actual build outputs for this preview:

- Portable: `artifacts\publish\win-x64-v2\StatsInfo.exe`
- Unsigned installer: `artifacts\installer\StatsInfo-Setup-2.0.0.exe`

See [implementation and limitations](docs/v2-implementation.md), the
[complete metric matrix](docs/v2-feature-matrix.md), and the [Windows validation report](docs/v2-validation.md).
Some sensor sources remain hardware dependent or deferred; actual game FPS has not been validated.
The existing release download below is unchanged and is not automatically updated by these local builds.

## Download and install

**[Download the latest StatsInfo Windows installer](https://github.com/gopichandchikoti/StatsInfo/releases/latest/download/StatsInfo-Setup.exe)**

Browse all versions on the [GitHub Releases page](https://github.com/gopichandchikoti/StatsInfo/releases).

The installer supports 64-bit Windows 10/11. Download and open `StatsInfo-Setup.exe`, follow the setup wizard, and launch StatsInfo from the Start Menu. A desktop shortcut is optional. To remove the app, use **Settings → Apps → Installed apps** in Windows.

The download becomes available after the first versioned release is published. To publish a release, push a version tag such as `v1.0.0`; GitHub Actions builds the self-contained app, packages it with Inno Setup, and attaches `StatsInfo-Setup.exe` to the release. Future releases use the same download link.

## Features
- **Always-on-top floating HUD:** Translucent, draggable, customizable two-row overlay with Settings and Close controls
- **Hardware sensors:** Real-time CPU load and supported package temperature, GPU load and supported core temperature, physical RAM consumption
- **Network throughput & uptime:** Live download/upload bandwidth deltas and system uptime
- **Display Hz:** Active monitor configured refresh rate detection
- **Application icon:** Custom StatsInfo icon embedded in the executable
- **Settings:** Custom opacity and update rates, with independent CPU/GPU usage and temperature visibility toggles

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

Run the unit and UI regression tests with a 90% minimum line-coverage gate:

```powershell
dotnet test StatsInfo.sln -c Release /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:Threshold=90 /p:ThresholdType=line /p:ThresholdStat=total
```

The Windows CI workflow runs this gate on every push and pull request. Release publishing also requires it to pass. Run the same command before pushing changes. If a regression test fails, fix the behavior rather than weakening or removing the test; change a regression test only when the intended behavior is deliberately changed. Configure the `StatsInfo CI / build-and-test` check as required in GitHub branch protection if failed checks must block merging.

Coverage excludes hardware-dependent sensor collectors, native interop, application startup wiring, and generated WPF build files. These platform integrations are nondeterministic or generated; application behavior and UI regression paths remain covered by unit and WPF tests.
