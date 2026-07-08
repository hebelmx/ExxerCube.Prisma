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
        return _cache.GetOrAdd(key, static (_, factory) => factory(), compute);
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
