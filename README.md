# dotZstd

High-performance .NET wrapper for Zstandard compression using native libzstd.

Provides Span-friendly, zero-copy APIs for compression, decompression, and full streaming support.

Ships as a native library (Windows, Linux, macOS) with direct bindings for low-level interop.

Tests and a demo are included and available in the Github repo.

[![NuGet](https://img.shields.io/nuget/v/nebulae.dotZstd.svg)](https://www.nuget.org/packages/nebulae.dotZstd)

---

## Features

- Byte and Span APIs for compression and decompression.
- Streaming support for compression and decompression.
- Dictionary-based compression and decompression.
- Streaming dictionary support.
- Can use FastCover dictionary training.
- Additional features like checksum, multi-threading, and more.

- **Cross-platform**: Works on Windows, Linux, and macOS (x64 & Apple Silicon).
- **High performance**: Optimized for speed, leveraging native SIMD-enabled code.
- **Easy to use**: Simple API for compression and decompression.
- **Secure**: Uses Meta's implementation, which is widely trusted in the industry.
- **Minimal dependencies**: No external dependencies required (all are included), making it lightweight and easy to integrate.

---

## Requirements

- .NET 8.0 or later
- AVX2 compatible CPU or Apple Silicon
- Windows x64, Linux x64, or macOS (x64 & Apple Silicon)

## Usage

Using the byte API:

```csharp

using nebulae.dotZstd;

byte[] input = Encoding.UTF8.GetBytes("some highly compressible text...");
int level = 3;

byte[] compressed = Zstd.Compress(input, level);
byte[] decompressed = Zstd.Decompress(compressed, input.Length);

string result = Encoding.UTF8.GetString(decompressed);
Console.WriteLine(result); // prints original text

```

Using the Span API:

```csharp

using nebulae.dotZstd;

ReadOnlySpan<byte> input = "some highly compressible text..."u8;
Span<byte> compressed = new byte[Zstd.GetMaxCompressedSize(input.Length)];
Span<byte> decompressed = new byte[input.Length];

int written = Zstd.Compress(input, compressed, 3);
int read = Zstd.Decompress(compressed[..written], decompressed);

string result = Encoding.UTF8.GetString(decompressed[..read]);
Console.WriteLine(result);

```

Streaming Compression:

```csharp

using nebulae.dotZstd;

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
Console.WriteLine(result); // chunk-1-chunk-2-chunk-3

```

Compression with Dictionary:

```csharp

using nebulae.dotZstd;

// Dictionary source data (could be trained)
var dict = Encoding.UTF8.GetBytes("chunk-1-chunk-2-chunk-3");

// Data to compress
var input = Encoding.UTF8.GetBytes("chunk-1-chunk-2-chunk-3");

// Allocate output buffers
var compressed = new byte[Zstd.GetMaxCompressedSize(input.Length)];
var decompressed = new byte[input.Length];

// Create compression and decompression dictionary wrappers
using var cdict = new ZstdCompressionDictionary(dict, compressionLevel: 3);
using var ddict = new ZstdDecompressionDictionary(dict);

// Compress using dictionary
int compressedSize = Zstd.CompressWithDict(input, compressed, cdict);

// Decompress using dictionary
int decompressedSize = Zstd.DecompressWithDict(compressed.AsSpan(0, compressedSize), decompressed, ddict);

// Get the original string back
string result = Encoding.UTF8.GetString(decompressed.AsSpan(0, decompressedSize));
Console.WriteLine(result); // "chunk-1-chunk-2-chunk-3"

```

Streaming Compression with Dictionary:

```csharp

using nebulae.dotZstd;

// Dictionary source data
var dict = Encoding.UTF8.GetBytes("chunk-1-chunk-2-chunk-3");
var input = Encoding.UTF8.GetBytes("chunk-1-chunk-2-chunk-3");

// Wrap dictionaries
using var cdict = new ZstdCompressionDictionary(dict, 3);
using var ddict = new ZstdDecompressionDictionary(dict);

// Set up streaming compression
using var compressor = new ZstdCompressStream(cdict);
using var compressedStream = new MemoryStream();
Span<byte> buffer = stackalloc byte[256];

// Compress input
int written = compressor.Compress(input, buffer, out var consumed);
compressedStream.Write(buffer[..written]);

// Finish stream and collect remaining output
written = compressor.Finish(buffer);
compressedStream.Write(buffer[..written]);

byte[] compressed = compressedStream.ToArray();

// Decompress stream
using var decompressor = new ZstdDecompressStream(ddict);
byte[] output = new byte[input.Length];
written = decompressor.Decompress(compressed, output, out var fullyConsumed);

string result = Encoding.UTF8.GetString(output, 0, written);
Console.WriteLine(result); // "chunk-1-chunk-2-chunk-3"

```

Training a Dictionary:

```csharp

using nebulae.dotZstd;
using System.Text;

// 1) Prepare your samples (each item is one sample buffer)
var samples = new ReadOnlyMemory<byte>[]
{
    Encoding.UTF8.GetBytes("GET /api/orders/12345?expand=items&currency=USD\n"),
    Encoding.UTF8.GetBytes("GET /api/orders/12346?expand=items&currency=USD\n"),
    Encoding.UTF8.GetBytes("GET /api/customers/42?include=orders\n"),
    Encoding.UTF8.GetBytes("inventory: sku=ABC-123 qty=100 loc=us-east-1\n"),
    // ... add more samples from your real traffic or files
};

// 2) Pick a dictionary size. A solid rule of thumb: ~1% of total sample bytes.
//    Keep it within 8-131 KiB for most workloads.
int total = samples.Sum(s => s.Length);
int dictCapacity = Math.Clamp(total / 100, 8 * 1024, 128 * 1024);

// 3) Train
byte[] dict = ZstdDictTrainer.Train(samples, (nuint)dictCapacity);

// 4) Check it has a dictID (non-zero means "looks valid")
uint dictId = Zstd.GetDictId(dict);
Console.WriteLine($"dict size={dict.Length} bytes, id={dictId}");

```
Using created dictionary (one shot):

```csharp

var payload = Encoding.UTF8.GetBytes("GET /api/orders/12345?expand=items&currency=USD\n");

using var cdict = new ZstdCompressionDictionary(dict, compressionLevel: 3);
using var ddict = new ZstdDecompressionDictionary(dict);

var compressed = new byte[Zstd.GetMaxCompressedSize(payload.Length)];
int csize = Zstd.CompressWithDict(payload, compressed, cdict);

var decompressed = new byte[payload.Length];
int dsize = Zstd.DecompressWithDict(compressed.AsSpan(0, csize), decompressed, ddict);

Console.WriteLine(Encoding.UTF8.GetString(decompressed.AsSpan(0, dsize)));

```

Using the created dictionary (streaming):
```csharp

using var cdict = new ZstdCompressionDictionary(dict, 3);
using var ddict = new ZstdDecompressionDictionary(dict);

using var cstream = new ZstdCompressStream(cdict);
using var dstream = new ZstdDecompressStream(ddict);

var input = Encoding.UTF8.GetBytes("chunk-1-chunk-2-chunk-3");
Span<byte> buf = stackalloc byte[256];

// compress
bool consumed;
int written = cstream.Compress(input, buf, out consumed);
using var ms = new MemoryStream();
ms.Write(buf[..written]);
written = cstream.Finish(buf);
ms.Write(buf[..written]);

// decompress
var outBuf = new byte[input.Length];
bool allUsed;
int outWritten = dstream.Decompress(ms.ToArray(), outBuf, out allUsed);

```

FastCover dictionary training:

```csharp

using nebulae.dotZstd;
using System.Text;

var samples = new ReadOnlyMemory<byte>[]
{
    Encoding.UTF8.GetBytes("GET /api/orders/12345?expand=items&currency=USD\n"),
    Encoding.UTF8.GetBytes("GET /api/orders/12346?expand=items&currency=USD\n"),
    Encoding.UTF8.GetBytes("GET /api/customers/42?include=orders\n"),
    Encoding.UTF8.GetBytes("inventory: sku=ABC-123 qty=100 loc=us-east-1\n"),
    // ... add more samples from your real traffic or files
};

// Choose capacity (often 16-64 KiB for small/med corpora)
int dictCapacity = 32 * 1024;

var fastOpts = new ZstdFastCoverOptions(
    DictCapacity: dictCapacity,
    K: 200,         // segment size
    D: 8,           // dmer size
    Steps: 4,
    NbThreads: 0,   // 0 = single-thread (deterministic)
    SplitPoint: 75, // % of samples used for training
    Accel: 1,       // >=1
    ShrinkDict: true);

byte[] seedDict = ZstdDictTrainer.TrainFastCover(samples, fastOpts);
Console.WriteLine($"fastCover seed size={seedDict.Length}, id={Zstd.GetDictId(seedDict)}");

```

---

## Installation

You can install the package via NuGet:

```bash

$ dotnet add package nebulae.dotZstd

```

Or via git:

```bash

$ git clone https://github.com/nebulaeonline/dotZstd.git
$ cd dotZstd
$ dotnet build

```

---

## License

MIT

## Roadmap

Unless there are vulnerabilities found, there are no plans to add any new features.