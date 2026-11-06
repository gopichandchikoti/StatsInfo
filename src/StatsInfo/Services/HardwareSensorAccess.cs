using System.Runtime.InteropServices;
using LibreHardwareMonitor.PawnIo;
using Microsoft.Win32.SafeHandles;
using StatsInfo.Models;

namespace StatsInfo.Services;

public sealed record SensorAccessIssue(string Reason, SampleQuality Quality = SampleQuality.Unavailable);

public static class HardwareSensorAccess
{
    public static SensorAccessIssue? CpuIssue()
    {
        if (!PawnIo.IsInstalled || PawnIo.Version < new Version(2, 2))
            return new("CPU sensors require the signed PawnIO 2.2 or newer driver. Install it from " +
                "https://pawnio.eu, then restart StatsInfo. GPU driver sensors can still work without PawnIO.");
        using var handle = OpenDevice(@"\\?\GLOBALROOT\Device\PawnIO", 0xC0000000, 3,
            IntPtr.Zero, 3, 0, IntPtr.Zero);
        if (!handle.IsInvalid) return null;
        return DeviceIssue(Marshal.GetLastWin32Error());
    }

    public static SensorAccessIssue DeviceIssue(int error) => error == 5
        ? new("Windows denied CPU sensor access. Close StatsInfo and choose Run as administrator. " +
            "GPU driver sensors can still work without elevation.", SampleQuality.PermissionDenied)
        : new($"PawnIO is installed but its device is unavailable (Windows error {error}). " +
            "Restart Windows or repair the signed PawnIO installation, then restart StatsInfo.");

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle OpenDevice(string name, uint access, uint share,
        IntPtr security, uint creation, uint flags, IntPtr template);
}
