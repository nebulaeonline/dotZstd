using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace nebulae.dotZstd;

/// <summary>
/// Provides functionality for compressing data using the Zstandard compression algorithm.
/// </summary>
/// <remarks>The <see cref="ZstdCompressStream"/> class enables efficient data compression using Zstandard, a fast
/// and high-ratio compression algorithm. It supports various features such as custom compression levels, dictionaries,
/// and multi-threading. Instances of this class are not thread-safe and must be disposed of when no longer needed to
/// release unmanaged resources.</remarks>
public sealed class ZstdCompressStream : IDisposable
{
    private readonly IntPtr _cstream;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ZstdCompressStream"/> class with the specified compression level.
    /// </summary>
    /// <remarks>This constructor initializes the ZSTD compression stream and prepares it for use. Ensure that
    /// the specified compression level is within the valid range to avoid unexpected behavior.</remarks>
    /// <param name="compressionLevel">The compression level to use for the stream. Valid values range from 1 (fastest compression) to 22 (maximum
    /// compression).</param>
    /// <exception cref="InvalidOperationException">Thrown if the underlying ZSTD compression stream could not be allocated or initialized.</exception>
    public ZstdCompressStream(int compressionLevel)
    {
        ZstdLibrary.Init();
        _cstream = ZstdInterop.ZSTD_createCStream();
        if (_cstream == IntPtr.Zero)
            throw new InvalidOperationException("Failed to allocate ZSTD_CStream");

        var initResult = ZstdInterop.ZSTD_initCStream(_cstream, compressionLevel);
        Check(initResult, "ZSTD_initCStream");
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ZstdCompressStream"/> class using the specified Zstandard
    /// compression dictionary.
    /// </summary>
    /// <remarks>This constructor sets up a Zstandard compression stream using the provided dictionary.  The
    /// dictionary is used to optimize compression for specific data patterns. Ensure that the dictionary is properly
    /// initialized before passing it to this constructor.</remarks>
    /// <param name="dict">The compression dictionary to use for initializing the stream. Must not be null.</param>
    /// <exception cref="InvalidOperationException">Thrown if the compression stream could not be allocated or initialized.</exception>
    public ZstdCompressStream(ZstdCompressionDictionary dict)
    {
        ZstdLibrary.Init();
        _cstream = ZstdInterop.ZSTD_createCStream();
        if (_cstream == IntPtr.Zero)
            throw new InvalidOperationException("Failed to allocate ZSTD_CStream");

        var initResult = ZstdInterop.ZSTD_initCStream_usingCDict(_cstream, dict.Handle);
        Check(initResult, "ZSTD_initCStream_usingCDict");
    }

    /// <summary>
    /// Compresses the specified input data into the provided output buffer.
    /// </summary>
    /// <param name="input">The input data to be compressed. Must not be empty.</param>
    /// <param name="output">The buffer to store the compressed data. Must not be empty.</param>
    /// <param name="inputFullyConsumed">When this method returns, contains a value indicating whether the entire input was consumed during compression.</param>
    /// <returns>The number of bytes written to the output buffer.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="input"/> is empty or if <paramref name="output"/> is empty.</exception>
    public unsafe int Compress(ReadOnlySpan<byte> input, Span<byte> output, out bool inputFullyConsumed)
    {
        EnsureNotDisposed();

        if (input.IsEmpty)
            throw new ArgumentException("Input is empty", nameof(input));
        if (output.IsEmpty)
            throw new ArgumentException("Output is empty", nameof(output));

        fixed (byte* inPtr = input)
        fixed (byte* outPtr = output)
        {
            var inBuf = new ZstdInBuffer
            {
                src = (IntPtr)inPtr,
                size = (nuint)input.Length,
                pos = 0
            };

            var outBuf = new ZstdOutBuffer
            {
                dst = (IntPtr)outPtr,
                size = (nuint)output.Length,
                pos = 0
            };

            var result = ZstdInterop.ZSTD_compressStream(_cstream, ref outBuf, ref inBuf);
            Check(result, "ZSTD_compressStream");

            inputFullyConsumed = (inBuf.pos == inBuf.size);
            return (int)outBuf.pos;
        }
    }

    /// <summary>
    /// Flushes any buffered data to the specified output span.
    /// </summary>
    /// <remarks>This method ensures that all buffered data is written to the provided output span.  It is
    /// important to check the return value to determine how many bytes were actually written.</remarks>
    /// <param name="output">The span of bytes where the flushed data will be written. Must be large enough to hold the data.</param>
    /// <returns>The number of bytes written to the output span.</returns>
    public unsafe int Flush(Span<byte> output)
    {
        EnsureNotDisposed();

        if (output.IsEmpty)
            throw new ArgumentException("Output is empty", nameof(output));

        fixed (byte* outPtr = output)
        {
            var outBuffer = new ZstdOutBuffer
            {
                dst = (IntPtr)outPtr,
                size = (nuint)output.Length,
                pos = 0
            };

            var result = ZstdInterop.ZSTD_flushStream(_cstream, ref outBuffer);
            Check(result, "ZSTD_flushStream");
            return (int)outBuffer.pos;
        }
    }

    /// <summary>
    /// Finalizes the compression stream and writes any remaining compressed data to the specified output buffer.
    /// </summary>
    /// <param name="output">The buffer to receive the remaining compressed data. Must not be empty.</param>
    /// <returns>The number of bytes written to the output buffer.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="output"/> is empty.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the output buffer is too small to complete the stream.</exception>
    public unsafe int Finish(Span<byte> output)
    {
        EnsureNotDisposed();
        if (output.IsEmpty)
            throw new ArgumentException("Output buffer is empty", nameof(output));

        fixed (byte* outPtr = output)
        {
            var outBuf = new ZstdOutBuffer
            {
                dst = (IntPtr)outPtr,
                size = (nuint)output.Length,
                pos = 0
            };

            nuint remaining = ZstdInterop.ZSTD_endStream(_cstream, ref outBuf);
            Check(remaining, "ZSTD_endStream");

            if (remaining != 0)
                throw new InvalidOperationException("Output buffer too small to complete stream");

            return (int)outBuf.pos;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        ZstdInterop.ZSTD_freeCStream(_cstream);
        _disposed = true;
    }

    /// <summary>
    /// Validates the result of a Zstandard operation and throws an exception if an error is detected.
    /// </summary>
    /// <param name="result">The result code from a Zstandard operation to be checked for errors.</param>
    /// <param name="context">A string providing context about the operation being checked, used in the exception message if an error occurs.</param>
    /// <exception cref="InvalidOperationException">Thrown if the <paramref name="result"/> indicates an error. The exception message includes the <paramref
    /// name="context"/> and the error name.</exception>
    private static void Check(nuint result, string context)
    {
        if (ZstdInterop.IsError(result) != 0)
        {
            var ptr = ZstdInterop.GetErrorName(result);
            var name = Marshal.PtrToStringAnsi(ptr) ?? "Unknown error";
            throw new InvalidOperationException($"{context} failed: {name}");
        }
    }

    private void EnsureNotDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ZstdCompressStream));
    }

    /// <summary>
    /// Compresses the data from the input buffer into the output buffer.
    /// </summary>
    /// <param name="input">The byte array containing the data to be compressed. Cannot be null.</param>
    /// <param name="output">The byte array where the compressed data will be stored. Cannot be null.</param>
    /// <param name="inputFullyConsumed">When this method returns, contains a boolean value indicating whether the entire input buffer was consumed
    /// during compression.</param>
    /// <returns>The number of bytes written to the output buffer.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="input"/> or <paramref name="output"/> is null.</exception>
    public int Compress(byte[] input, byte[] output, out bool inputFullyConsumed)
    {
        if (input is null) throw new ArgumentNullException(nameof(input));
        if (output is null) throw new ArgumentNullException(nameof(output));

        return Compress(input.AsSpan(), output.AsSpan(), out inputFullyConsumed);
    }

    /// <summary>
    /// Flushes any buffered data to the specified output array.
    /// </summary>
    /// <param name="output">The byte array to which the buffered data will be written. Cannot be null.</param>
    /// <returns>The number of bytes written to the output array.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="output"/> is null.</exception>
    public int Flush(byte[] output)
    {
        if (output is null) throw new ArgumentNullException(nameof(output));
        return Flush(output.AsSpan());
    }

    /// <summary>
    /// Finalizes the operation and writes the result to the specified output buffer.
    /// </summary>
    /// <param name="output">The buffer to which the final result is written. Cannot be <see langword="null"/>.</param>
    /// <returns>The number of bytes written to the output buffer.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="output"/> is <see langword="null"/>.</exception>
    public int Finish(byte[] output)
    {
        if (output is null) throw new ArgumentNullException(nameof(output));
        return Finish(output.AsSpan());
    }

    /// <summary>
    /// Configures the compression stream to use the specified number of worker threads.
    /// </summary>
    /// <remarks>Using multiple worker threads can improve compression performance for large data streams.
    /// However, the actual performance gain depends on the system's hardware and workload.</remarks>
    /// <param name="workers">The number of worker threads to use for compression. Must be a non-negative integer. A value of 0 disables
    /// multi-threading, while higher values increase parallelism.</param>
    /// <returns>The current <see cref="ZstdCompressStream"/> instance, allowing for method chaining.</returns>
    public ZstdCompressStream WithWorkers(int workers)
    {
        var rc = ZstdInterop.ZSTD_CCtx_setParameter(_cstream, ZSTD_cParameter.ZSTD_c_nbWorkers, workers);
        Check(rc, "ZSTD_CCtx_setParameter(nbWorkers)");
        return this;
    }

    /// <summary>
    /// Sets the pledged source size for the compression stream and returns the current instance.
    /// </summary>
    /// <remarks>Setting the pledged source size can improve compression efficiency by enabling the compressor
    /// to make better decisions. If the actual size of the source data differs from the pledged size, compression may
    /// still succeed, but performance or compression ratio may be affected.</remarks>
    /// <param name="size">The total size, in bytes, of the source data to be compressed. This value is used to optimize compression.</param>
    /// <returns>The current <see cref="ZstdCompressStream"/> instance, allowing for method chaining.</returns>
    public ZstdCompressStream WithPledgedSize(ulong size)
    {
        var rc = ZstdInterop.ZSTD_CCtx_setPledgedSrcSize(_cstream, size); Check(rc, "ZSTD_CCtx_setPledgedSrcSize"); return this;
    }

    /// <summary>
    /// Enables or disables the inclusion of a checksum in the compressed stream.
    /// </summary>
    /// <remarks>When the checksum is enabled, a checksum is appended to the compressed stream,  which can be
    /// used to verify the integrity of the data during decompression.</remarks>
    /// <param name="on">A value indicating whether to enable the checksum.  <see langword="true"/> enables the checksum; <see
    /// langword="false"/> disables it.  The default is <see langword="true"/>.</param>
    /// <returns>The current <see cref="ZstdCompressStream"/> instance, allowing for method chaining.</returns>
    public ZstdCompressStream ToggleChecksum(bool on = true)
    {
        var rc = ZstdInterop.ZSTD_CCtx_setParameter(_cstream, ZSTD_cParameter.ZSTD_c_checksumFlag, on ? 1 : 0);
        Check(rc, "ZSTD_CCtx_setParameter(checksum)");
        return this;
    }

    /// <summary>
    /// Loads a compression dictionary into the current compression stream.
    /// </summary>
    /// <remarks>The dictionary is used to improve compression efficiency for data that shares similarities 
    /// with the dictionary content. Ensure that the dictionary is properly constructed and matches  the data being
    /// compressed to achieve optimal results.</remarks>
    /// <param name="dictBytes">A read-only span of bytes representing the dictionary to be loaded.  The dictionary must be valid and compatible
    /// with the Zstandard compression format.</param>
    /// <returns>The current <see cref="ZstdCompressStream"/> instance, allowing for method chaining.</returns>
    public unsafe ZstdCompressStream LoadDictionary(ReadOnlySpan<byte> dictBytes)
    {
        EnsureNotDisposed();
        fixed (byte* p = dictBytes)
        {
            var rc = ZstdInterop.ZSTD_CCtx_loadDictionary(_cstream, (IntPtr)p, (nuint)dictBytes.Length);
            Check(rc, "ZSTD_CCtx_loadDictionary");
        }
        return this;
    }

    /// <summary>
    /// Loads a compression dictionary into the current stream by reference. You must ensure that the
    /// reference remains valid for the lifetime of the stream.
    /// </summary>
    /// <remarks>This method uses the provided dictionary bytes directly without copying them, which can
    /// improve performance. The caller must ensure that the memory backing <paramref name="dictBytes"/> remains valid
    /// for the duration of its use.</remarks>
    /// <param name="dictBytes">A read-only span of bytes representing the compression dictionary. The span must remain valid while the
    /// dictionary is in use.</param>
    /// <returns>The current <see cref="ZstdCompressStream"/> instance, allowing for method chaining.</returns>
    public unsafe ZstdCompressStream LoadDictionaryByReference(ReadOnlySpan<byte> dictBytes)
    {
        EnsureNotDisposed();
        fixed (byte* p = dictBytes)
        {
            var rc = ZstdInterop.ZSTD_CCtx_loadDictionary_byReference(_cstream, (IntPtr)p, (nuint)dictBytes.Length);
            Check(rc, "ZSTD_CCtx_loadDictionary_byReference");
        }
        return this;
    }

    /// <summary>
    /// Associates a pre-trained compression dictionary with the current compression stream.
    /// </summary>
    /// <remarks>Using a compression dictionary can improve compression ratios for data that shares
    /// similarities  with the dictionary's training set. Ensure the dictionary is compatible with the data being
    /// compressed.</remarks>
    /// <param name="cdict">The compression dictionary to use for subsequent compression operations.  Cannot be <see langword="null"/>.</param>
    /// <returns>The current <see cref="ZstdCompressStream"/> instance, allowing for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="cdict"/> is <see langword="null"/>.</exception>
    public ZstdCompressStream RefDictionary(ZstdCompressionDictionary cdict)
    {
        if (cdict is null) throw new ArgumentNullException(nameof(cdict));
        EnsureNotDisposed();
        var rc = ZstdInterop.ZSTD_CCtx_refCDict(_cstream, cdict.Handle);
        Check(rc, "ZSTD_CCtx_refCDict");
        return this;
    }

    /// <summary>
    /// Enables or disables long-distance matching for compression.
    /// </summary>
    /// <remarks>Long-distance matching can improve compression ratios for inputs with repeated patterns  that
    /// are far apart. However, enabling this feature may increase memory usage and compression time.</remarks>
    /// <param name="on">A value indicating whether long-distance matching should be enabled.  <see langword="true"/> enables
    /// long-distance matching; <see langword="false"/> disables it.  The default is <see langword="true"/>.</param>
    /// <returns>The current <see cref="ZstdCompressStream"/> instance, allowing for method chaining.</returns>
    public ZstdCompressStream ToggleLongDistanceMatching(bool on = true)
    {
        EnsureNotDisposed();
        var rc = ZstdInterop.ZSTD_CCtx_setParameter(
            _cstream,
            ZSTD_cParameter.ZSTD_c_longDistanceMatching,
            on ? 1 : 0);
        Check(rc, "ZSTD_CCtx_setParameter(longDistanceMatching)");
        return this;
    }
}

