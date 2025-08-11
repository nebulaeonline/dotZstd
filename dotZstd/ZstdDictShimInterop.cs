using System.Runtime.InteropServices;

namespace nebulae.dotZstd;

[StructLayout(LayoutKind.Sequential)]
internal struct ShimFastCoverParams
{
    public uint K;
    public uint D;
    public uint Steps;
    public uint NbThreads;
    public uint SplitPoint;   // 0..100
    public uint Accel;        // 0 = default
    public uint ShrinkDict;   // 0/1
}

[StructLayout(LayoutKind.Sequential)]
internal struct ShimDictParams
{
    public int CompressionLevel;   // 0 = default
    public uint NotificationLevel;  // 0 = silent
    public uint DictID;             // 0 = auto
}

internal static class ZstdDictShimInterop
{
    private const string SHIM = "zstd_dict_shim";

    [DllImport(SHIM, CallingConvention = CallingConvention.Cdecl)]
    internal static extern nuint ZSTD_DICT_SHIM_trainBasic(
        IntPtr dictBuffer, nuint dictCapacity,
        IntPtr samplesBuffer, IntPtr samplesSizes, uint nbSamples);

    [DllImport(SHIM, CallingConvention = CallingConvention.Cdecl)]
    internal static extern nuint ZSTD_DICT_SHIM_trainFastCover(
        IntPtr dictBuffer, nuint dictCapacity,
        IntPtr samplesBuffer, IntPtr samplesSizes, uint nbSamples,
        ShimFastCoverParams p);

    [DllImport(SHIM, CallingConvention = CallingConvention.Cdecl)]
    internal static extern nuint ZSTD_DICT_SHIM_finalize(
        IntPtr dictBuffer, nuint dictCapacity,
        IntPtr dictContent, nuint dictContentSize,
        IntPtr samplesBuffer, IntPtr samplesSizes, uint nbSamples,
        ShimDictParams p);

    [DllImport(SHIM, CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint ZSTD_DICT_SHIM_isError(nuint code);

    [DllImport(SHIM, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr ZSTD_DICT_SHIM_getErrorName(nuint code);
}
