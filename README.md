# dotZstd

High-performance .NET wrapper for Zstandard compression using native libzstd.

Provides Span-friendly, zero-copy APIs for compression, decompression, and full streaming support.

Ships as a native library (Windows, Linux, macOS) with direct bindings for low-level interop.

Tests are included and available in the Github repo.

[![NuGet](https://img.shields.io/nuget/v/nebulae.dotZstd.svg)](https://www.nuget.org/packages/nebulae.dotZstd)

---

## Features

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

Zstd.Init();

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

Zstd.Init();

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
Console.WriteLine(result); // chunk-1-chunk-2-chunk-3

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