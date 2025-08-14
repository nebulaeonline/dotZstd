using System;
using System.Runtime.InteropServices;

namespace nebulae.dotZstd;

/// <summary>
/// Provides functionality for decompressing data using the Zstandard compression algorithm.
/// </summary>
/// <remarks>The <see cref="ZstdDecompressStream"/> class enables efficient decompression of data compressed with
/// the Zstandard algorithm. It supports operations such as loading dictionaries, configuring decompression parameters,
/// and handling decompression streams. Instances of this class are not thread-safe and must be disposed when no longer
/// needed to release unmanaged resources.</remarks>
public sealed class ZstdDecompressStream : IDisposable
{
    private readonly IntPtr _dstream;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="ZstdDecompressStream"/> class, setting up the decompression stream
    /// for use.
    /// </summary>
    /// <remarks>This constructor initializes the Zstandard decompression stream by allocating the necessary
    /// resources and performing initialization. If the decompression stream cannot be allocated or initialized, an
    /// exception is thrown.</remarks>
    /// <exception cref="InvalidOperationException">Thrown if the decompression stream fails to allocate or initialize.</exception>
    public ZstdDecompressStream()
    {
        ZstdLibrary.Init();
        _dstream = ZstdInterop.ZSTD_createDStream();
        if (_dstream == IntPtr.Zero)
            throw new InvalidOperationException("Failed to allocate ZSTD_DStream");

        var initResult = ZstdInterop.ZSTD_initDStream(_dstream);
        Check(initResult, "ZSTD_initDStream");
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ZstdDecompressStream"/> class using the specified decompression
    /// dictionary.
    /// </summary>
    /// <remarks>This constructor sets up a Zstandard decompression stream using the provided dictionary. The
    /// dictionary is used to optimize decompression for data encoded with the corresponding compression dictionary.
    /// Ensure that the dictionary passed to this constructor matches the one used during compression to avoid errors or
    /// suboptimal performance.</remarks>
    /// <param name="dict">The decompression dictionary to use for initializing the stream. This dictionary must be valid and properly
    /// configured for the data being decompressed.</param>
    /// <exception cref="InvalidOperationException">Thrown if the decompression stream could not be allocated or initialized.</exception>
    public ZstdDecompressStream(ZstdDecompressionDictionary dict)
    {
        ZstdLibrary.Init();
        _dstream = ZstdInterop.ZSTD_createDStream();
        if (_dstream == IntPtr.Zero)
            throw new InvalidOperationException("Failed to allocate ZSTD_DStream");

        var initResult = ZstdInterop.ZSTD_initDStream_usingDDict(_dstream, dict.Handle);
        Check(initResult, "ZSTD_initDStream_usingDDict");
    }

    /// <summary>
    /// Decompresses the data from the input buffer into the output buffer.
    /// </summary>
    /// <param name="input">The input buffer containing compressed data. Must not be empty.</param>
    /// <param name="output">The output buffer where decompressed data will be written. Must not be empty.</param>
    /// <param name="inputFullyConsumed">When this method returns, contains a value indicating whether the entire input buffer was consumed during
    /// decompression.</param>
    /// <returns>The number of bytes written to the output buffer.</returns>
    /// <exception cref="ArgumentException">Thrown if <paramref name="input"/> is empty or if <paramref name="output"/> is empty.</exception>
    public unsafe int Decompress(ReadOnlySpan<byte> input, Span<byte> output, out bool inputFullyConsumed)
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

            var result = ZstdInterop.ZSTD_decompressStream(_dstream, ref outBuf, ref inBuf);
            Check(result, "ZSTD_decompressStream");

            inputFullyConsumed = (inBuf.pos == inBuf.size);
            return (int)outBuf.pos;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        ZstdInterop.ZSTD_freeDStream(_dstream);
        _disposed = true;
    }

