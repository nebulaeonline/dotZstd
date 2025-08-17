using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace nebulae.dotZstd;

/// <summary>
/// Provides static methods for interacting with the Zstandard compression library.
/// </summary>
/// <remarks>The <see cref="Zstd"/> class offers a variety of methods for compression, decompression, and buffer
/// size recommendations using the Zstandard algorithm. It also includes utilities for working with Zstandard
/// dictionaries and retrieving version information. This class is designed to simplify integration with the Zstandard
/// library and ensure optimal performance for compression and decompression operations.</remarks>
public static class Zstd
{
    /// <summary>
    /// Retrieves the version string of the Zstandard library.
    /// </summary>
    /// <remarks>This method provides the version information of the underlying Zstandard library being used.
    /// The returned string is useful for logging, diagnostics, or ensuring compatibility with specific library
    /// versions.</remarks>
    /// <returns>A string representing the version of the Zstandard library in the format "major.minor.patch".</returns>
    public static string VersionString() => Marshal.PtrToStringAnsi(ZstdInterop.ZSTD_versionString())!;

    /// <summary>
    /// Retrieves the version number of the Zstandard library.
    /// </summary>
    /// <remarks>This method provides the version of the underlying Zstandard library being used. It can be
    /// useful for ensuring compatibility or debugging purposes when working with Zstandard compression and
    /// decompression operations.</remarks>
    /// <returns>An unsigned integer representing the version number of the Zstandard library. The version number is encoded as a
    /// single integer, where the major, minor, and patch versions are combined (e.g., version 1.5.2 would be
    /// represented as 10502).</returns>
    public static uint VersionNumber() => ZstdInterop.ZSTD_versionNumber();

    /// <summary>
    /// Retrieves the minimum compression level supported by the Zstandard library.
    /// </summary>
    /// <remarks>The compression level determines the trade-off between compression ratio and speed. Lower
    /// levels typically result in faster compression but less effective compression.</remarks>
    /// <returns>The minimum compression level as an integer. This value represents the lowest level of compression that can be
    /// applied using the Zstandard algorithm.</returns>
    public static int MinLevel() => ZstdInterop.ZSTD_minCLevel();
    
    /// <summary>
    /// Retrieves the maximum compression level supported by the Zstandard library.
    /// </summary>
    /// <remarks>This method is a wrapper around the Zstandard library's native functionality. The returned
    /// value can be used to configure compression settings for optimal results based on application
    /// requirements.</remarks>
    /// <returns>The highest compression level available for Zstandard compression. Higher values typically result in better
    /// compression ratios but may require more computational resources.</returns>
    public static int MaxLevel() => ZstdInterop.ZSTD_maxCLevel();
    
    /// <summary>
    /// Retrieves the default compression level used by the Zstandard library.
    /// </summary>
    /// <returns>The default compression level as an integer. This value is determined by the Zstandard library and may vary
    /// depending on the version or configuration.</returns>
    public static int DefaultLevel() => ZstdInterop.ZSTD_defaultCLevel();

    /// <summary>
    /// Retrieves the recommended input buffer size for compression streams.
    /// </summary>
    /// <remarks>Use this method to allocate appropriately sized buffers for compression operations.
    /// Allocating buffers of this size helps avoid performance degradation due to suboptimal buffer sizes.</remarks>
    /// <returns>The recommended size, in bytes, for the input buffer used in compression streams. This value is determined by
    /// the underlying compression library and ensures optimal performance.</returns>
    public static int RecommendedCStreamInSize() => checked((int)ZstdInterop.ZSTD_CStreamInSize());
    
    /// <summary>
    /// Retrieves the recommended output buffer size for streaming compression operations.
    /// </summary>
    /// <remarks>Use this method to allocate an appropriately sized buffer for streaming compression. The
    /// returned size is designed to minimize memory usage while maintaining efficient compression.</remarks>
    /// <returns>The recommended size, in bytes, for the output buffer used during streaming compression. This value is
    /// determined by the underlying compression library and ensures optimal performance.</returns>
    public static int RecommendedCStreamOutSize() => checked((int)ZstdInterop.ZSTD_CStreamOutSize());
    
