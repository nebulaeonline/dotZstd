using System;
using System.Runtime.InteropServices;

namespace nebulae.dotZstd;

public sealed class ZstdDecompressStream : IDisposable
{
    private readonly IntPtr _dstream;
    private bool _disposed;

    public ZstdDecompressStream()
    {
        ZstdLibrary.Init();
        _dstream = ZstdInterop.ZSTD_createDStream();
        if (_dstream == IntPtr.Zero)
            throw new InvalidOperationException("Failed to allocate ZSTD_DStream");

        var initResult = ZstdInterop.ZSTD_initDStream(_dstream);
        Check(initResult, "ZSTD_initDStream");
    }

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
}

