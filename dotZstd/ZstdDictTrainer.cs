using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace nebulae.dotZstd;

/// <summary>
/// Represents configuration options for creating a dictionary using the Zstandard Fast Cover algorithm.
/// </summary>
/// <remarks>The Zstandard Fast Cover algorithm is used to generate dictionaries for compression, optimizing for
/// speed and efficiency. This record encapsulates parameters that control the behavior of the dictionary generation
/// process, such as capacity, sampling rates, and threading options.</remarks>
/// <param name="DictCapacity">The maximum capacity of the dictionary, in bytes. This value determines the size of the generated dictionary.</param>
/// <param name="K">The segment size used for training, in bytes. A value of 0 indicates that the default segment size will be used.</param>
/// <param name="D">The number of dimensions used for clustering during dictionary training. A value of 0 indicates that the default
/// number of dimensions will be used.</param>
/// <param name="Steps">The number of optimization steps to perform during dictionary training. A value of 0 indicates that the default
/// number of steps will be used.</param>
/// <param name="NbThreads">The number of threads to use for dictionary training. A value of 0 indicates that the training will be performed on
/// a single thread.</param>
/// <param name="SplitPoint">The percentage of samples to use for training versus testing, expressed as an integer between 0 and 100. The default
/// value is 75.</param>
/// <param name="Accel">The acceleration factor for training. Higher values result in faster training but may reduce dictionary quality. A
/// value of 0 indicates the default acceleration factor.</param>
/// <param name="ShrinkDict">A boolean value indicating whether the dictionary should be shrunk to remove unused capacity. <see langword="true"/>
/// to shrink the dictionary; otherwise, <see langword="false"/>.</param>
public sealed record ZstdFastCoverOptions(
    int DictCapacity,
    uint K = 0, uint D = 0, uint Steps = 0, uint NbThreads = 0,
    uint SplitPoint = 75, uint Accel = 0, bool ShrinkDict = true);

/// <summary>
/// Represents options for finalizing a Zstandard dictionary during compression or decompression.
/// </summary>
/// <remarks>This type encapsulates configuration settings for the Zstandard dictionary finalization process,
/// including capacity, compression level, notification level, and dictionary ID. These options influence the behavior
/// of dictionary-based compression or decompression operations.</remarks>
/// <param name="DictCapacity">The maximum capacity of the dictionary, in bytes. Must be a positive integer.</param>
/// <param name="CompressionLevel">The compression level to apply when finalizing the dictionary. A value of 0 indicates the default compression level.
/// Higher values may result in better compression at the cost of performance.</param>
/// <param name="NotificationLevel">The level of notifications or logging during the finalization process. A value of 0 disables notifications.</param>
/// <param name="DictID">The unique identifier for the dictionary. A value of 0 indicates that no specific dictionary ID is assigned.</param>
public sealed record ZstdFinalizeOptions(
    int DictCapacity,
    int CompressionLevel = 0,
    uint NotificationLevel = 0,
    uint DictID = 0);

/// <summary>
/// Provides methods for training and finalizing dictionaries using zstd compression algorithms.
/// </summary>
/// <remarks>This class includes functionality for training dictionaries with sample data, using zstd's default
/// trainer or FastCover algorithm, as well as finalizing dictionaries with additional options. It also provides utility
/// methods for verifying dictionary IDs.</remarks>
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

    /// <summary>
    /// Trains a dictionary using the FastCover algorithm based on the provided samples and options.
    /// </summary>
    /// <remarks>The FastCover algorithm is used to generate a dictionary optimized for compression based on
    /// the provided training samples. This method requires valid training samples and options to execute successfully.
    /// The size of the resulting dictionary depends on the training process and may be adjusted based on the
    /// algorithm's output.</remarks>
    /// <param name="samples">An array of byte buffers representing the training samples. Must not be null or empty.</param>
    /// <param name="opts">The options for configuring the FastCover training process. Must not be null.</param>
    /// <returns>A byte array containing the trained dictionary. The size of the array may be smaller than the specified
    /// dictionary capacity in <paramref name="opts"/>.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="samples"/> is null or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="opts"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the FastCover training process fails due to an internal error.</exception>
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

    /// <summary>
    /// Retrieves the unique identifier of a Zstandard dictionary.
    /// </summary>
    /// <remarks>This method uses the Zstandard library to extract the dictionary ID. The dictionary ID can be
    /// used to verify compatibility between compressed data and the dictionary.</remarks>
    /// <param name="dictionary">A read-only span of bytes representing the dictionary. Must not be empty.</param>
    /// <returns>The unique identifier of the dictionary as an unsigned integer.  Returns <see langword="0"/> if the dictionary
    /// is empty.</returns>
    public static unsafe uint GetDictId(ReadOnlySpan<byte> dictionary)
    {
        if (dictionary.IsEmpty) return 0;
        fixed (byte* p = dictionary)
            return ZstdInterop.ZSTD_getDictID_fromDict((IntPtr)p, (nuint)dictionary.Length);
    }

    /// <summary>
    /// Finalizes a Zstandard dictionary using the provided seed dictionary, training samples, and options.
    /// </summary>
    /// <remarks>This method uses the Zstandard library to finalize a dictionary based on the provided seed
    /// dictionary and training samples. The finalized dictionary can be used for compression and decompression
    /// operations.</remarks>
    /// <param name="seedDict">The seed dictionary used as the basis for finalizing the dictionary. Must not be <see langword="null"/> or
    /// empty.</param>
    /// <param name="samples">An array of training samples used to optimize the dictionary. Must not be <see langword="null"/> or empty.</param>
    /// <param name="opts">Options specifying the parameters for dictionary finalization, such as compression level and dictionary
    /// capacity. Must not be <see langword="null"/>.</param>
    /// <returns>A byte array containing the finalized dictionary. The size of the array may be smaller than the specified
    /// dictionary capacity.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="seedDict"/> is <see langword="null"/> or empty, or if <paramref name="samples"/> is
    /// <see langword="null"/> or empty.</exception>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="opts"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the dictionary finalization process fails due to an error in the Zstandard library.</exception>
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
    /// Flattens an array of byte buffers into a single contiguous buffer and calculates metadata about the buffers.
    /// </summary>
    /// <remarks>This method combines multiple byte buffers into a single flat buffer, calculates the total
    /// size of all buffers, and generates an array of sizes representing the length of each individual buffer. The
    /// resulting flat buffer contains the contents of all input buffers concatenated in order.</remarks>
    /// <param name="samples">An array of <see cref="ReadOnlyMemory{T}"/> objects containing the byte buffers to flatten. Each buffer must be
    /// non-empty.</param>
    /// <param name="flatBuf">When the method returns, contains the flattened byte buffer that concatenates all input buffers.</param>
    /// <param name="sizes">When the method returns, contains an array of <see cref="nuint"/> values representing the size of each input
    /// buffer.</param>
    /// <param name="totalSize">When the method returns, contains the total size of all input buffers combined.</param>
    /// <exception cref="ArgumentException">Thrown if any buffer in <paramref name="samples"/> is empty.</exception>
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
