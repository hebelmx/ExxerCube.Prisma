using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Orchestration.Tests.Calibration;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Tests.Calibration;

/// <summary>
/// §5.3 — Driver [Fact] for the corpus calibration harness.
/// </summary>
/// <remarks>
/// <para>
/// Cardinal-rule regression guard: every KnownGood / KnownSynthetic specimen must have
/// ZERO NewFails.  Any new Fail that is not in AllowedFails ∪ KnownFixtureDefects is a
/// false-Fail and constitutes a regression that blocks shipping.
/// </para>
/// <para>
/// Gracefully skips (via <c>Assert.Skip</c>) when the corpus directory or manifest is
/// absent (CI environments that do not ship binary fixtures).
/// </para>
/// </remarks>
public sealed class CalibrationDriverTests
{
    // -----------------------------------------------------------------------
    // Manifest-path resolution — NO hardcoded absolute paths.
    // Walk up from AppContext.BaseDirectory looking for the repo root
    // (the directory that contains "Prisma/Fixtures/PRP2/corpus-manifest.json").
    // Reuses the same marker the renderer already uses ("Prisma/Fixtures").
    // -----------------------------------------------------------------------

    // [CallerFilePath] captures the compile-time absolute path of THIS .cs file, which is
    // always inside the repo on the dev machine.  Walking up from it reaches the repo root
    // even when the build artifacts live on a completely different directory tree (which is
    // the case here: artifacts are under E:\Dynamic\ExxerCubeBanamex\... while the repo is
    // under E:\Dynamic\IndFusion\ExxerCube.Prisma\...).
    // In CI (artifacts-only build without source), [CallerFilePath] compiles to the
    // path at the time the agent built the binary — may be absent; we fall back to other roots.
    private static string? GetThisFilePath([CallerFilePath] string? path = null) => path;

    private static string? LocateManifestPath()
    {
        // Try multiple starting roots in priority order:
        // 1. [CallerFilePath] — source tree root on dev machine (survives out-of-repo artifacts).
        // 2. AppContext.BaseDirectory — build artifacts dir (works when artifacts ARE in the repo).
        // 3. Directory.GetCurrentDirectory() — dotnet-test invocation directory.
        var callerFile = GetThisFilePath();
        var callerDir  = callerFile is not null ? Path.GetDirectoryName(callerFile) : null;

        var searchRoots = new[]
        {
            callerDir,
            AppContext.BaseDirectory,
            Directory.GetCurrentDirectory(),
        };

        foreach (var root in searchRoots)
        {
            if (root is null) continue;
            var found = WalkUpForManifest(root);
            if (found is not null)
                return found;
        }
        return null;
    }

    private static string? WalkUpForManifest(string startDir)
    {
        try
        {
            var dir = startDir;
            while (!string.IsNullOrEmpty(dir))
            {
                var candidate = Path.Combine(dir, "Prisma", "Fixtures", "PRP2", "corpus-manifest.json");
                if (File.Exists(candidate))
                    return candidate;

                var parent = Directory.GetParent(dir)?.FullName;
                if (parent == dir || parent is null)
                    break;
                dir = parent;
            }
        }
        catch (Exception)
        {
            // Swallow — caller will skip if null.
        }
        return null;
    }

    /// <summary>
    /// Runs the calibration harness over the seed corpus and asserts:
    /// (a) the pipeline returns a successful Result for each specimen;
    /// (b) every KnownGood / KnownSynthetic specimen has empty NewFails (cardinal-rule guard);
    /// (c) at least one finding per non-skipped specimen (proves rules ran);
    /// (d) the calibration report is written to the repo.
    /// </summary>
    [Fact]
    public async Task Calibration_SyntheticCorpus_NoNewFalseFails()
    {
        var ct = TestContext.Current.CancellationToken;

        // Graceful skip — manifest absent (CI without fixtures).
        // Assert.Skip marks the test as skipped rather than silently passing.
        var manifestPath = LocateManifestPath();
        if (manifestPath is null)
            Assert.Skip("corpus-manifest.json not found — PDF fixtures absent (expected in CI without binaries).");

        // Deserialize manifest.
        var json = await File.ReadAllTextAsync(manifestPath, ct);
        var manifest = JsonSerializer.Deserialize<CorpusManifest>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        manifest.ShouldNotBeNull("corpus-manifest.json must deserialize to a CorpusManifest");
        manifest!.Specimens.Count.ShouldBeGreaterThan(0, "Manifest must contain at least one specimen");

        // Resolve corpus dir relative to the manifest file.
        var manifestDir = Path.GetDirectoryName(manifestPath)!;
        var corpusDir = Path.IsPathRooted(manifest.CorpusDir)
            ? manifest.CorpusDir
            : Path.GetFullPath(Path.Combine(manifestDir, manifest.CorpusDir));

        // Graceful skip — corpus directory absent (e.g. CI without fixtures).
        if (!Directory.Exists(corpusDir))
            Assert.Skip($"Corpus directory '{corpusDir}' not found — PDF fixtures absent.");

        // Run harness.
        var harness = new CalibrationHarness();
        var harnessResult = await harness.RunAsync(manifest, corpusDir, ct);

        // (a) Harness must succeed.
        harnessResult.IsSuccess.ShouldBeTrue(
            $"CalibrationHarness.RunAsync must succeed. Error: {harnessResult.Error ?? "<none>"}");

        var calibration = harnessResult.Value!;

        foreach (var specimenResult in calibration.SpecimenResults)
        {
            if (specimenResult.Skipped)
                continue; // PDF absent — graceful skip per §5.2.

            // (b) Cardinal-rule guard: no new false-Fails on KnownGood / KnownSynthetic specimens.
            if (specimenResult.Specimen.Label == SpecimenLabel.KnownGood
                || specimenResult.Specimen.Label == SpecimenLabel.KnownSynthetic)
            {
                specimenResult.NewFails.ShouldBeEmpty(
                    $"Specimen '{specimenResult.Specimen.FileName}' ({specimenResult.Specimen.Label}) " +
                    $"has unexpected Fail(s): [{string.Join(", ", specimenResult.NewFails)}]. " +
                    "Either add them to allowedFails / knownFixtureDefects with a reason, " +
                    "or fix the rule (cardinal-rule violation).");
            }

            // (c) At least one finding per non-skipped specimen (proves rules executed).
            specimenResult.FindingCount.ShouldBeGreaterThan(0,
                $"Specimen '{specimenResult.Specimen.FileName}' produced zero findings — " +
                "the validation engine may not have run (binding may have failed).");
        }

        // (d) Write report and confirm it was produced.
        // Derive repo root by walking up from the manifest path.
        string? repoRootHint = null;
        var walkDir = Path.GetDirectoryName(manifestPath);
        while (!string.IsNullOrEmpty(walkDir))
        {
            if (Directory.Exists(Path.Combine(walkDir, "Prisma", "Fixtures")))
            {
                repoRootHint = walkDir;
                break;
            }
            var parent = Directory.GetParent(walkDir)?.FullName;
            if (parent == walkDir || parent is null) break;
            walkDir = parent;
        }

        var reportPath = await CalibrationReportRenderer.RenderAndWriteAsync(
            manifest, calibration, ct, repoRootHint: repoRootHint);

        // Report path may be null in CI without repo root — only assert when written.
        if (reportPath is not null)
        {
            File.Exists(reportPath).ShouldBeTrue(
                $"Calibration report must exist at '{reportPath}'.");
        }
    }
}
