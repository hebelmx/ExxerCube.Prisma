using System.Text.Json;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Tests;

/// <summary>
/// A single row of the standing synthetic corpus index (<c>corpus-manifest.json</c>,
/// E6.S6.2.6). <see cref="Id"/>/<see cref="Pdf"/>/<see cref="Manifest"/> are the hard contract
/// the golden round-trip tests consume; the index's other documentation fields
/// (<c>profile</c>/<c>slice</c>/<c>defect</c>/<c>description</c>) are intentionally not
/// modeled here — nothing in this test project asserts on them.
/// </summary>
internal sealed record SyntheticCorpusSpecimen(string Id, string Pdf, string Manifest);

/// <summary>
/// Loads the standing synthetic corpus index (<c>corpus-manifest.json</c>) that lists every
/// specimen (a <c>*.pdf</c> + sibling <c>*.manifest.json</c> pair) in
/// <c>Prisma/Fixtures/PRP2/synthetic/</c>. This index — not a hardcoded per-fixture test
/// method — is the single source of truth for "which specimens exist": adding a fixture to
/// the standing corpus is an index-row addition, not a new test method.
/// </summary>
internal static class SyntheticCorpusIndexLoader
{
    /// <summary>
    /// Reads and parses <c>corpus-manifest.json</c> from <paramref name="fixturesDir"/>.
    /// </summary>
    /// <remarks>
    /// This is test-collection-time infrastructure (an xUnit <c>[MemberData]</c> source), not
    /// production code, so it deliberately propagates exceptions on a malformed/missing index
    /// rather than wrapping them in a <c>Result&lt;T&gt;</c> — a broken index must fail test
    /// discovery loudly, not be silently swallowed.
    /// </remarks>
    /// <param name="fixturesDir">
    /// Absolute path to the synthetic fixtures directory (e.g. the test output's
    /// <c>Fixtures/synthetic</c>) containing <c>corpus-manifest.json</c>.
    /// </param>
    public static IReadOnlyList<SyntheticCorpusSpecimen> Load(string fixturesDir)
    {
        var indexPath = Path.Combine(fixturesDir, "corpus-manifest.json");
        using var stream = File.OpenRead(indexPath);
        using var doc = JsonDocument.Parse(stream);

        var specimens = new List<SyntheticCorpusSpecimen>();
        foreach (var specimenEl in doc.RootElement.GetProperty("specimens").EnumerateArray())
        {
            specimens.Add(new SyntheticCorpusSpecimen(
                specimenEl.GetProperty("id").GetString() ?? string.Empty,
                specimenEl.GetProperty("pdf").GetString() ?? string.Empty,
                specimenEl.GetProperty("manifest").GetString() ?? string.Empty));
        }

        return specimens;
    }
}
