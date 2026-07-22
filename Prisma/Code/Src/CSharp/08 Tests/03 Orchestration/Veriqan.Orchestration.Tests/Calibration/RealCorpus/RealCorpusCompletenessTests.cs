namespace ExxerCube.Prisma.Veriqan.Orchestration.Tests.Calibration.RealCorpus;

/// <summary>
/// RC1.S1 — loud-loader completeness gate for the real-corpus index: asserts
/// <c>corpus-index.json</c> lists exactly the 16 expected specimens (3 accounts × 4 consecutive
/// months = 12, plus 4 defect specimens). Paired with
/// <see cref="RealCorpusPiiLeakGateTests"/>'s index-driven <c>[Theory]</c> so that an index that
/// silently shrank (e.g. a build_corpus_index.py run over a partially-populated staging tree)
/// cannot pass unnoticed as "all rows green" — this fact independently pins the row count.
/// </summary>
/// <remarks>
/// Gracefully skips when the corpus root/index is absent (CI has no corpus staged), mirroring
/// <see cref="RealCorpusPiiLeakGateTests"/> and the existing
/// <see cref="ExxerCube.Prisma.Veriqan.Orchestration.Tests.Calibration.CalibrationDriverTests"/>
/// skip pattern.
/// </remarks>
[Trait("Category", "RealCorpus")]
public sealed class RealCorpusCompletenessTests
{
    [Fact]
    public void RealCorpusIndex_Loaded_HasExactlySixteenEntries()
    {
        var fixture = RealCorpusFixtureLocator.TryLoad();
        if (fixture is null)
        {
            Assert.Skip(
                $"Real corpus not staged locally — set {RealCorpusFixtureLocator.RootEnvVar} to the " +
                "staging root containing corpus-index.json. Expected in CI (no corpus shipped).");
            return;
        }

        fixture.Entries.Count.ShouldBe(16,
            $"corpus-index.json at '{fixture.RootDir}' must list exactly 16 entries " +
            "(3 accounts x 4 months + 4 defect specimens); found " +
            $"{fixture.Entries.Count}. Re-run build_corpus_index.py against the full staging tree.");

        // Defense-in-depth: also prove every indexed relative path actually resolves on disk, and
        // every id is unique — a duplicated id or a dangling relativePath would otherwise let the
        // count-only assertion above pass on a corrupted index.
        var problems = new List<string>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in fixture.Entries)
        {
            if (!seenIds.Add(entry.Id))
                problems.Add($"duplicate id '{entry.Id}'");

            var path = Path.Combine(fixture.RootDir, entry.RelativePath);
            if (!File.Exists(path))
                problems.Add($"entry '{entry.Id}' points at missing file: {entry.RelativePath}");
        }

        problems.ShouldBeEmpty(
            $"corpus-index.json integrity problems:\n{string.Join("\n", problems)}");
    }
}
