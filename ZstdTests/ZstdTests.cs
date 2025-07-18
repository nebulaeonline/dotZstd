using nebulae.dotZstd;
using System.Text;

namespace ZstdTests;

public class ZstdTests
{
    private static readonly byte[] Plaintext = Encoding.UTF8.GetBytes("The quick brown fox jumps over the lazy dog. The quick brown fox jumps over the lazy dog. The quick brown fox...");

    [Fact]
    public void CompressDecompress_ByteArray_Roundtrip()
    {
        Zstd.Init();
        byte[] compressed = Zstd.Compress(Plaintext, 3);
        byte[] decompressed = Zstd.Decompress(compressed, Plaintext.Length);
        Assert.Equal(Plaintext, decompressed);
    }

    [Fact]
    public void CompressDecompress_Span_Roundtrip()
    {
        Zstd.Init();
        int maxCompressed = Zstd.GetMaxCompressedSize(Plaintext.Length);
        byte[] compressedBuffer = new byte[maxCompressed];
        byte[] decompressedBuffer = new byte[Plaintext.Length];

        int written = Zstd.Compress(Plaintext.AsSpan(), compressedBuffer, 3);
        int read = Zstd.Decompress(compressedBuffer.AsSpan(0, written), decompressedBuffer);

        Assert.Equal(Plaintext.Length, read);
        Assert.Equal(Plaintext, decompressedBuffer);
    }

    [Fact]
    public void CompressDecompress_Stream_Roundtrip()
    {
        Zstd.Init();
        byte[] compressedBuffer = new byte[Zstd.GetMaxCompressedSize(Plaintext.Length)];
        byte[] decompressedBuffer = new byte[Plaintext.Length];

        int compressedOffset = 0;

        using (var compressor = new ZstdCompressStream(3))
        {
            bool consumed;
            compressedOffset += compressor.Compress(Plaintext, compressedBuffer, out consumed);
            Assert.True(consumed);

            compressedOffset += compressor.Flush(compressedBuffer.AsSpan(compressedOffset));
            compressedOffset += compressor.Finish(compressedBuffer.AsSpan(compressedOffset));
        }

        int decompressedOffset = 0;
        using (var decompressor = new ZstdDecompressStream())
        {
            bool consumed;
            decompressedOffset += decompressor.Decompress(
                compressedBuffer.AsSpan(0, compressedOffset),
                decompressedBuffer,
                out consumed);
            Assert.True(consumed);
        }

        Assert.Equal(Plaintext.Length, decompressedOffset);
        Assert.Equal(Plaintext, decompressedBuffer);
    }

    [Fact]
    public void Compress_Stream_MultipleChunks()
    {
        var chunk1 = Encoding.UTF8.GetBytes("chunk-1-");
        var chunk2 = Encoding.UTF8.GetBytes("chunk-2-");
        var chunk3 = Encoding.UTF8.GetBytes("chunk-3");

        using var compressor = new ZstdCompressStream(3);
        using var compressedStream = new MemoryStream();

        Span<byte> compressBuffer = stackalloc byte[256];
        bool consumed;

        // Compress each chunk
        int written = compressor.Compress(chunk1, compressBuffer, out consumed);
        Assert.True(consumed);
        compressedStream.Write(compressBuffer[..written]);

        written = compressor.Compress(chunk2, compressBuffer, out consumed);
        Assert.True(consumed);
        compressedStream.Write(compressBuffer[..written]);

        written = compressor.Compress(chunk3, compressBuffer, out consumed);
        Assert.True(consumed);
        compressedStream.Write(compressBuffer[..written]);

        // Finish the stream
        written = compressor.Finish(compressBuffer);
        compressedStream.Write(compressBuffer[..written]);

        var compressedBytes = compressedStream.ToArray();
        var expectedOutput = "chunk-1-chunk-2-chunk-3";

        var decompressed = new byte[expectedOutput.Length];
        using var decompressor = new ZstdDecompressStream();

        written = decompressor.Decompress(compressedBytes, decompressed, out var decompressedConsumed);
        Assert.True(decompressedConsumed);

        string result = Encoding.UTF8.GetString(decompressed, 0, written);
        Assert.Equal(expectedOutput, result);
    }

