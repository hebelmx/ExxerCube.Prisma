using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Ocr;

/// <summary>
/// Pure, in-memory content-hash-keyed cache for header-crop OCR text results.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately factored out of <see cref="TesseractHeaderProductOcrEngine"/> so the caching
/// behaviour can be unit-tested in isolation without invoking real Tesseract (design doc
/// <c>veriqan-e7-s72-header-ocr-design-2026-07-08.md</c> §3.3 / §5 step 1: "Unit-test the
/// crop-hash cache in isolation (no real Tesseract)").
/// </para>
/// <para>
/// The key is a SHA-256 hash of the rendered crop's PNG bytes — identical crops (the shared
/// demo header, rendered at the same DPI, produces byte-identical PNGs across the 4 non-scanned
/// demo fixtures) hit the same cache entry, so the serialized Tesseract engine is invoked at
/// most once per distinct header image rather than once per document.
/// </para>
/// </remarks>
internal sealed class HeaderOcrResultCache
{
    /// <summary>
    /// Hard cap on the number of distinct crop-hash entries retained. The cache is held by a
    /// process-lifetime singleton, so without a bound a Worker processing many distinct statements
    /// over its lifetime would grow it without limit. The demo shares 1 crop across 4 fixtures;
    /// 256 bounds unbounded growth in a long-lived Worker while keeping realistic hit rates.
    /// </summary>
    public const int MaxEntries = 256;

    private readonly ConcurrentDictionary<string, string> _cache = new(StringComparer.Ordinal);

    /// <summary>Number of distinct crop hashes currently cached. Exposed for tests only.</summary>
    public int Count => _cache.Count;

    /// <summary>
    /// Returns the cached OCR text for <paramref name="cropBytes"/> when present.
    /// </summary>
    public bool TryGet(byte[] cropBytes, out string? text) =>
        _cache.TryGetValue(ComputeKey(cropBytes), out text);

    /// <summary>
    /// Returns the cached OCR text for <paramref name="cropBytes"/>, computing and caching it via
    /// <paramref name="compute"/> on a cache miss. <paramref name="compute"/> is never invoked on
    /// a cache hit.
    /// </summary>
    public string GetOrCompute(byte[] cropBytes, Func<string> compute)
    {
        ArgumentNullException.ThrowIfNull(cropBytes);
        ArgumentNullException.ThrowIfNull(compute);

        var key = ComputeKey(cropBytes);
        var value = _cache.GetOrAdd(key, static (_, factory) => factory(), compute);

        EvictIfOverCapacity();

        return value;
    }

    /// <summary>
    /// If the cache has grown past <see cref="MaxEntries"/>, removes entries (via a
    /// <see cref="ConcurrentDictionary{TKey,TValue}.Keys"/> snapshot) until it is back at/below the
    /// cap. <see cref="ConcurrentDictionary{TKey,TValue}"/> has no built-in LRU ordering, so eviction
    /// here is approximate and non-LRU by design — the goal is bounding unbounded growth in a
    /// long-lived process-scoped Worker singleton, not guaranteeing any particular retention policy.
    /// Thread-safe: each removal is an independent <see cref="ConcurrentDictionary{TKey,TValue}.TryRemove(TKey,out TValue)"/>;
    /// a benign race between concurrent callers can only ever evict slightly more than strictly necessary.
    /// </summary>
    private void EvictIfOverCapacity()
    {
        if (_cache.Count <= MaxEntries)
            return;

        foreach (var key in _cache.Keys)
        {
            if (_cache.Count <= MaxEntries)
                break;

            _cache.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Computes the stable content-hash key for a crop's raw bytes (SHA-256, hex-encoded).
    /// Internal (not private) so cache-key stability can be asserted directly by unit tests.
    /// </summary>
    internal static string ComputeKey(byte[] cropBytes)
    {
        ArgumentNullException.ThrowIfNull(cropBytes);
        return Convert.ToHexString(SHA256.HashData(cropBytes));
    }
}
