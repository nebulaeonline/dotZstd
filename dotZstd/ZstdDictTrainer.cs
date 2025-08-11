using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace nebulae.dotZstd;

public sealed record ZstdFastCoverOptions(
    int DictCapacity,
    uint K = 0, uint D = 0, uint Steps = 0, uint NbThreads = 0,
    uint SplitPoint = 75, uint Accel = 0, bool ShrinkDict = true);

public sealed record ZstdFinalizeOptions(
    int DictCapacity,
    int CompressionLevel = 0,
    uint NotificationLevel = 0,
    uint DictID = 0);

public static partial class ZstdDictTrainer
{
    /// <summary>
    /// Trains a dictionary using zstd's default trainer (no params structs).
    /// </summary>
    public static unsafe byte[] Train(ReadOnlyMemory<byte>[] samples, nuint dictCapacity)
    {
        if (samples is null || samples.Length == 0)
            throw new ArgumentException("At least one sample is required.", nameof(samples));
        if (dictCapacity == 0)
            throw new ArgumentOutOfRangeException(nameof(dictCapacity), "Dictionary capacity must be > 0.");

        FlattenSamples(samples, out byte[] flatBuf, out nuint[] sizes, out _);

        byte[] dict = new byte[checked((int)dictCapacity)];
        GCHandle hFlat = default, hSizes = default;

        try
        {
            hFlat = GCHandle.Alloc(flatBuf, GCHandleType.Pinned);
            hSizes = GCHandle.Alloc(sizes, GCHandleType.Pinned);

            fixed (byte* pDict = dict)
            {
                nuint written = ZstdInterop.ZDICT_trainFromBuffer(
                    (IntPtr)pDict, (nuint)dict.Length,
                    hFlat.AddrOfPinnedObject(), hSizes.AddrOfPinnedObject(), (uint)samples.Length);

                if (ZstdInterop.ZDICT_isError(written) != 0)
                {
                    var err = Marshal.PtrToStringAnsi(ZstdInterop.ZDICT_getErrorName(written)) ?? "Unknown ZDICT error";
                    throw new InvalidOperationException($"ZDICT_trainFromBuffer failed: {err}");
                }

                Array.Resize(ref dict, checked((int)written));
            }
        }
        finally
        {
            if (hSizes.IsAllocated) hSizes.Free();
            if (hFlat.IsAllocated) hFlat.Free();
            Array.Clear(flatBuf, 0, flatBuf.Length); // scrub
        }

        return dict;
    }

    public static unsafe byte[] TrainFastCover(ReadOnlyMemory<byte>[] samples, ZstdFastCoverOptions opts)
    {
        if (samples is null || samples.Length == 0) throw new ArgumentException("Samples required.", nameof(samples));
        if (opts is null) throw new ArgumentNullException(nameof(opts));

        ZstdLibrary.Init(); // ensures resolver is installed

        FlattenSamples(samples, out byte[] flat, out nuint[] sizes, out _);
        var dict = new byte[opts.DictCapacity];

        GCHandle hFlat = default, hSizes = default;
        try
        {
            hFlat = GCHandle.Alloc(flat, GCHandleType.Pinned);
            hSizes = GCHandle.Alloc(sizes, GCHandleType.Pinned);

            fixed (byte* pDict = dict)
            {
                var p = new ShimFastCoverParams
                {
                    K = opts.K,
                    D = opts.D,
                    Steps = opts.Steps,
                    NbThreads = opts.NbThreads,
                    SplitPoint = opts.SplitPoint,
                    Accel = opts.Accel,
                    ShrinkDict = opts.ShrinkDict ? 1u : 0u
                };

                nuint written = ZstdDictShimInterop.ZSTD_DICT_SHIM_trainFastCover(
                    (IntPtr)pDict, (nuint)dict.Length,
                    hFlat.AddrOfPinnedObject(), hSizes.AddrOfPinnedObject(), (uint)samples.Length,
                    p);

                if (ZstdDictShimInterop.ZSTD_DICT_SHIM_isError(written) != 0)
                    throw new InvalidOperationException($"fastCover failed: {Marshal.PtrToStringAnsi(ZstdDictShimInterop.ZSTD_DICT_SHIM_getErrorName(written))}");

                Array.Resize(ref dict, checked((int)written));
            }
        }
        finally
        {
            if (hSizes.IsAllocated) hSizes.Free();
            if (hFlat.IsAllocated) hFlat.Free();
            Array.Clear(flat, 0, flat.Length);
        }

        return dict;
    }

