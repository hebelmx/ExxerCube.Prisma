using System.Text.Json;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Tests.Calibration.RealCorpus;

/// <summary>
/// RC1.S1 — loads every real PII token from <c>vec-anonymization-map.json</c> at test run time.
/// The map has shape <c>{ products: { "A": { real_to_fake: { "&lt;real&gt;": "&lt;fake&gt;" } }, ... } }</c>
/// (see <c>scripts/veriqan-corpus/anonymize.py</c>). This loader flattens every product's
/// <c>real_to_fake</c> keys into one deduplicated set — the tokens that must NEVER appear in any
/// staged PDF's text layer, regardless of which product folder the PDF lives in (some tokens,
/// e.g. the holder name, are shared across all three products' statements).
/// </summary>
/// <remarks>
/// This class only ever runs against a file living under <c>~/Downloads/</c> (out of repo, never
/// committed) — see <see cref="RealCorpusFixtureLocator"/>. It deliberately propagates a parse
/// exception on a malformed (but present) map file rather than swallowing it: a corrupt local map
/// is a real setup bug that must fail loudly, not silently skip the PII gate.
/// </remarks>
public static class RealCorpusPiiTokenLoader
{
    /// <summary>
    /// Reads <paramref name="mapPath"/> and returns the deduplicated set of every real token
    /// (the left-hand side of every product's <c>real_to_fake</c> entries). Tokens shorter than
    /// 3 characters are excluded — they are too short to scan for without false positives and
    /// none of the known PII categories (names, RFCs, card/account numbers, addresses) are that
    /// short.
    /// </summary>
    public static IReadOnlyList<string> LoadAllRealTokens(string mapPath)
    {
        using var stream = File.OpenRead(mapPath);
        using var doc = JsonDocument.Parse(stream);

        var tokens = new HashSet<string>(StringComparer.Ordinal);

        if (doc.RootElement.TryGetProperty("products", out var products))
        {
            foreach (var product in products.EnumerateObject())
            {
                if (!product.Value.TryGetProperty("real_to_fake", out var realToFake))
                    continue;

                foreach (var mapping in realToFake.EnumerateObject())
                {
                    var realToken = mapping.Name;
                    if (realToken.Length >= 3)
                        tokens.Add(realToken);
                }
            }
        }

        return tokens.ToList();
    }
}
