using System.Text;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Ocr;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests.Ocr;

/// <summary>
/// Unit tests for <see cref="HeaderOcrResultCache"/> — the content-hash-keyed OCR cache,
/// exercised entirely in isolation with no real Tesseract call (design doc
/// <c>veriqan-e7-s72-header-ocr-design-2026-07-08.md</c> §5 step 1).
/// </summary>
public sealed class HeaderOcrResultCacheTests
{
    private static byte[] Bytes(string s) => Encoding.UTF8.GetBytes(s);

    [Fact]
    public void TryGet_EmptyCache_ReturnsFalse()
    {
        var cache = new HeaderOcrResultCache();

        var found = cache.TryGet(Bytes("crop-a"), out var text);

        found.ShouldBeFalse();
        text.ShouldBeNull();
    }

    [Fact]
    public void GetOrCompute_FirstCall_InvokesComputeAndCaches()
    {
        var cache = new HeaderOcrResultCache();
        var callCount = 0;

        var result = cache.GetOrCompute(Bytes("crop-a"), () =>
        {
            callCount++;
            return "Tarjeta de Crédito COSTCO BANAMEX";
        });

        result.ShouldBe("Tarjeta de Crédito COSTCO BANAMEX");
        callCount.ShouldBe(1);
        cache.Count.ShouldBe(1);
    }

    [Fact]
    public void GetOrCompute_SameBytesTwice_ComputeInvokedOnlyOnce()
    {
        var cache = new HeaderOcrResultCache();
        var callCount = 0;
        var cropBytes = Bytes("shared-header-crop");

        var first = cache.GetOrCompute(cropBytes, () => { callCount++; return "OCR TEXT"; });
        var second = cache.GetOrCompute(cropBytes, () => { callCount++; return "SHOULD NOT WIN"; });

        first.ShouldBe("OCR TEXT");
        second.ShouldBe("OCR TEXT");
        callCount.ShouldBe(1, "The compute delegate must run at most once per distinct crop-hash key.");
        cache.Count.ShouldBe(1);
    }

    [Fact]
    public void GetOrCompute_DifferentBytes_ComputeInvokedForEach()
    {
        var cache = new HeaderOcrResultCache();
        var callCount = 0;

        cache.GetOrCompute(Bytes("crop-a"), () => { callCount++; return "TEXT-A"; });
        cache.GetOrCompute(Bytes("crop-b"), () => { callCount++; return "TEXT-B"; });

        callCount.ShouldBe(2);
        cache.Count.ShouldBe(2);
    }

    [Fact]
    public void GetOrCompute_ThenTryGet_ReturnsCachedValue()
    {
        var cache = new HeaderOcrResultCache();
        var cropBytes = Bytes("crop-a");

        cache.GetOrCompute(cropBytes, () => "TEXT-A");
        var found = cache.TryGet(cropBytes, out var text);

        found.ShouldBeTrue();
        text.ShouldBe("TEXT-A");
    }

    [Fact]
    public void ComputeKey_SameBytes_ProducesSameKey()
    {
        var a = HeaderOcrResultCache.ComputeKey(Bytes("identical-content"));
        var b = HeaderOcrResultCache.ComputeKey(Bytes("identical-content"));

        a.ShouldBe(b);
    }

    [Fact]
    public void ComputeKey_DifferentBytes_ProducesDifferentKeys()
    {
        var a = HeaderOcrResultCache.ComputeKey(Bytes("content-one"));
        var b = HeaderOcrResultCache.ComputeKey(Bytes("content-two"));

        a.ShouldNotBe(b);
    }

    [Fact]
    public void Cache_ExceedsMaxEntries_EvictsDownToBound()
    {
        var cache = new HeaderOcrResultCache();

        for (var i = 0; i < HeaderOcrResultCache.MaxEntries + 10; i++)
        {
            var index = i;
            cache.GetOrCompute(Bytes($"crop-{index}"), () => $"TEXT-{index}");
        }

        cache.Count.ShouldBeLessThanOrEqualTo(
            HeaderOcrResultCache.MaxEntries,
            "The cache must bound its size once MaxEntries is exceeded (a process-lifetime " +
            "singleton must not grow without limit).");
    }
}