    /// <summary>
    /// Retrieves the recommended input buffer size for a decompression stream.
    /// </summary>
    /// <remarks>This method provides the optimal buffer size for efficient decompression operations. Using a
    /// buffer of this size can improve performance and reduce memory overhead.</remarks>
    /// <returns>The recommended size, in bytes, for the input buffer used in decompression streams. This value is determined by
    /// the underlying Zstandard library.</returns>
    public static int RecommendedDStreamInSize() => checked((int)ZstdInterop.ZSTD_DStreamInSize());

    /// <summary>
    /// Gets the recommended output buffer size for a decompression stream.
    /// </summary>
    /// <remarks>Use this method to allocate an appropriately sized buffer for efficient decompression when
    /// working with Zstandard streams. The recommended size ensures optimal performance and compatibility with the
    /// Zstandard decompression API.</remarks>
    /// <returns>The recommended size, in bytes, for the output buffer used in decompression streams. This value is determined by
    /// the underlying Zstandard library.</returns>
    public static int RecommendedDStreamOutSize() => checked((int)ZstdInterop.ZSTD_DStreamOutSize());

    /// <summary>
    /// Returns the dictionary ID embedded in a Zstandard dictionary blob (0 if none).
    /// </summary>
    /// <param name="dictionary">Raw dictionary bytes.</param>
    public static unsafe uint GetDictId(ReadOnlySpan<byte> dictionary)
    {
        if (dictionary.IsEmpty) return 0;
        fixed (byte* p = dictionary)
        {
            return ZstdInterop.ZSTD_getDictID_fromDict((IntPtr)p, (nuint)dictionary.Length);
        }
    }

    /// <summary>
    /// Extracts the dictionary ID from a Zstandard-compressed frame.
    /// </summary>
    /// <remarks>This method is useful for determining whether a specific dictionary was used during
    /// compression and for identifying the dictionary required for decompression.</remarks>
    /// <param name="frame">A read-only span of bytes representing the Zstandard-compressed frame.</param>
    /// <returns>The dictionary ID used to compress the frame, or <see langword="0"/> if no dictionary was used.</returns>
    public static uint GetDictIdFromFrame(ReadOnlySpan<byte> frame) =>
        ZstdInterop.ZSTD_getDictID_fromFrame(ref MemoryMarshal.GetReference(frame), (nuint)frame.Length);

    /// <summary>
    /// Determines whether the specified magic value represents a skippable frame.
    /// </summary>
    /// <remarks>A skippable frame is identified by a magic value within the range 0x184D2A50 to 0x184D2A5F,
    /// inclusive. This method can be used to filter or process frames based on their magic values.</remarks>
    /// <param name="magic">The magic value to evaluate.</param>
    /// <returns><see langword="true"/> if the magic value is within the range of skippable frames;  otherwise, <see
    /// langword="false"/>. </returns>
    public static bool IsSkippableFrameMagic(uint magic) => magic >= 0x184D2A50 && magic <= 0x184D2A5F;

    /// <summary>
    /// Compresses the input data using the specified compression level and writes the compressed data to the output
    /// buffer.
    /// </summary>
    /// <param name="input">The input data to be compressed. Must not be empty.</param>
    /// <param name="output">The buffer where the compressed data will be written. Must be large enough to hold the compressed data.</param>
    /// <param name="compressionLevel">The level of compression to apply, ranging from 1 (fastest) to 22 (most compressed).</param>
    /// <returns>The number of bytes written to the output buffer.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="input"/> is empty.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="compressionLevel"/> is not between 1 and 22.</exception>
    public static int Compress(ReadOnlySpan<byte> input, Span<byte> output, int compressionLevel)
    {
        if (input.Length == 0)
            throw new ArgumentException("Input must not be empty", nameof(input));

        if (compressionLevel < 1 || compressionLevel > 22)
            throw new ArgumentOutOfRangeException(nameof(compressionLevel), "Must be between 1 and 22");

        nuint result = ZstdInterop.Compress(
            ref MemoryMarshal.GetReference(output), (nuint)output.Length,
            ref MemoryMarshal.GetReference(input), (nuint)input.Length,
            compressionLevel);

        return CheckResult(result, "Compress");
    }

