using System.Runtime.InteropServices;

namespace StatsInfo.Helpers;

public sealed record DxgiAdapterInfo(string Luid, string Name, ulong DedicatedBytes)
{
    public bool Matches(string token) => token.StartsWith(Luid + "_phys_", StringComparison.OrdinalIgnoreCase);
}

public static class DxgiAdapters
{
    public static DxgiAdapterInfo[] Read()
    {
        IntPtr factory = IntPtr.Zero;
        try
        {
            var iid = new Guid("770aae78-f26f-4dba-a829-253c83d1b387");
            if (CreateDXGIFactory1(ref iid, out factory) < 0) return [];
            var enumerate = Method<EnumAdapter>(factory, 12);
            var result = new List<DxgiAdapterInfo>();
            for (uint index = 0; index < 32; index++)
            {
                if (enumerate(factory, index, out var adapter) < 0) break;
                try
                {
                    if (Method<GetDescription>(adapter, 10)(adapter, out var description) < 0) continue;
                    if ((description.Flags & 2) != 0) continue; // Software rasterizer has no physical dedicated VRAM.
                    string luid = $"luid_0x{unchecked((uint)description.High):X8}_0x{description.Low:X8}";
                    result.Add(new(luid, description.Description.TrimEnd('\0'), description.DedicatedVideoMemory.ToUInt64()));
                }
                finally { Marshal.Release(adapter); }
            }
            return result.ToArray();
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or COMException)
        { return []; }
        finally { if (factory != IntPtr.Zero) Marshal.Release(factory); }
    }

    private static T Method<T>(IntPtr instance, int slot) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), slot * IntPtr.Size));

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct AdapterDescription
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
        public uint VendorId, DeviceId, SubSystemId, Revision;
        public UIntPtr DedicatedVideoMemory, DedicatedSystemMemory, SharedSystemMemory;
        public uint Low;
        public int High;
        public uint Flags;
    }
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int EnumAdapter(IntPtr factory, uint index, out IntPtr adapter);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int GetDescription(IntPtr adapter, out AdapterDescription description);
    [DllImport("dxgi.dll", ExactSpelling = true)]
    private static extern int CreateDXGIFactory1(ref Guid iid, out IntPtr factory);
}
