using System.Runtime.InteropServices;

namespace nebulae.dotZstd;

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
}