    /// <summary>
    /// Compresses the specified payload using the provided dictionary and compression level.
    /// </summary>
    /// <remarks>This method uses a Zstandard compression dictionary to optimize compression for data with
    /// recurring patterns. Ensure that the dictionary is appropriate for the type of data being compressed.</remarks>
    /// <param name="payload">The data to be compressed. Cannot be null.</param>
    /// <param name="dict">The dictionary to use for compression. This improves compression efficiency for similar data patterns. Cannot be
    /// null.</param>
    /// <param name="clevel">The compression level to apply. Must be a positive integer, where higher values typically result in better
    /// compression at the cost of performance.</param>
    /// <returns>The size of the compressed data, in bytes.</returns>
    public static int CompressWith(byte[] payload, byte[] dict, int clevel)
    {
        using var cdict = new ZstdCompressionDictionary(dict, clevel);
        var buf = new byte[Zstd.GetMaxCompressedSize(payload.Length)];
        return Zstd.CompressWithDict(payload, buf, cdict);
    }

    /// <summary>
    /// Decompresses the specified compressed data into the provided output buffer.
    /// </summary>
    /// <remarks>Ensure that the output buffer is appropriately sized to accommodate the decompressed data. 
    /// The method uses Zstandard (Zstd) for decompression.</remarks>
    /// <param name="compressedData">The compressed data to be decompressed.</param>
    /// <param name="output">The buffer where the decompressed data will be stored. Must be large enough to hold the decompressed data.</param>
    /// <returns>The number of bytes written to the output buffer.</returns>
    public static int Decompress(ReadOnlySpan<byte> compressedData, Span<byte> output)
    {
        nuint result = ZstdInterop.Decompress(
            ref MemoryMarshal.GetReference(output), (nuint)output.Length,
            ref MemoryMarshal.GetReference(compressedData), (nuint)compressedData.Length);

        return CheckResult(result, "Decompress");
    }

    /// <summary>
    /// Calculates the maximum size of the buffer required to hold the compressed data.
    /// </summary>
    /// <param name="uncompressedSize">The size of the uncompressed data in bytes. Must be non-negative.</param>
    /// <returns>The maximum size, in bytes, of the buffer needed to store the compressed data.</returns>
    public static int GetMaxCompressedSize(int uncompressedSize)
    {
        return checked((int)ZstdInterop.GetCompressBound((nuint)uncompressedSize));
    }

    /// <summary>
    /// Calculates the maximum size of the decompressed data for a given compressed frame.
    /// </summary>
    /// <remarks>This method is useful for allocating a buffer large enough to hold the decompressed data 
    /// before performing the actual decompression. The returned value represents an upper bound  and may exceed the
    /// actual size of the decompressed data.</remarks>
    /// <param name="frame">A read-only span of bytes representing the compressed frame.  The span must contain valid compressed data.</param>
    /// <returns>The maximum size, in bytes, of the decompressed data that can result from the given compressed frame.</returns>
    public static long GetMaxDecompressedSize(ReadOnlySpan<byte> frame) =>
        (long)ZstdInterop.ZSTD_decompressBound(ref MemoryMarshal.GetReference(frame), (nuint)frame.Length);

    /// <summary>
    /// Determines the decompressed size of the given compressed data.
    /// </summary>
    /// <remarks>This method uses the Zstandard algorithm to estimate the decompressed size. The returned size
    /// is based on metadata within the compressed data and may not be accurate if the data is corrupted or improperly
    /// formatted.</remarks>
    /// <param name="compressedData">A read-only span of bytes representing the compressed data.</param>
    /// <returns>The size, in bytes, of the data after decompression.</returns>
    public static long GetDecompressedSize(ReadOnlySpan<byte> compressedData)
    {
        return (long)ZstdInterop.GetDecompressedSize(
            ref MemoryMarshal.GetReference(compressedData), (nuint)compressedData.Length);
    }