    /// <summary>
    /// Validates the result of a Zstandard operation and throws an exception if an error is detected.
    /// </summary>
    /// <param name="result">The result code from a Zstandard operation to be checked for errors.</param>
    /// <param name="context">A string describing the context in which the operation was performed, used in the exception message if an error
    /// occurs.</param>
    /// <exception cref="InvalidOperationException">Thrown if the result indicates an error. The exception message includes the context and a description of the
    /// error.</exception>
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
            throw new ObjectDisposedException(nameof(ZstdDecompressStream));
    }

    /// <summary>
    /// Decompresses the data from the input byte array into the output byte array.
    /// </summary>
    /// <param name="input">The byte array containing the compressed data. Cannot be <see langword="null"/>.</param>
    /// <param name="output">The byte array to store the decompressed data. Cannot be <see langword="null"/>.</param>
    /// <param name="inputFullyConsumed">When this method returns, contains a <see langword="true"/> if the entire input was decompressed; otherwise,
    /// <see langword="false"/>.</param>
    /// <returns>The number of bytes written to the output array.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="input"/> or <paramref name="output"/> is <see langword="null"/>.</exception>
    public int Decompress(byte[] input, byte[] output, out bool inputFullyConsumed)
    {
        if (input is null) throw new ArgumentNullException(nameof(input));
        if (output is null) throw new ArgumentNullException(nameof(output));

        return Decompress(input.AsSpan(), output.AsSpan(), out inputFullyConsumed);
    }

    /// <summary>
    /// Configures the decompression stream to use a stable output buffer.
    /// </summary>
    /// <remarks>When stable output buffer mode is enabled, the decompression stream ensures that the output
    /// buffer  remains consistent across decompression calls, which can be useful in scenarios requiring predictable 
    /// buffer behavior. This setting may affect performance depending on the use case.</remarks>
    /// <param name="on">A value indicating whether to enable the stable output buffer mode.  <see langword="true"/> enables stable
    /// output buffer mode; <see langword="false"/> disables it.</param>
    /// <param name="throwIfUnsupported">This parameter indicates whether to throw an exception if the stable output buffer mode is not supported by the underlying Zstandard library.</param>
    /// <returns>The current <see cref="ZstdDecompressStream"/> instance, allowing for method chaining.</returns>
    public ZstdDecompressStream StableOutBuffer(bool on = true, bool throwIfUnsupported = false)
    {
        EnsureNotDisposed();
        var rc = ZstdInterop.ZSTD_DCtx_setParameter(
            _dstream,
            ZSTD_dParameter.ZSTD_d_stableOutBuffer,
            on ? 1 : 0);

        if (ZstdInterop.IsError(rc) != 0)
        {
            var err = Marshal.PtrToStringAnsi(ZstdInterop.GetErrorName(rc)) ?? "unknown";
            // Gracefully ignore "unsupported"/"out of bound" unless the caller insists
            if (!throwIfUnsupported &&
                (err.Contains("Unsupported", StringComparison.OrdinalIgnoreCase) ||
                 err.Contains("unsupported", StringComparison.OrdinalIgnoreCase) ||
                 err.Contains("out of bound", StringComparison.OrdinalIgnoreCase)))
            {
                return this; // no-op on older zstd builds
            }
            throw new InvalidOperationException($"ZSTD_DCtx_setParameter(stableOutBuffer) failed: {err}");
        }
        return this;
    }

    /// <summary>
    /// Loads a dictionary into the decompression stream to improve decompression performance.
    /// </summary>
    /// <remarks>Using a dictionary can significantly improve decompression speed and compression ratio for
    /// data that was compressed with the same dictionary. Ensure the dictionary matches the one used during
    /// compression.</remarks>
    /// <param name="dictBytes">A read-only span of bytes representing the dictionary to be loaded. The dictionary must be in a valid format
    /// supported by Zstandard.</param>
    /// <returns>The current <see cref="ZstdDecompressStream"/> instance, allowing for method chaining.</returns>
    public unsafe ZstdDecompressStream LoadDictionary(ReadOnlySpan<byte> dictBytes)
    {
        EnsureNotDisposed();
        fixed (byte* p = dictBytes)
        {
            var rc = ZstdInterop.ZSTD_DCtx_loadDictionary(_dstream, (IntPtr)p, (nuint)dictBytes.Length);
            Check(rc, "ZSTD_DCtx_loadDictionary");
        }
        return this;
    }

    /// <summary>
    /// Loads a dictionary into the decompression stream by reference. You must ensure that the memory backing
    /// the dictionary remains valid and unmodified while the dictionary is in use.
    /// </summary>
    /// <remarks>This method loads the dictionary directly by reference, without copying its contents.  The
    /// caller must ensure that the memory backing <paramref name="dictBytes"/> remains valid  and unmodified while the
    /// dictionary is in use.</remarks>
    /// <param name="dictBytes">A read-only span of bytes representing the dictionary to be loaded. The span must remain valid  for the duration
    /// of its use in the decompression stream.</param>
    /// <returns>The current <see cref="ZstdDecompressStream"/> instance, allowing for method chaining.</returns>
    public unsafe ZstdDecompressStream LoadDictionaryByReference(ReadOnlySpan<byte> dictBytes)
    {
        EnsureNotDisposed();
        fixed (byte* p = dictBytes)
        {
            var rc = ZstdInterop.ZSTD_DCtx_loadDictionary_byReference(_dstream, (IntPtr)p, (nuint)dictBytes.Length);
            Check(rc, "ZSTD_DCtx_loadDictionary_byReference");
        }
        return this;
    }

    /// <summary>
    /// Associates a decompression dictionary with the current decompression stream.
    /// </summary>
    /// <remarks>This method enables the use of a predefined decompression dictionary to improve decompression
    /// performance for data compressed with the corresponding dictionary. Ensure that the dictionary  matches the one
    /// used during compression.</remarks>
    /// <param name="ddict">The decompression dictionary to be used. This parameter cannot be <see langword="null"/>.</param>
    /// <returns>The current <see cref="ZstdDecompressStream"/> instance, allowing for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="ddict"/> is <see langword="null"/>.</exception>
    public ZstdDecompressStream RefDictionary(ZstdDecompressionDictionary ddict)
    {
        if (ddict is null) throw new ArgumentNullException(nameof(ddict));
        EnsureNotDisposed();
        var rc = ZstdInterop.ZSTD_DCtx_refDDict(_dstream, ddict.Handle);
        Check(rc, "ZSTD_DCtx_refDDict");
        return this;
    }
}

