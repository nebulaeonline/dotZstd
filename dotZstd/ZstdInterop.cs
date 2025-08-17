using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace nebulae.dotZstd;

[StructLayout(LayoutKind.Sequential)]
public struct ZstdInBuffer
{
    public IntPtr src;      // void*
    public nuint size;      // size_t
    public nuint pos;       // size_t
}

[StructLayout(LayoutKind.Sequential)]
public struct ZstdOutBuffer
{
    public IntPtr dst;      // void*
    public nuint size;      // size_t
    public nuint pos;       // size_t
}

public enum ZSTD_cParameter : int
{
    ZSTD_c_compressionLevel = 100,
    ZSTD_c_checksumFlag = 200,
    ZSTD_c_nbWorkers = 400,
    ZSTD_c_jobSize = 401,
    ZSTD_c_overlapLog = 402,
    ZSTD_c_contentSizeFlag = 500, 
    ZSTD_c_dictIDFlag = 501,
    ZSTD_c_windowLog = 1000,
    ZSTD_c_longDistanceMatching = 1002
}

public enum ZSTD_dParameter : int
{
    ZSTD_d_windowLogMax = 100, 
    ZSTD_d_format = 200,
    ZSTD_d_stableOutBuffer = 400,
    ZSTD_d_refMultipleDDicts = 500
}

public enum ZSTD_ResetDirective : uint 
{ 
    ZSTD_reset_session_only = 1, 
    ZSTD_reset_parameters = 2, 
    ZSTD_reset_session_and_parameters = 3
}

public static class ZstdInterop
{
    static ZstdInterop()
    {
        ZstdLibrary.Init();
    }

    private const string LIB = "libzstd";

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ZSTD_compress")]
    public static extern nuint Compress(
        ref byte dst, nuint dstCapacity,
        ref byte src, nuint srcSize,
        int compressionLevel);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ZSTD_decompress")]
    public static extern nuint Decompress(
        ref byte dst, nuint dstCapacity,
        ref byte src, nuint compressedSize);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ZSTD_compressBound")]
    public static extern nuint GetCompressBound(nuint srcSize);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ZSTD_getFrameContentSize")]
    public static extern ulong GetDecompressedSize(ref byte src, nuint srcSize);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ZSTD_isError")]
    public static extern uint IsError(nuint code);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ZSTD_getErrorName")]
    public static extern IntPtr GetErrorName(nuint code);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr ZSTD_createCStream();

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern nuint ZSTD_initCStream(IntPtr cstream, int compressionLevel);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern nuint ZSTD_compressStream(
        IntPtr cstream, ref ZstdOutBuffer output, ref ZstdInBuffer input);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern nuint ZSTD_flushStream(
        IntPtr cstream, ref ZstdOutBuffer output);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern nuint ZSTD_endStream(
        IntPtr cstream, ref ZstdOutBuffer output);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ZSTD_CStreamInSize")]
    public static extern nuint ZSTD_CStreamInSize();

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ZSTD_CStreamOutSize")]
    public static extern nuint ZSTD_CStreamOutSize();

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern nuint ZSTD_freeCStream(IntPtr cstream);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr ZSTD_createDStream();

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern nuint ZSTD_initDStream(IntPtr dstream);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern nuint ZSTD_decompressStream(
        IntPtr dstream, ref ZstdOutBuffer output, ref ZstdInBuffer input);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ZSTD_DStreamInSize")]
    public static extern nuint ZSTD_DStreamInSize();

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ZSTD_DStreamOutSize")]
    public static extern nuint ZSTD_DStreamOutSize();

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern nuint ZSTD_freeDStream(IntPtr dstream);

    // Compression dictionary
    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr ZSTD_createCDict(
        IntPtr dictBuffer, nuint dictSize, int compressionLevel);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern nuint ZSTD_compress_usingCDict(
        IntPtr ctx,
        IntPtr dst, nuint dstCapacity,
        IntPtr src, nuint srcSize,
        IntPtr cdict);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern void ZSTD_freeCDict(IntPtr cdict);

    // Decompression dictionary
    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr ZSTD_createDDict(
        IntPtr dictBuffer, nuint dictSize);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern nuint ZSTD_decompress_usingDDict(
        IntPtr ctx,
        IntPtr dst, nuint dstCapacity,
        IntPtr src, nuint srcSize,
        IntPtr ddict);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern void ZSTD_freeDDict(IntPtr ddict);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr ZSTD_createCCtx();

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern nuint ZSTD_freeCCtx(IntPtr cctx);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr ZSTD_createDCtx();

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern nuint ZSTD_freeDCtx(IntPtr dctx);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern nuint ZSTD_initCStream_usingCDict(IntPtr cstream, IntPtr cdict);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern nuint ZSTD_initDStream_usingDDict(IntPtr dstream, IntPtr ddict);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ZDICT_trainFromBuffer")]
    public static extern nuint ZDICT_trainFromBuffer(
        IntPtr dictBuffer, nuint dictCapacity,
        IntPtr samplesBuffer, IntPtr samplesSizes, uint nbSamples);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ZDICT_isError")]
    public static extern uint ZDICT_isError(nuint code);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ZDICT_getErrorName")]
    public static extern IntPtr ZDICT_getErrorName(nuint code);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ZSTD_getDictID_fromDict")]
    public static extern uint ZSTD_getDictID_fromDict(IntPtr dict, nuint dictSize);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern uint ZSTD_versionNumber();

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr ZSTD_versionString();

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern nuint ZSTD_findFrameCompressedSize(ref byte src, nuint srcSize);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern nuint ZSTD_CCtx_setParameter(IntPtr cctx, ZSTD_cParameter param, int value);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern nuint ZSTD_DCtx_setParameter(IntPtr dctx, ZSTD_dParameter param, int value);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern uint ZSTD_getDictID_fromFrame(ref byte src, nuint srcSize);
    
    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern uint ZSTD_getDictID_fromDDict(IntPtr ddict);
    
    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern uint ZSTD_getDictID_fromCDict(IntPtr cdict);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern nuint ZSTD_CCtx_loadDictionary(IntPtr cctx, IntPtr dict, nuint dictSize);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern nuint ZSTD_CCtx_loadDictionary_byReference(IntPtr cctx, IntPtr dict, nuint dictSize);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern nuint ZSTD_CCtx_refCDict(IntPtr cctx, IntPtr cdict);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern nuint ZSTD_DCtx_loadDictionary(IntPtr dctx, IntPtr dict, nuint dictSize);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern nuint ZSTD_DCtx_loadDictionary_byReference(IntPtr dctx, IntPtr dict, nuint dictSize);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern nuint ZSTD_DCtx_refDDict(IntPtr dctx, IntPtr ddict);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)] 
    public static extern int ZSTD_minCLevel();
    
    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)] 
    public static extern int ZSTD_maxCLevel();

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)] 
    public static extern int ZSTD_defaultCLevel();

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)]
    public static extern ulong ZSTD_decompressBound(ref byte src, nuint srcSize);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)] 
    public static extern nuint ZSTD_CCtx_reset(IntPtr cctx, ZSTD_ResetDirective d);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)] 
    public static extern nuint ZSTD_DCtx_reset(IntPtr dctx, ZSTD_ResetDirective d);

    [DllImport(LIB, CallingConvention = CallingConvention.Cdecl)] 
    public static extern nuint ZSTD_CCtx_setPledgedSrcSize(IntPtr cctx, ulong pledgedSrcSize);
}