    /// <summary>
    /// Compresses the specified input data using the given compression level.
    /// </summary>
    /// <param name="input">The byte array containing the data to be compressed. Cannot be null.</param>
    /// <param name="compressionLevel">The level of compression to apply, typically ranging from 0 (no compression) to 9 (maximum compression).</param>
    /// <returns>A byte array containing the compressed data. The size of the array may vary depending on the compression level
    /// and input data.</returns>
    public static byte[] Compress(byte[] input, int compressionLevel)
    {
        int maxSize = GetMaxCompressedSize(input.Length);
        byte[] output = new byte[maxSize];
        int written = Compress(input, output, compressionLevel);
        Array.Resize(ref output, written);
        return output;
    }

    /// <summary>
    /// Compresses the input data into the output buffer using the specified compression level.
    /// </summary>
    /// <param name="input">The byte array containing the data to be compressed.</param>
    /// <param name="output">The byte array where the compressed data will be stored.</param>
    /// <param name="compressionLevel">The level of compression to apply, typically ranging from 0 (no compression) to 9 (maximum compression).</param>
    /// <returns>The number of bytes written to the output buffer.</returns>
    public static int Compress(byte[] input, byte[] output, int compressionLevel)
    {
        return Compress((ReadOnlySpan<byte>)input, (Span<byte>)output, compressionLevel);
    }

    /// <summary>
    /// Decompresses the specified compressed data into a byte array.
    /// </summary>
    /// <remarks>The method resizes the output array to fit the actual decompressed data size after
    /// decompression. Ensure that <paramref name="maxExpectedSize"/> is a reasonable estimate to avoid unnecessary
    /// memory allocation.</remarks>
    /// <param name="compressedData">The byte array containing the data to decompress.</param>
    /// <param name="maxExpectedSize">The maximum expected size of the decompressed data. This determines the initial size of the output buffer.</param>
    /// <returns>A byte array containing the decompressed data. The size of the array is adjusted to match the actual
    /// decompressed data size.</returns>
    public static byte[] Decompress(byte[] compressedData, int maxExpectedSize)
    {
        byte[] output = new byte[maxExpectedSize];
        int written = Decompress(compressedData, output);
        Array.Resize(ref output, written);
        return output;
    }

    /// <summary>
    /// Decompresses the specified compressed data into the provided output buffer.
    /// </summary>
    /// <param name="compressedData">The byte array containing the compressed data to be decompressed.</param>
    /// <param name="output">The byte array where the decompressed data will be stored. Must be large enough to hold the decompressed data.</param>
    /// <returns>The number of bytes written to the output buffer.</returns>
    public static int Decompress(byte[] compressedData, byte[] output)
    {
        return Decompress((ReadOnlySpan<byte>)compressedData, (Span<byte>)output);
    }

    /// <summary>
    /// Validates the result of a Zstandard operation and throws an exception if an error is detected.
    /// </summary>
    /// <param name="result">The result code from a Zstandard operation to be checked for errors.</param>
    /// <param name="operation">A description of the operation being checked, used in the exception message if an error occurs.</param>
    /// <returns>The result code cast to an integer if no error is detected.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the result indicates an error, with a message describing the failure.</exception>
    private static int CheckResult(nuint result, string operation)
    {
        if (ZstdInterop.IsError(result) != 0)
        {
            IntPtr errPtr = ZstdInterop.GetErrorName(result);
            string err = Marshal.PtrToStringAnsi(errPtr) ?? "Unknown Zstd error";
            throw new InvalidOperationException($"{operation} failed: {err}");
        }

        return checked((int)result);
    }

    /// <summary>
    /// Compresses the input data using the specified Zstandard compression dictionary.
    /// </summary>
    /// <param name="input">The input data to be compressed. Must not be empty.</param>
    /// <param name="output">The buffer to store the compressed data. Must not be empty.</param>
    /// <param name="dict">The compression dictionary to use for the operation.</param>
    /// <returns>The size of the compressed data in bytes.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="input"/> or <paramref name="output"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the compression context could not be created.</exception>
    public static unsafe int CompressWithDict(ReadOnlySpan<byte> input, Span<byte> output, ZstdCompressionDictionary dict)
    {
        if (input.IsEmpty) throw new ArgumentException("Input is empty", nameof(input));
        if (output.IsEmpty) throw new ArgumentException("Output is empty", nameof(output));

        fixed (byte* inPtr = input)
        fixed (byte* outPtr = output)
        {
            IntPtr ctx = ZstdInterop.ZSTD_createCCtx();
            if (ctx == IntPtr.Zero)
                throw new InvalidOperationException("Failed to create compression context");

            try
            {
                var result = ZstdInterop.ZSTD_compress_usingCDict(
                    ctx,
                    (IntPtr)outPtr, (nuint)output.Length,
                    (IntPtr)inPtr, (nuint)input.Length,
                    dict.Handle);

                CheckResult(result, "ZSTD_compress_usingCDict");
                return (int)result;
            }
            finally
            {
                ZstdInterop.ZSTD_freeCCtx(ctx);
            }
        }
    }

