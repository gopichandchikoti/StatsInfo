using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace StatsInfo.Helpers;

public static class OverlayShell
{
    public static bool RegisterShortcut(IntPtr window, int id, uint modifiers, uint key) =>
        RegisterHotKey(window, id, modifiers | 0x4000, key);
    public static void UnregisterShortcut(IntPtr window, int id) => UnregisterHotKey(window, id);

    public static bool IsFullscreen(IntPtr ownWindow, MonitorInfo monitor)
    {
        var foreground = GetForegroundWindow();
        if (foreground == IntPtr.Zero || foreground == ownWindow) return false;
        var name = new StringBuilder(256);
        GetClassName(foreground, name, name.Capacity);
        if (name.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd") return false;
        if (!GetWindowRect(foreground, out var rect)) return false;
        return Math.Abs(rect.Left - monitor.Left) <= 2 && Math.Abs(rect.Top - monitor.Top) <= 2 &&
            Math.Abs(rect.Right - monitor.Right) <= 2 && Math.Abs(rect.Bottom - monitor.Bottom) <= 2;
    }

    public static void SetStartup(bool enabled, string executable)
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (enabled) key.SetValue("StatsInfo", $"\"{executable}\"");
        else key.DeleteValue("StatsInfo", false);
    }
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr window,
        int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr window, int id);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out DisplayInterop.Rect rect);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr window,
        StringBuilder name, int length);
}
