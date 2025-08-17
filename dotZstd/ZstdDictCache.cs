using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nebulae.dotZstd;

public sealed class ZstdDictCache : IDisposable
{
    private readonly Dictionary<uint, ZstdDecompressionDictionary> _map = new();
    public void Add(byte[] dict) { uint id = Zstd.GetDictId(dict); if (id == 0) throw new ArgumentException("No dictID"); _map[id] = new ZstdDecompressionDictionary(dict); }
    public bool TryGet(uint id, out ZstdDecompressionDictionary dd) => _map.TryGetValue(id, out dd!);
    public void Dispose() { foreach (var d in _map.Values) d.Dispose(); _map.Clear(); }
}