    /// <summary>
    /// Decompresses the specified input data using a Zstandard decompression dictionary.
    /// </summary>
    /// <param name="input">The compressed data to be decompressed. Cannot be empty.</param>
    /// <param name="output">The buffer to store the decompressed data. Cannot be empty.</param>
    /// <param name="dict">The decompression dictionary to use for the operation.</param>
    /// <returns>The number of bytes written to the output buffer.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="input"/> or <paramref name="output"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the decompression context cannot be created.</exception>
    public static unsafe int DecompressWithDict(ReadOnlySpan<byte> input, Span<byte> output, ZstdDecompressionDictionary dict)
    {
        if (input.IsEmpty) throw new ArgumentException("Input is empty", nameof(input));
        if (output.IsEmpty) throw new ArgumentException("Output is empty", nameof(output));

        fixed (byte* inPtr = input)
        fixed (byte* outPtr = output)
        {
            IntPtr ctx = ZstdInterop.ZSTD_createDCtx();
            if (ctx == IntPtr.Zero)
                throw new InvalidOperationException("Failed to create decompression context");

            try
            {
                var result = ZstdInterop.ZSTD_decompress_usingDDict(
                    ctx,
                    (IntPtr)outPtr, (nuint)output.Length,
                    (IntPtr)inPtr, (nuint)input.Length,
                    dict.Handle);

                CheckResult(result, "ZSTD_decompress_usingDDict");
                return (int)result;
            }
            finally
            {
                ZstdInterop.ZSTD_freeDCtx(ctx);
            }
        }
    }

    /// <summary>
    /// Determines the compressed size of a Zstandard frame within the provided data.
    /// </summary>
    /// <param name="data">A read-only span of bytes containing the data to analyze. The span must contain  a valid Zstandard frame.</param>
    /// <returns>The size, in bytes, of the compressed Zstandard frame.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the operation fails, such as when the provided data does not contain  a valid Zstandard frame.</exception>
    public static int FindFrameCompressedSize(ReadOnlySpan<byte> data)
    {
        var n = ZstdInterop.ZSTD_findFrameCompressedSize(
            ref MemoryMarshal.GetReference(data), (nuint)data.Length);
        if (ZstdInterop.IsError(n) != 0)
            throw new InvalidOperationException("findFrameCompressedSize failed");
        return checked((int)n);
    }

    /// <summary>
    /// Compresses a file to <paramref name="outputPath"/> using Zstandard.
    /// </summary>
    /// <param name="inputPath">Path to the source file.</param>
    /// <param name="outputPath">Path to write the compressed file (overwritten if exists).</param>
    /// <param name="compressionLevel">1..22 (higher = smaller but slower). Default 3.</param>
    public static void CompressFile(string inputPath, string outputPath, int compressionLevel = 3)
        => CompressFileInternal(inputPath, outputPath, compressionLevel, dictBytes: null);

    /// <summary>
    /// Compresses a file with a Zstandard dictionary.
    /// </summary>
    /// <param name="inputPath">Path to the source file.</param>
    /// <param name="outputPath">Path to write the compressed file (overwritten if exists).</param>
    /// <param name="dict">Dictionary bytes (trained or prebuilt).</param>
    /// <param name="compressionLevel">1..22 (higher = smaller but slower). Default 3.</param>
    public static void CompressFile(string inputPath, string outputPath, byte[] dict, int compressionLevel = 3)
        => CompressFileInternal(inputPath, outputPath, compressionLevel, dictBytes: dict);

