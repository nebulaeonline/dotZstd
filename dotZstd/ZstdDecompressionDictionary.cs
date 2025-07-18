using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nebulae.dotZstd;

public sealed class ZstdDecompressionDictionary : IDisposable
{
    internal IntPtr Handle { get; }

    public ZstdDecompressionDictionary(ReadOnlySpan<byte> dictionaryData)
    {
        if (dictionaryData.IsEmpty)
            throw new ArgumentException("Dictionary must not be empty", nameof(dictionaryData));

        unsafe
        {
            fixed (byte* dictPtr = dictionaryData)
            {
                Handle = ZstdInterop.ZSTD_createDDict((IntPtr)dictPtr, (nuint)dictionaryData.Length);
                if (Handle == IntPtr.Zero)
                    throw new InvalidOperationException("Failed to create decompression dictionary");
            }
        }
    }

    public void Dispose()
    {
        if (Handle != IntPtr.Zero)
            ZstdInterop.ZSTD_freeDDict(Handle);
    }
}