    /// <summary>Utility used by tests to verify dict has a non-zero ID.</summary>
    public static unsafe uint GetDictId(ReadOnlySpan<byte> dictionary)
    {
        if (dictionary.IsEmpty) return 0;
        fixed (byte* p = dictionary)
            return ZstdInterop.ZSTD_getDictID_fromDict((IntPtr)p, (nuint)dictionary.Length);
    }

    public static unsafe byte[] FinalizeDictionary(byte[] seedDict, ReadOnlyMemory<byte>[] samples, ZstdFinalizeOptions opts)
    {
        if (seedDict is null || seedDict.Length == 0) throw new ArgumentException("Seed dict required.", nameof(seedDict));
        if (samples is null || samples.Length == 0) throw new ArgumentException("Samples required.", nameof(samples));
        if (opts is null) throw new ArgumentNullException(nameof(opts));

        ZstdLibrary.Init();

        FlattenSamples(samples, out byte[] flat, out nuint[] sizes, out _);
        var outDict = new byte[opts.DictCapacity];

        GCHandle hFlat = default, hSizes = default, hSeed = default;
        try
        {
            hFlat = GCHandle.Alloc(flat, GCHandleType.Pinned);
            hSizes = GCHandle.Alloc(sizes, GCHandleType.Pinned);
            hSeed = GCHandle.Alloc(seedDict, GCHandleType.Pinned);

            fixed (byte* pOut = outDict)
            {
                var p = new ShimDictParams
                {
                    CompressionLevel = opts.CompressionLevel,
                    NotificationLevel = opts.NotificationLevel,
                    DictID = opts.DictID
                };

                nuint written = ZstdDictShimInterop.ZSTD_DICT_SHIM_finalize(
                    (IntPtr)pOut, (nuint)outDict.Length,
                    hSeed.AddrOfPinnedObject(), (nuint)seedDict.Length,
                    hFlat.AddrOfPinnedObject(), hSizes.AddrOfPinnedObject(), (uint)samples.Length,
                    p);

                if (ZstdDictShimInterop.ZSTD_DICT_SHIM_isError(written) != 0)
                    throw new InvalidOperationException($"finalize failed: {Marshal.PtrToStringAnsi(ZstdDictShimInterop.ZSTD_DICT_SHIM_getErrorName(written))}");

                Array.Resize(ref outDict, checked((int)written));
            }
        }
        finally
        {
            if (hSeed.IsAllocated) hSeed.Free();
            if (hSizes.IsAllocated) hSizes.Free();
            if (hFlat.IsAllocated) hFlat.Free();
            Array.Clear(flat, 0, flat.Length);
        }

        return outDict;
    }

    /// <summary>
    /// Flattens an array of ReadOnlyMemory<byte> into a single contiguous buffer and a parallel sizes array.
    /// This is the form required by ZDICT_trainFromBuffer* APIs.
    /// </summary>
    private static void FlattenSamples(ReadOnlyMemory<byte>[] samples,
                                       out byte[] flatBuf,
                                       out nuint[] sizes,
                                       out int totalSize)
    {
        totalSize = 0;
        sizes = new nuint[samples.Length];

        for (int i = 0; i < samples.Length; i++)
        {
            var len = samples[i].Length;
            if (len <= 0)
                throw new ArgumentException($"Sample[{i}] must not be empty.");
            sizes[i] = (nuint)len;
            totalSize = checked(totalSize + len);
        }

        flatBuf = new byte[totalSize];
        int offset = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i].Span.CopyTo(flatBuf.AsSpan(offset, samples[i].Length));
            offset += samples[i].Length;
        }
    }
}