    /// <summary>
    /// Decompresses a .zst file to <paramref name="outputPath"/>.
    /// </summary>
    public static void DecompressFile(string inputPath, string outputPath)
        => DecompressFileInternal(inputPath, outputPath, dictBytes: null);

    /// <summary>
    /// Decompresses a .zst file that was compressed with the specified dictionary.
    /// </summary>
    public static void DecompressFile(string inputPath, string outputPath, byte[] dict)
        => DecompressFileInternal(inputPath, outputPath, dictBytes: dict);

    /// <summary>
    /// Compresses a file using the Zstandard compression algorithm and writes the compressed output to a specified
    /// file.
    /// </summary>
    /// <remarks>This method performs streaming compression, making it suitable for large files.  The output
    /// file's directory will be created if it does not already exist.</remarks>
    /// <param name="inputPath">The path to the input file to be compressed. Must not be null, empty, or whitespace.</param>
    /// <param name="outputPath">The path to the output file where the compressed data will be written. Must not be null, empty, or whitespace.</param>
    /// <param name="level">The compression level to use, ranging from 1 (fastest) to 22 (maximum compression).  Must be within the range of
    /// 1 to 22.</param>
    /// <param name="dictBytes">An optional byte array representing a custom compression dictionary.  If provided, the dictionary will be used
    /// to improve compression efficiency. Pass <see langword="null"/> or an empty array to use default compression
    /// settings.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="inputPath"/> or <paramref name="outputPath"/> is null, empty, or consists only of
    /// whitespace.</exception>
    /// <exception cref="FileNotFoundException">Thrown if the file specified by <paramref name="inputPath"/> does not exist.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="level"/> is outside the valid range of 1 to 22.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the compression stream or dictionary initialization fails, or if an error occurs during compression.</exception>
    private static unsafe void CompressFileInternal(string inputPath, string outputPath, int level, byte[]? dictBytes)
    {
        if (string.IsNullOrWhiteSpace(inputPath)) throw new ArgumentException("Input path is required.", nameof(inputPath));
        if (string.IsNullOrWhiteSpace(outputPath)) throw new ArgumentException("Output path is required.", nameof(outputPath));
        if (!File.Exists(inputPath)) throw new FileNotFoundException("Input file not found.", inputPath);
        if (level < 1 || level > 22) throw new ArgumentOutOfRangeException(nameof(level), "Compression level must be 1..22.");

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".");

        int inChunk = RecommendedCStreamInSize();
        int outChunk = RecommendedCStreamOutSize();

        byte[] inBuf = ArrayPool<byte>.Shared.Rent(inChunk);
        byte[] outBuf = ArrayPool<byte>.Shared.Rent(outChunk);

        IntPtr cctx = IntPtr.Zero;
        IntPtr cdict = IntPtr.Zero;

