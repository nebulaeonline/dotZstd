using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace nebulae.dotZstd;

public sealed class ZstdCompressStream : IDisposable
{
    private readonly IntPtr _cstream;
    private bool _disposed;

    public ZstdCompressStream(int compressionLevel)
    {
        ZstdLibrary.Init();
        _cstream = ZstdInterop.ZSTD_createCStream();
        if (_cstream == IntPtr.Zero)
            throw new InvalidOperationException("Failed to allocate ZSTD_CStream");

        var initResult = ZstdInterop.ZSTD_initCStream(_cstream, compressionLevel);
        Check(initResult, "ZSTD_initCStream");
    }

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
}