    [Fact]
    public unsafe void Manual_CompressStream_YieldsValidZstdFrame()
    {
        Zstd.Init();

        byte[] input = Encoding.UTF8.GetBytes("chunk-1-chunk-2-chunk-3");
        byte[] output = new byte[256];
        byte[] tail = new byte[256];

        IntPtr stream = ZstdInterop.ZSTD_createCStream();
        Assert.NotEqual(IntPtr.Zero, stream);

        var init = ZstdInterop.ZSTD_initCStream(stream, 3);
        Assert.Equal(0u, ZstdInterop.IsError(init));

        int part1 = 0, part2 = 0;

        fixed (byte* inPtr = input)
        fixed (byte* outPtr = output)
        fixed (byte* tailPtr = tail)
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

            var compressResult = ZstdInterop.ZSTD_compressStream(stream, ref outBuf, ref inBuf);
            Assert.Equal(0u, ZstdInterop.IsError(compressResult));
            part1 = (int)outBuf.pos;

            var tailBuf = new ZstdOutBuffer
            {
                dst = (IntPtr)tailPtr,
                size = (nuint)tail.Length,
                pos = 0
            };

            var endResult = ZstdInterop.ZSTD_endStream(stream, ref tailBuf);
            Assert.Equal(0u, ZstdInterop.IsError(endResult));
            part2 = (int)tailBuf.pos;
        }

        ZstdInterop.ZSTD_freeCStream(stream);

        var compressed = new byte[part1 + part2];
        Array.Copy(output, 0, compressed, 0, part1);
        Array.Copy(tail, 0, compressed, part1, part2);

        Console.WriteLine("Compressed: " + BitConverter.ToString(compressed));

        var decompressed = Zstd.Decompress(compressed, 128);
        var result = Encoding.UTF8.GetString(decompressed).TrimEnd('\0');
        Assert.Equal("chunk-1-chunk-2-chunk-3", result);
    }

    [Fact]
    public void ByteExampleWorks()
    {
        Zstd.Init();

        byte[] input = Encoding.UTF8.GetBytes("some highly compressible text...");
        int level = 3;

        byte[] compressed = Zstd.Compress(input, level);
        byte[] decompressed = Zstd.Decompress(compressed, input.Length);

        string result = Encoding.UTF8.GetString(decompressed);
        Assert.Equal("some highly compressible text...", result);
    }

    [Fact]
    public void SpanExampleWorks()
    {
        Zstd.Init();

        ReadOnlySpan<byte> input = "some highly compressible text..."u8;
        Span<byte> compressed = new byte[Zstd.GetMaxCompressedSize(input.Length)];
        Span<byte> decompressed = new byte[input.Length];

        int written = Zstd.Compress(input, compressed, 3);
        int read = Zstd.Decompress(compressed[..written], decompressed);

        string result = Encoding.UTF8.GetString(decompressed[..read]);
        Assert.Equal("some highly compressible text...", result);
    }

    [Fact]
    public void StreamingExampleWorks()
    {
        Zstd.Init();

        var chunks = new[]
        {
    Encoding.UTF8.GetBytes("chunk-1-"),
    Encoding.UTF8.GetBytes("chunk-2-"),
    Encoding.UTF8.GetBytes("chunk-3")
};

        using var compressor = new ZstdCompressStream(3);
        using var compressedStream = new MemoryStream();

        Span<byte> buffer = stackalloc byte[256];

        foreach (var chunk in chunks)
        {
            bool consumed;
            int written = compressor.Compress(chunk, buffer, out consumed);
            if (!consumed) throw new Exception("Chunk not fully consumed");
            compressedStream.Write(buffer[..written]);
        }

        // Finish the stream
        int final = compressor.Finish(buffer);
        compressedStream.Write(buffer[..final]);

        byte[] compressed = compressedStream.ToArray();

        // Decompress
        using var decompressor = new ZstdDecompressStream();
        Span<byte> output = new byte[64]; // max expected
        bool consumedFinal;
        int outputWritten = decompressor.Decompress(compressed, output, out consumedFinal);

        string result = Encoding.UTF8.GetString(output[..outputWritten]);
        Assert.Equal("chunk-1-chunk-2-chunk-3", result);
    }
}