        try
        {
            cctx = ZstdInterop.ZSTD_createCStream();
            if (cctx == IntPtr.Zero) throw new InvalidOperationException("Failed to create ZSTD_CStream");

            nuint initRc;

            if (dictBytes is { Length: > 0 })
            {
                // Use a CDict (best perf when reusing across many files; fine here too)
                fixed (byte* pDict = dictBytes)
                {
                    cdict = ZstdInterop.ZSTD_createCDict((IntPtr)pDict, (nuint)dictBytes.Length, level);
                }
                if (cdict == IntPtr.Zero) throw new InvalidOperationException("Failed to create CDict");
                initRc = ZstdInterop.ZSTD_initCStream_usingCDict(cctx, cdict);
            }
            else
            {
                initRc = ZstdInterop.ZSTD_initCStream(cctx, level);
            }

            Check(initRc, "ZSTD_initCStream");

            using var fin = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, FileOptions.SequentialScan);
            using var fout = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20, FileOptions.SequentialScan);

            while (true)
            {
                int read = fin.Read(inBuf, 0, inBuf.Length);
                if (read == 0) break;

                fixed (byte* pin = inBuf)
                fixed (byte* pout = outBuf)
                {
                    var inB = new ZstdInBuffer { src = (IntPtr)pin, size = (nuint)read, pos = 0 };
                    while (inB.pos < inB.size)
                    {
                        var outB = new ZstdOutBuffer { dst = (IntPtr)pout, size = (nuint)outBuf.Length, pos = 0 };
                        nuint rc = ZstdInterop.ZSTD_compressStream(cctx, ref outB, ref inB);
                        Check(rc, "ZSTD_compressStream");
                        if (outB.pos > 0)
                            fout.Write(outBuf, 0, checked((int)outB.pos));
                    }
                }
            }

            // Finish the frame
            fixed (byte* pout = outBuf)
            {
                var outB = new ZstdOutBuffer { dst = (IntPtr)pout, size = (nuint)outBuf.Length, pos = 0 };
                nuint remaining;
                do
                {
                    outB.pos = 0;
                    remaining = ZstdInterop.ZSTD_endStream(cctx, ref outB);
                    Check(remaining, "ZSTD_endStream");
                    if (outB.pos > 0)
                        fout.Write(outBuf, 0, checked((int)outB.pos));
                } while (remaining != 0);
            }
        }
        finally
        {
            if (cdict != IntPtr.Zero) ZstdInterop.ZSTD_freeCDict(cdict);
            if (cctx != IntPtr.Zero) ZstdInterop.ZSTD_freeCStream(cctx);
            ArrayPool<byte>.Shared.Return(inBuf);
            ArrayPool<byte>.Shared.Return(outBuf);
        }

        static void Check(nuint code, string ctx)
        {
            if (ZstdInterop.IsError(code) != 0)
            {
                var ptr = ZstdInterop.GetErrorName(code);
                var msg = Marshal.PtrToStringAnsi(ptr) ?? "Unknown zstd error";
                throw new InvalidOperationException($"{ctx} failed: {msg}");
            }
        }
    }

    /// <summary>
    /// Decompresses a file using Zstandard compression and writes the decompressed data to the specified output file.
    /// </summary>
    /// <remarks>This method uses Zstandard decompression to process the input file and write the decompressed
    /// data to the output file. It creates the necessary directories for the output file if they do not already exist.
    /// The method is optimized for sequential file access and uses buffer pooling to minimize memory
    /// allocations.</remarks>
    /// <param name="inputPath">The path to the input file to be decompressed. Must not be null, empty, or whitespace.</param>
    /// <param name="outputPath">The path to the output file where the decompressed data will be written. Must not be null, empty, or whitespace.</param>
    /// <param name="dictBytes">An optional byte array representing a Zstandard dictionary to use for decompression. If null or empty, no
    /// dictionary is used.</param>
    /// <exception cref="ArgumentException">Thrown if <paramref name="inputPath"/> or <paramref name="outputPath"/> is null, empty, or consists only of
    /// whitespace.</exception>
    /// <exception cref="FileNotFoundException">Thrown if the file specified by <paramref name="inputPath"/> does not exist.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the Zstandard decompression stream or dictionary cannot be initialized, or if a decompression error
    /// occurs.</exception>
    private static unsafe void DecompressFileInternal(string inputPath, string outputPath, byte[]? dictBytes)
    {
        if (string.IsNullOrWhiteSpace(inputPath)) throw new ArgumentException("Input path is required.", nameof(inputPath));
        if (string.IsNullOrWhiteSpace(outputPath)) throw new ArgumentException("Output path is required.", nameof(outputPath));
        if (!File.Exists(inputPath)) throw new FileNotFoundException("Input file not found.", inputPath);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath)) ?? ".");

        int inChunk = RecommendedDStreamInSize();
        int outChunk = RecommendedDStreamOutSize();

        byte[] inBuf = ArrayPool<byte>.Shared.Rent(inChunk);
        byte[] outBuf = ArrayPool<byte>.Shared.Rent(outChunk);

        IntPtr dctx = IntPtr.Zero;
        IntPtr ddict = IntPtr.Zero;

        try
        {
            dctx = ZstdInterop.ZSTD_createDStream();
            if (dctx == IntPtr.Zero) throw new InvalidOperationException("Failed to create ZSTD_DStream");

            nuint initRc;
            if (dictBytes is { Length: > 0 })
            {
                fixed (byte* pDict = dictBytes)
                {
                    ddict = ZstdInterop.ZSTD_createDDict((IntPtr)pDict, (nuint)dictBytes.Length);
                }
                if (ddict == IntPtr.Zero) throw new InvalidOperationException("Failed to create DDict");
                initRc = ZstdInterop.ZSTD_initDStream_usingDDict(dctx, ddict);
            }
            else
            {
                initRc = ZstdInterop.ZSTD_initDStream(dctx);
            }
            Check(initRc, "ZSTD_initDStream");

            using var fin = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, FileOptions.SequentialScan);
            using var fout = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20, FileOptions.SequentialScan);

            int read;
            while ((read = fin.Read(inBuf, 0, inBuf.Length)) > 0)
            {
                fixed (byte* pin = inBuf)
                fixed (byte* pout = outBuf)
                {
                    var inB = new ZstdInBuffer { src = (IntPtr)pin, size = (nuint)read, pos = 0 };
                    while (inB.pos < inB.size)
                    {
                        var outB = new ZstdOutBuffer { dst = (IntPtr)pout, size = (nuint)outBuf.Length, pos = 0 };
                        nuint rc = ZstdInterop.ZSTD_decompressStream(dctx, ref outB, ref inB);
                        Check(rc, "ZSTD_decompressStream");
                        if (outB.pos > 0)
                            fout.Write(outBuf, 0, checked((int)outB.pos));
                    }
                }
            }
        }
        finally
        {
            if (ddict != IntPtr.Zero) ZstdInterop.ZSTD_freeDDict(ddict);
            if (dctx != IntPtr.Zero) ZstdInterop.ZSTD_freeDStream(dctx);
            ArrayPool<byte>.Shared.Return(inBuf);
            ArrayPool<byte>.Shared.Return(outBuf);
        }

        static void Check(nuint code, string ctx)
        {
            if (ZstdInterop.IsError(code) != 0)
            {
                var ptr = ZstdInterop.GetErrorName(code);
                var msg = Marshal.PtrToStringAnsi(ptr) ?? "Unknown zstd error";
                throw new InvalidOperationException($"{ctx} failed: {msg}");
            }
        }
    }

    /// <summary>
    /// Enumerates the frames within a Zstandard-compressed binary blob.
    /// </summary>
    /// <remarks>This method identifies both skippable frames and regular Zstandard frames within the provided
    /// binary blob. Skippable frames are detected based on their magic number range (0x184D2A50 to 0x184D2A5F), and
    /// their size is determined using the size field. Regular Zstandard frames are processed to determine their
    /// compressed size.  If the blob does not contain enough data to fully define a frame, the enumeration
    /// stops.</remarks>
    /// <param name="blob">A read-only span of bytes representing the binary blob to analyze.</param>
    /// <returns>A list of tuples, where each tuple contains the offset and length of a frame. The offset represents the starting
    /// position of the frame within the blob, and the length represents the size of the frame.</returns>
    public static List<(int Offset, int Length)> EnumerateFrames(ReadOnlySpan<byte> blob)
    {
        var frames = new List<(int, int)>();
        int off = 0;

        while (off < blob.Length)
        {
            // Need at least 4 bytes for magic
            if (off + 4 > blob.Length) break;

            // Read magic (little-endian in zstd spec)
            uint magic = BinaryPrimitives.ReadUInt32LittleEndian(blob.Slice(off, 4));

            // Skippable frames (0x184D2A50..0x184D2A5F)
            if (magic >= 0x184D2A50 && magic <= 0x184D2A5F)
            {
                if (off + 8 > blob.Length) break; // need size field
                uint size = BinaryPrimitives.ReadUInt32LittleEndian(blob.Slice(off + 4, 4));
                long len64 = 8L + size;
                if (len64 > int.MaxValue) break;
                int len = (int)len64;
                if (off + len > blob.Length) break;

                frames.Add((off, len));
                off += len;
                continue;
            }

            // Regular zstd frame: ask zstd for exact length
            int flen = FindFrameCompressedSize(blob.Slice(off));
            if (flen <= 0 || off + flen > blob.Length) break;

            frames.Add((off, flen));
            off += flen;
        }

        return frames;
    }
}
