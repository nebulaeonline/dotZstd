using System;
using System.IO;
using System.Linq;
using System.Text;
using nebulae.dotZstd;

internal static class Program
{
    public static void Main()
    {
        Console.WriteLine("== dotZstd demo ==");

        BasicOneShot();
        StreamingExample();
        ParameterTuningExample();
        BasicDictionaryTrainingExample();
        FastCoverAndFinalizeExample();

        Console.WriteLine("\nAll examples completed.");
    }

    // ------------- 1) One-shot compress/decompress -------------
    private static void BasicOneShot()
    {
        Console.WriteLine("\n[1] One-shot");

        byte[] input = Encoding.UTF8.GetBytes("some highly compressible text...");
        int level = 3;

        byte[] compressed = Zstd.Compress(input, level);
        byte[] decompressed = Zstd.Decompress(compressed, input.Length);

        Console.WriteLine($" input: {input.Length} B, compressed: {compressed.Length} B");
        Console.WriteLine($" round-trip ok: {Encoding.UTF8.GetString(decompressed) == Encoding.UTF8.GetString(input)}");
    }

    // ------------- 2) Streaming compress/decompress -------------
    private static void StreamingExample()
    {
        Console.WriteLine("\n[2] Streaming");

        byte[] chunk1 = Encoding.UTF8.GetBytes("chunk-1-");
        byte[] chunk2 = Encoding.UTF8.GetBytes("chunk-2-");
        byte[] chunk3 = Encoding.UTF8.GetBytes("chunk-3");
        string expected = "chunk-1-chunk-2-chunk-3";

        byte[] outBuf = new byte[Zstd.RecommendedCStreamOutSize()];
        using var c = new ZstdCompressStream(compressionLevel: 3);
        using var ms = new MemoryStream();

        bool consumed;
        int w = c.Compress(chunk1, outBuf, out consumed); ms.Write(outBuf, 0, w);
        w = c.Compress(chunk2, outBuf, out consumed); ms.Write(outBuf, 0, w);
        w = c.Compress(chunk3, outBuf, out consumed); ms.Write(outBuf, 0, w);
        w = c.Finish(outBuf); ms.Write(outBuf, 0, w);

        byte[] compressed = ms.ToArray();

        using var d = new ZstdDecompressStream();
        byte[] plain = new byte[expected.Length];
        int read = d.Decompress(compressed, plain, out var allUsed);
        string result = Encoding.UTF8.GetString(plain, 0, read);

        Console.WriteLine($" compressed: {compressed.Length} B (stream)");
        Console.WriteLine($" round-trip ok: {result == expected}, consumed all input: {allUsed}");
    }

    // ------------- 3) Tuning with parameters -------------
    private static void ParameterTuningExample()
    {
        Console.WriteLine("\n[3] Parameters (workers, checksum, LDM)");

        // Build a repetitive input so LDM has something to latch onto
        byte[] input = Encoding.UTF8.GetBytes(
            new string('A', 4096) + new string('B', 4096) + new string('C', 4096));

        // Output buffer sized by zstd's recommended stream size
        byte[] outBuf = new byte[Zstd.RecommendedCStreamOutSize()];
        using var compressor = new ZstdCompressStream(compressionLevel: 5)
            // If your libzstd was built with multithreading, this enables parallel compression.
            // If not, your helper can no-op or you can comment this out.
            .WithWorkers(Math.Max(1, Environment.ProcessorCount / 2))
            .ToggleChecksum()               // add a frame checksum
            .ToggleLongDistanceMatching();  // better on large repetitive data

        // Compress in one go for simplicity (still uses the streaming API)
        using var blob = new MemoryStream();
        bool consumed;
        int w = compressor.Compress(input, outBuf, out consumed);
        if (w > 0) blob.Write(outBuf, 0, w);
        w = compressor.Finish(outBuf);
        if (w > 0) blob.Write(outBuf, 0, w);

        byte[] tunedCompressed = blob.ToArray();

        // Decompress with default settings (no special decoder params needed)
        using var decompressor = new ZstdDecompressStream();
        byte[] plain = new byte[input.Length];
        int read = decompressor.Decompress(tunedCompressed, plain, out var allUsed);

        Console.WriteLine($" compressed (tuned): {tunedCompressed.Length} B");
        Console.WriteLine($" round-trip ok: {allUsed && read == input.Length && ByteSeqEq(input, plain)}");
    }

    // ------------- 4) Dictionary training (basic) + use -------------
    private static void BasicDictionaryTrainingExample()
    {
        Console.WriteLine("\n[4] Dictionary training (basic)");

        var samples = BuildSampleCorpus();
        int total = samples.Sum(s => s.Length);
        int dictCap = ChooseDictCapacity(total); // ~1% clamped

        byte[] dict = ZstdDictTrainer.Train(samples, (nuint)dictCap);
        uint dictId = Zstd.GetDictId(dict);

        Console.WriteLine($" trained dict: {dict.Length} B, id={dictId}");

        // Use the dictionary for one-shot compression
        byte[] payload = Encoding.UTF8.GetBytes("GET /api/orders/12345?expand=items&currency=USD\n");
        int max = Zstd.GetMaxCompressedSize(payload.Length);

        using var cdict = new ZstdCompressionDictionary(dict, compressionLevel: 3);
        byte[] compWithDict = new byte[max];
        int sizeWithDict = Zstd.CompressWithDict(payload, compWithDict, cdict);

        byte[] compNoDict = new byte[max];
        int sizeNoDict = Zstd.Compress(payload, compNoDict, 3);

        using var ddict = new ZstdDecompressionDictionary(dict);
        byte[] round = new byte[payload.Length];
        int outLen = Zstd.DecompressWithDict(compWithDict.AsSpan(0, sizeWithDict), round, ddict);

        Console.WriteLine($" with dict: {sizeWithDict} B, no dict: {sizeNoDict} B, round-trip ok: {ByteSeqEq(payload, round.AsSpan(0, outLen).ToArray())}");
    }

