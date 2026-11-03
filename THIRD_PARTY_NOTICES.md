# Third-party notices

StatsInfo retains its existing .NET 8/WPF dependency stack. This preview is unsigned.

| Dependency | License / upstream source |
| --- | --- |
| LibreHardwareMonitorLib 0.9.3 | Mozilla Public License 2.0; https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/tree/v0.9.3 |
| CommunityToolkit.Mvvm 8.2.2 | MIT; https://github.com/CommunityToolkit/dotnet |
| Microsoft.Extensions and System.Text.Json | MIT; https://github.com/dotnet/runtime |
| Hardcodet.NotifyIcon.Wpf 1.1.0 | Code Project Open License 1.02; see package license metadata and https://github.com/hardcodet/wpf-notifyicon |

LibreHardwareMonitor is distributed as an unmodified library. Its source and third-party notices are
available at the versioned upstream link. The MPL license is included in `licenses/` and in the installer.
Its hardware access can require a kernel driver and permissions; StatsInfo opens it only after explicit
sensor opt-in. No driver is silently installed on first launch.

PresentMon is **not bundled or downloaded**. The optional adapter accepts a user-supplied official 2.x
console executable. Upstream: https://github.com/GameTechDev/PresentMon (MIT, with additional third-party
notices). Verify that binary's provenance and license before use. ETW access and game compatibility vary.

The synthetic PresentMon fixture in `tests/Fixtures/` is solely a test helper and is never packaged.
