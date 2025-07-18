using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nebulae.dotZstd;

public sealed class ZstdCompressionDictionary : IDisposable
{
    internal IntPtr Handle { get; }

    public ZstdCompressionDictionary(ReadOnlySpan<byte> dictionaryData, int compressionLevel)
    {
        if (dictionaryData.IsEmpty)
            throw new ArgumentException("Dictionary must not be empty", nameof(dictionaryData));

        unsafe
        {
            fixed (byte* dictPtr = dictionaryData)
            {
                Handle = ZstdInterop.ZSTD_createCDict((IntPtr)dictPtr, (nuint)dictionaryData.Length, compressionLevel);
                if (Handle == IntPtr.Zero)
                    throw new InvalidOperationException("Failed to create compression dictionary");
            }
        }
    }

    public void Dispose()
    {
        if (Handle != IntPtr.Zero)
            ZstdInterop.ZSTD_freeCDict(Handle);
    }
}