    // ------------- 5) FastCover training + finalize + pick best -------------
    private static void FastCoverAndFinalizeExample()
    {
        Console.WriteLine("\n[5] FastCover + Finalize + pick best");

        var samples = BuildSampleCorpus();
        byte[] payload = Encoding.UTF8.GetBytes(
            "GET /api/orders/12345?expand=items&currency=USD\n" +
            "GET /api/orders/12346?expand=items&currency=USD\n" +
            "GET /api/customers/42?include=orders\n" +
            "inventory: sku=ABC-123 qty=100 loc=us-east-1\n");

        // 5a) Train a seed dict (FastCover)
        var fast = new ZstdFastCoverOptions(
            DictCapacity: 16 * 1024,
            K: 200, D: 8, Steps: 4, NbThreads: 0, SplitPoint: 75, Accel: 1, ShrinkDict: true);

        byte[] seed = ZstdDictTrainer.TrainFastCover(samples, fast);
        Console.WriteLine($" seed: {seed.Length} B, id={Zstd.GetDictId(seed)}");

        // 5b) Optionally finalize (match capacity and runtime level for small corpora)
        var fin = new ZstdFinalizeOptions(
            DictCapacity: seed.Length,
            CompressionLevel: 3,
            NotificationLevel: 0,
            DictID: ZstdDictTrainer.GetDictId(seed));

        byte[] finalDict = ZstdDictTrainer.FinalizeDictionary(seed, samples, fin);
        Console.WriteLine($" final: {finalDict.Length} B, id={Zstd.GetDictId(finalDict)}");

        // 5c) Measure seed vs final; keep the smaller on a representative payload
        int sizeSeed = SizeWithDict(payload, seed, 3);
        int sizeFinal = SizeWithDict(payload, finalDict, 3);
        byte[] best = sizeFinal <= sizeSeed ? finalDict : seed;
        int bestSize = Math.Min(sizeFinal, sizeSeed);

        // sanity: dict should beat no-dict
        int max = Zstd.GetMaxCompressedSize(payload.Length);
        byte[] tmp = new byte[max];
        int noDict = Zstd.Compress(payload, tmp, 3);

        Console.WriteLine($" sizes -> seed:{sizeSeed} B final:{sizeFinal} B no-dict:{noDict} B (best:{bestSize} B)");

        // 5d) Round-trip with the chosen best dict
        using var cdictBest = new ZstdCompressionDictionary(best, 3);
        byte[] bestBuf = new byte[max];
        int bestCompressed = Zstd.CompressWithDict(payload, bestBuf, cdictBest);

        using var ddictBest = new ZstdDecompressionDictionary(best);
        byte[] round = new byte[payload.Length];
        int outLen = Zstd.DecompressWithDict(bestBuf.AsSpan(0, bestCompressed), round, ddictBest);

        Console.WriteLine($" best round-trip ok: {ByteSeqEq(payload, round.AsSpan(0, outLen).ToArray())}");
    }

    // -------------------- helpers used by the examples --------------------
    private static ReadOnlyMemory<byte>[] BuildSampleCorpus()
    {
        return new[]
        {
            "GET /api/orders/12345?expand=items&currency=USD\n",
            "GET /api/orders/12346?expand=items&currency=USD\n",
            "GET /api/orders/12347?expand=items&currency=USD\n",
            "POST /api/orders {\"customerId\":42,\"currency\":\"USD\",\"items\":[1,2,3]}\n",
            "PATCH /api/orders/12345 {\"status\":\"confirmed\"}\n",
            "GET /api/customers/42?include=orders\n",
            "GET /static/config?region=us-east-1&rev=abcdef\n",
            "inventory: sku=ABC-123 qty=100 loc=us-east-1\n",
            "inventory: sku=ABC-124 qty=95  loc=us-east-1\n",
            "inventory: sku=ABC-125 qty=87  loc=us-east-1\n",
        }.Select(s => (ReadOnlyMemory<byte>)Encoding.UTF8.GetBytes(s)).ToArray();
    }

    private static int ChooseDictCapacity(int totalSampleBytes)
    {
        // Rule of thumb: ~1% of total sample bytes, clamped to [8 KiB, 128 KiB]
        return Math.Clamp(totalSampleBytes / 100, 8 * 1024, 128 * 1024);
    }

    private static bool ByteSeqEq(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }

    private static int SizeWithDict(byte[] payload, byte[] dict, int clevel)
    {
        using var cdict = new ZstdCompressionDictionary(dict, clevel);
        byte[] buf = new byte[Zstd.GetMaxCompressedSize(payload.Length)];
        return Zstd.CompressWithDict(payload, buf, cdict);
    }
}