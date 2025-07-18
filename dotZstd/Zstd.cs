using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace nebulae.dotZstd;

public static class Zstd
{
    public static void Init()
    {
        ZstdLibrary.Init();
    }

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
}
