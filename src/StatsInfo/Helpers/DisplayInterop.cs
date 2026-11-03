using System.Runtime.InteropServices;

namespace StatsInfo.Helpers;

public sealed record MonitorInfo(IntPtr Handle, string Device, int Left, int Top, int Right, int Bottom,
    int WorkLeft, int WorkTop, int WorkRight, int WorkBottom, bool Primary, double DpiScale)
{
    public string Label => $"{Device} · {Right - Left} × {Bottom - Top}{(Primary ? " · primary" : "")}";
}

public static class DisplayInterop
{
    public static MonitorInfo[] Monitors()
    {
        var result = new List<MonitorInfo>();
        MonitorCallback callback = (IntPtr handle, IntPtr dc, ref Rect rect, IntPtr data) =>
        {
            var info = new MonitorNative { Size = Marshal.SizeOf<MonitorNative>(), Device = "" };
            if (!GetMonitorInfo(handle, ref info)) return true;
            uint dpi = 96;
            if (GetDpiForMonitor(handle, 0, out var x, out _) == 0) dpi = x;
            result.Add(new(handle, info.Device, rect.Left, rect.Top, rect.Right, rect.Bottom,
                info.Work.Left, info.Work.Top, info.Work.Right, info.Work.Bottom, (info.Flags & 1) != 0, dpi / 96.0));
            return true;
        };
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
        return result.OrderByDescending(m => m.Primary).ThenBy(m => m.Device).ToArray();
    }

    public static (double? Hz, bool? Hdr) ActiveMode(string device)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            if (GetDisplayConfigBufferSizes(2, out var pathsCount, out var modesCount) != 0) break;
            IntPtr paths = Marshal.AllocHGlobal(checked((int)pathsCount * 72));
            IntPtr modes = Marshal.AllocHGlobal(checked((int)modesCount * 64));
            try
            {
                int error = QueryDisplayConfig(2, ref pathsCount, paths, ref modesCount, modes, IntPtr.Zero);
                if (error == 122) continue;
                if (error != 0) break;
                for (int i = 0; i < pathsCount; i++)
                {
                    var path = IntPtr.Add(paths, i * 72);
                    if (SourceName(path) != device) continue;
                    uint num = unchecked((uint)Marshal.ReadInt32(path, 48));
                    uint den = unchecked((uint)Marshal.ReadInt32(path, 52));
                    return (den > 0 ? (double)num / den : null, HdrEnabled(IntPtr.Add(path, 20)));
                }
            }
            finally { Marshal.FreeHGlobal(paths); Marshal.FreeHGlobal(modes); }
        }
        return (null, null);
    }

    private static string? SourceName(IntPtr source)
    {
        var request = Marshal.AllocHGlobal(84);
        try
        {
            InitializeRequest(request, 84, 1, source);
            return DisplayConfigGetDeviceInfo(request) == 0 ? Marshal.PtrToStringUni(IntPtr.Add(request, 20)) : null;
        }
        finally { Marshal.FreeHGlobal(request); }
    }

    private static bool? HdrEnabled(IntPtr target)
    {
        var request = Marshal.AllocHGlobal(32);
        try
        {
            InitializeRequest(request, 32, 9, target);
            return DisplayConfigGetDeviceInfo(request) == 0 ? (Marshal.ReadInt32(request, 20) & 2) != 0 : null;
        }
        finally { Marshal.FreeHGlobal(request); }
    }

    private static void InitializeRequest(IntPtr request, int size, int type, IntPtr sourceOrTarget)
    {
        Marshal.Copy(new byte[size], 0, request, size);
        Marshal.WriteInt32(request, type);
        Marshal.WriteInt32(request, 4, size);
        for (int i = 0; i < 12; i += 4) Marshal.WriteInt32(request, 8 + i, Marshal.ReadInt32(sourceOrTarget, i));
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorNative
    {
        public int Size;
        public Rect Monitor, Work;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Device;
    }
    private delegate bool MonitorCallback(IntPtr monitor, IntPtr dc, ref Rect rect, IntPtr data);
    [DllImport("user32.dll")]
    private static extern bool EnumDisplayMonitors(IntPtr dc, IntPtr clip,
        MonitorCallback callback, IntPtr data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetMonitorInfoW")]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorNative info);
    [DllImport("shcore.dll")] private static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint x, out uint y);
    [DllImport("user32.dll")] private static extern int GetDisplayConfigBufferSizes(uint flags, out uint paths, out uint modes);
    [DllImport("user32.dll")]
    private static extern int QueryDisplayConfig(uint flags, ref uint pathCount, IntPtr paths,
        ref uint modeCount, IntPtr modes, IntPtr topology);
    [DllImport("user32.dll")] private static extern int DisplayConfigGetDeviceInfo(IntPtr request);
}
