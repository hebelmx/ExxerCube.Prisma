using System.IO;
using System.Text.Json;
using ExxerCube.Prisma.Veriqan.Application.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Application.Verdict;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Tenant;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Infrastructure.ReferenceData.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Infrastructure.Reporting.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Infrastructure.Validation.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Infrastructure.Visual.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Orchestration.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Orchestration.Observability;
using ExxerCube.Prisma.Veriqan.Orchestration.Pipeline;
using IndQuestResults;
using Microsoft.Extensions.DependencyInjection;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Tests;

/// <summary>
/// Verdict-level end-to-end proof for epic slice E6.S6.2.3: drives the REAL
/// <see cref="VerificationPipeline"/> against the synthetic "s6211" corpus (god's-eye manifest,
/// see <c>Prisma/Fixtures/PRP2/synthetic/</c>) and asserts that CL-21 / CL-22 fire RED on the
/// injected math defect and PASS on the clean baseline, that CL-35 fires on the font-substitution
/// variant, and that the scanned (image-only) variant halts with ExtractionGap.
/// </summary>
/// <remarks>
/// <para>
/// <b>DI wiring and repo-root walk are copied verbatim from <see cref="VecChecklistDemoE2ETests"/></b>
/// (per owner instruction) — same service registrations, same reference bundle
/// (<c>Prisma/Fixtures/PRP2/demo/reference-bundle</c>, institution "Demo Bank (Iqubica)"), same
/// two-case repo-root resolution (direct / sibling-repo-next-to-BuildArtifacts).
/// </para>
/// <para>
/// <b>Why the demo bundle, not a dedicated synthetic bundle:</b> the s6211 generator emits
/// product token "Tarjeta de Crédito BSSB", which the demo bundle's <c>products.csv</c> already
/// aliases to <c>TC-BSSB</c> (see the alias list including "Tarjeta de Crédito BSSB" verbatim), so
/// the pipeline binds a tenant profile and evaluates real rules without any new bundle authoring.
/// </para>
/// <para>
/// <b>Overall Signal is RED for baseline/math/font/abstain regardless of the arithmetic outcome</b>
/// — the synthetic PDFs have no glossary/section pages, so structural CONDUSEF checks
/// (LAW-SEC-PRESENCE, LAW-§26-NOTAS, LAW-§27-GLOSARIO, ...) fail independently of CL-21/CL-22.
/// This is expected and is why assertions target specific <c>FailCheckIds</c> entries rather than
/// the overall <see cref="VerdictSummary.Signal"/> for those four fixtures. Only the scanned
/// fixture (which halts before rules run) asserts on overall <see cref="VerdictSummary.Signal"/>.
/// </para>
/// </remarks>
[Trait("Category", "LiveOcr")]
[Collection(MetricsIsolationCollection.Name)]
public sealed class SyntheticDefectVerdictE2ETests
{
    // -----------------------------------------------------------------------
    // Corpus location
    // -----------------------------------------------------------------------

    /// <summary>
    /// Sub-path from the repository root to the synthetic s6211 corpus directory.
    /// </summary>
    private const string SyntheticCorpusRelativePath = "Prisma/Fixtures/PRP2/synthetic";

    /// <summary>
    /// Sub-path from the repository root to the demo reference-bundle directory (reused as-is;
    /// the synthetic product token aliases into it — see remarks).
    /// </summary>
    private const string DemoBundleRelativePath = "Prisma/Fixtures/PRP2/demo/reference-bundle";

    /// <summary>
    /// Absolute path to the repository root, resolved at runtime by walking up from the test
    /// assembly location until a directory containing <c>CLAUDE.md</c> is found. Returns
    /// <see langword="null"/> when the repo root cannot be located, in which case all tests skip.
    /// </summary>
    private static readonly string? RepoRoot = ComputeRepoRoot();

    private static string? ComputeRepoRoot()
    {
        // Walk up from the assembly output directory looking for the repo root,
        // identified by the presence of CLAUDE.md.
        //
        // Two cases are handled at each level:
        //   1. Direct: CLAUDE.md in the current directory — assembly output is inside the
        //      repo tree (e.g. a Debug output dir placed within the repo).
        //   2. Sibling: a child directory named "ExxerCube.Prisma" contains CLAUDE.md —
        //      handles the standard layout where BuildArtifacts sits next to the repo root.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "CLAUDE.md")))
                return dir.FullName;

            var siblingRepo = Path.Combine(dir.FullName, "ExxerCube.Prisma");
            if (Directory.Exists(siblingRepo) &&
                File.Exists(Path.Combine(siblingRepo, "CLAUDE.md")))
                return siblingRepo;

            dir = dir.Parent;
        }

        return null;
    }

    /// <summary>
    /// Context key used for all synthetic submissions. Institution matches the reused demo
    /// reference bundle; period label is arbitrary (no period-specific reference lookups are
    /// exercised by these checks).
    /// </summary>
    private static readonly StatementContextKey SyntheticContextKey =
        new("Demo Bank (Iqubica)", PeriodLabel: "Mar-Abr 2026");

    // -----------------------------------------------------------------------
    // Manifest DTOs (local, minimal — do NOT depend on Extraction.Tests' internal
    // SyntheticGoldManifest type; parse the JSON shape we need directly).
    // -----------------------------------------------------------------------

    private sealed record ArithmeticCheckEntry(string CheckId, string ExpectedOutcome);

    private static IReadOnlyList<ArithmeticCheckEntry> ReadArithmeticChecks(string manifestPath)
    {
        using var stream = File.OpenRead(manifestPath);
        using var doc = JsonDocument.Parse(stream);

        if (!doc.RootElement.TryGetProperty("arithmeticChecks", out var arr) ||
            arr.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var results = new List<ArithmeticCheckEntry>();
        foreach (var entry in arr.EnumerateArray())
        {
            var checkId = entry.GetProperty("checkId").GetString()
                ?? throw new InvalidOperationException($"'checkId' missing in manifest '{manifestPath}'.");
            var expectedOutcome = entry.GetProperty("expectedOutcome").GetString()
                ?? throw new InvalidOperationException($"'expectedOutcome' missing in manifest '{manifestPath}'.");
            results.Add(new ArithmeticCheckEntry(checkId, expectedOutcome));
        }

        return results;
    }

    // -----------------------------------------------------------------------
    // Shared pipeline driver
    // -----------------------------------------------------------------------

    /// <summary>
    /// Builds the real DI graph (verbatim wiring from <see cref="VecChecklistDemoE2ETests"/>),
    /// resolves the synthetic fixture and the demo reference bundle via the shared repo-root walk,
    /// submits the given PDF through <see cref="IVerificationPipeline"/>, and returns the resulting
    /// <see cref="VerdictSummary"/>. Skips cleanly (via <see cref="Assert.Skip"/>) when the repo
    /// root, the fixture, or the bundle cannot be found.
    /// </summary>
    private static async Task<VerdictSummary> RunSyntheticFixtureAsync(string pdfFileName, CancellationToken ct)
    {
        if (RepoRoot is null)
            Assert.Skip("Repository root could not be determined from assembly location — synthetic corpus unreachable.");

        var syntheticDir = Path.Combine(RepoRoot, SyntheticCorpusRelativePath);
        var fixturePath = Path.Combine(syntheticDir, pdfFileName);

        if (!File.Exists(fixturePath))
            Assert.Skip(
                $"Synthetic fixture '{pdfFileName}' not present at '{fixturePath}'. " +
                "Run synth_gen.py to (re)generate the s6211 corpus.");

        var bundleRootDir = Path.Combine(RepoRoot, DemoBundleRelativePath);
        if (!Directory.Exists(bundleRootDir))
            Assert.Skip(
                $"Reference bundle root not found at '{bundleRootDir}'. " +
                "Run the bundle-authoring step to create the CSV files.");

        // ------------------------------------------------------------------
        // Arrange — real CsvReferenceDataAdapter pointed at the reused demo bundle.
        // ------------------------------------------------------------------
        var services = new ServiceCollection();
        services.AddLogging();

        // Application layer
        services.AddVeriqanIngestion();
        services.AddVeriqanBinding();
        services.AddVeriqanVerdict();

        // Infrastructure adapters (real implementations)
        services.AddVeriqanExtraction();
        services.AddVeriqanValidation();
        services.AddVeriqanVisual();
        services.AddVeriqanReporting();

        // Real reference-data provider (CSV adapter, reused demo bundle).
        services.AddVeriqanReferenceData(opts => opts.RootDirectory = bundleRootDir);

        // In-memory persistence stubs — no SQL Server required
        services.AddVeriqanInMemoryPersistence();

        // Metrics singleton required by VerificationPipeline
        services.AddSingleton<VeriqanMetrics>();

        // Pipeline
        services.AddScoped<IVerificationPipeline, VerificationPipeline>();
        services.AddSingleton(TimeProvider.System);

        // Dispose the root container (not just the child scope below) — it owns the singleton
        // IHeaderProductOcrEngine (TesseractHeaderProductOcrEngine); disposing only the async
        // scope would leak that native engine handle (see VecChecklistDemoE2ETests).
        await using var sp = services.BuildServiceProvider();

        var pdfBytes = await File.ReadAllBytesAsync(fixturePath, ct);

        var submission = new StatementSubmission(
            Pdf: pdfBytes,
            FileName: pdfFileName,
            ContextKey: SyntheticContextKey);

        Result<VerificationOutcome> result;
        await using (var scope = sp.CreateAsyncScope())
        {
            var pipeline = scope.ServiceProvider.GetRequiredService<IVerificationPipeline>();
            result = await pipeline.ProcessAsync(submission, ct);
        }

        result.IsSuccess.ShouldBeTrue(
            $"Pipeline returned a Result failure for '{pdfFileName}': {result.Error ?? "<none>"}.");

        var outcome = result.Value!;

        outcome.Summary.ShouldNotBeNull(
            $"'{pdfFileName}': VerdictSummary must be populated.");

        return outcome.Summary;
    }

    // -----------------------------------------------------------------------
    // Manifest-driven arithmetic theory
    // -----------------------------------------------------------------------

    public static IEnumerable<object[]> ArithmeticFixtures =>
    [
        ["s6211-baseline.pdf", "s6211-baseline.manifest.json"],
        ["s6211-math.pdf", "s6211-math.manifest.json"],
        ["s6211-font.pdf", "s6211-font.manifest.json"],
        ["s6211-abstain.pdf", "s6211-abstain.manifest.json"],
    ];

    /// <summary>
    /// Drives each s6211 fixture through the real pipeline and asserts, per the fixture's own
    /// god's-eye manifest <c>arithmeticChecks</c> entries, that CL-21/CL-22 land in the expected
    /// bucket: <c>Fail</c> → must be in <see cref="VerdictSummary.FailCheckIds"/>; <c>Pass</c> →
    /// must be absent from both <see cref="VerdictSummary.FailCheckIds"/> and
    /// <see cref="VerdictSummary.InsufficientDataCheckIds"/>.
    /// </summary>
    [Theory]
    [MemberData(nameof(ArithmeticFixtures))]
    public async Task Synthetic_ArithmeticChecks_MatchManifest(string pdfFileName, string manifestFileName)
    {
        var ct = TestContext.Current.CancellationToken;

        if (RepoRoot is null)
            Assert.Skip("Repository root could not be determined from assembly location — synthetic corpus unreachable.");

        var manifestPath = Path.Combine(RepoRoot, SyntheticCorpusRelativePath, manifestFileName);
        if (!File.Exists(manifestPath))
            Assert.Skip($"Synthetic manifest '{manifestFileName}' not present at '{manifestPath}'.");

        var arithmeticChecks = ReadArithmeticChecks(manifestPath);

        var summary = await RunSyntheticFixtureAsync(pdfFileName, ct);

        foreach (var check in arithmeticChecks)
        {
            var dump =
                $"'{pdfFileName}' / check '{check.CheckId}' (expected {check.ExpectedOutcome}): " +
                $"FailCheckIds=[{string.Join(", ", summary.FailCheckIds)}]. " +
                $"InsufficientDataCheckIds=[{string.Join(", ", summary.InsufficientDataCheckIds)}].";

            if (string.Equals(check.ExpectedOutcome, "Fail", StringComparison.Ordinal))
            {
                summary.FailCheckIds.ShouldContain(check.CheckId, dump);
            }
            else if (string.Equals(check.ExpectedOutcome, "Pass", StringComparison.Ordinal))
            {
                summary.FailCheckIds.ShouldNotContain(check.CheckId, dump);
                summary.InsufficientDataCheckIds.ShouldNotContain(check.CheckId, dump);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Unrecognized expectedOutcome '{check.ExpectedOutcome}' for check '{check.CheckId}' in '{manifestFileName}'.");
            }
        }
    }

    // -----------------------------------------------------------------------
    // Focused facts
    // -----------------------------------------------------------------------

    /// <summary>
    /// The font-substitution variant injects a Courier run where the demo bundle requires
    /// Helvetica; asserts CL-35 (typography/legal-form check) fires as a Fail finding on the font
    /// variant <b>and</b> does <b>not</b> fire on the all-Helvetica baseline — locking CL-35 as a
    /// genuine discriminator (a change that made CL-35 fire on every fixture would fail this test).
    /// </summary>
    [Fact]
    public async Task Synthetic_FontVariant_TripsCl35()
    {
        var ct = TestContext.Current.CancellationToken;

        var baseline = await RunSyntheticFixtureAsync("s6211-baseline.pdf", ct);
        baseline.FailCheckIds.ShouldNotContain(
            "CL-35",
            $"'s6211-baseline.pdf' (all-Helvetica) must NOT trip CL-35 but did: " +
            $"FailCheckIds=[{string.Join(", ", baseline.FailCheckIds)}].");

        var summary = await RunSyntheticFixtureAsync("s6211-font.pdf", ct);
        summary.FailCheckIds.ShouldContain(
            "CL-35",
            $"'s6211-font.pdf': expected 'CL-35' in FailCheckIds but got " +
            $"[{string.Join(", ", summary.FailCheckIds)}]. " +
            $"InsufficientDataCheckIds=[{string.Join(", ", summary.InsufficientDataCheckIds)}].");
    }

    /// <summary>
    /// The scanned (image-only) variant has no extractable text layer; asserts the pipeline halts
    /// with <see cref="VerdictSignal.ExtractionGap"/> rather than running rules — <b>and</b> that it
    /// halts for the correct reason. An image-only PDF extracts zero fields, so the
    /// <b>extraction-coverage floor</b> (Stage 2b, <c>MinExtractionCoverageCount</c>) short-circuits
    /// <b>first</b> with <see cref="BlockReason.InsufficientExtractionCoverage"/>; the later
    /// text-layer floor (Stage 2c, <c>MinTextLayerWordCount</c>) is never reached. Asserting the
    /// specific <see cref="BlockReason"/> (not just the collapsed <see cref="VerdictSignal"/>) keeps
    /// the god's-eye claim mechanism-honest and would catch a regression that swapped the two floors.
    /// </summary>
    [Fact]
    public async Task Synthetic_ScannedVariant_ProducesExtractionGap()
    {
        var ct = TestContext.Current.CancellationToken;

        var summary = await RunSyntheticFixtureAsync("s6211-scanned.pdf", ct);

        var dump =
            $"'s6211-scanned.pdf': got Signal={summary.Signal}. " +
            $"FailCheckIds=[{string.Join(", ", summary.FailCheckIds)}]. " +
            $"InsufficientDataCheckIds=[{string.Join(", ", summary.InsufficientDataCheckIds)}]. " +
            $"BlockedReason={summary.BlockedOutcome?.Reason}. " +
            $"BlockedDetail={summary.BlockedOutcome?.Detail}.";

        summary.Signal.ShouldBe(VerdictSignal.ExtractionGap, dump);
        summary.BlockedOutcome.ShouldNotBeNull(dump);
        summary.BlockedOutcome!.Reason.ShouldBe(BlockReason.InsufficientExtractionCoverage, dump);
    }

    // -----------------------------------------------------------------------
    // C1.0a make-or-break gate — the geometric CAT/TASA swap
    // -----------------------------------------------------------------------

    /// <summary>
    /// Epic C1.0a's make-or-break proof: <c>ExtractTasaAndCat</c> (PdfPigStatementFieldExtractor.cs
    /// ~1245-1288) disambiguates CAT vs TASA by pure X-order with no label/column cross-check.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The 's-c1-swap' fixture transposes the two percent tokens (keeping 'sin IVA' between them,
    /// exactly like the real baseline) so the extractor reads <c>Cat=0.2736</c> / <c>Tasa=0.2886</c>
    /// — TRANSPOSED — at confidence <c>1.0</c> (the field's true identity, per the fixture's
    /// god's-eye manifest <c>geometryDefect.trueValue</c>, is <c>Cat=0.2886</c> / <c>Tasa=0.2736</c>,
    /// unchanged from the s6211 baseline).
    /// </para>
    /// <para>
    /// CL-10 (<see cref="Prisma.Veriqan.Infrastructure.Validation.Rules.Cl10CatRule"/>) computes
    /// <c>CAT = ((creditLine × Tasa + annualCommission) / creditLine) × 100</c> and compares it to
    /// the extracted CAT within a 0.50-percentage-point legal tolerance. With the TRUE values
    /// (creditLine=78000, Tasa=0.2736, Cat=0.2886 from the demo bundle's TC-BSSB account) the
    /// computed CAT is ≈29.28%, extracted CAT is 28.86% — diff ≈0.42pp, comfortably inside
    /// tolerance, so the baseline fixture legitimately PASSES CL-10. With the SWAPPED extraction
    /// (Cat=0.2736, Tasa=0.2886) the computed CAT becomes ≈30.78% against an extracted 27.36% —
    /// diff ≈3.42pp, far outside tolerance — so CL-10 FAILS. Both reads are reported at confidence
    /// <c>1.0</c> (nothing today makes the swapped read look any less certain than the correct one)
    /// — this is the "confident-WRONG, worse than abstain" failure class C1 exists to close.
    /// </para>
    /// <para>
    /// This test intentionally does NOT touch the extractor, <c>ExtractedField</c>, or any scorer —
    /// it proves the defect exists TODAY, on the unmodified pipeline, as the prerequisite ground
    /// truth for the C1.0b separation spike and the C1.2 geometric-plausibility scorer.
    /// </para>
    /// </remarks>
    [Fact]
    public async Task Synthetic_C1Swap_FlipsCl10ToConfidentWrongFail()
    {
        var ct = TestContext.Current.CancellationToken;

        // Control: the unmodified s6211 baseline (true, untransposed CAT/TASA) must PASS CL-10 —
        // i.e. CL-10 is neither a Fail nor an InsufficientData abstention. If this control itself
        // doesn't pass, the "swap flips it to Fail" comparison below would be meaningless.
        var baseline = await RunSyntheticFixtureAsync("s6211-baseline.pdf", ct);
        var baselineDump =
            $"'s6211-baseline.pdf': FailCheckIds=[{string.Join(", ", baseline.FailCheckIds)}]. " +
            $"InsufficientDataCheckIds=[{string.Join(", ", baseline.InsufficientDataCheckIds)}].";
        baseline.FailCheckIds.ShouldNotContain("CL-10", baselineDump);
        baseline.InsufficientDataCheckIds.ShouldNotContain("CL-10", baselineDump);

        // The swap: identical fixture except the two TASA/CAT percent tokens are transposed.
        // CL-10 must now FAIL — confidently, not abstain — proving the geometric misread drives a
        // false verdict rather than an honest "cannot verify".
        var swapped = await RunSyntheticFixtureAsync("s-c1-swap.pdf", ct);
        var swappedDump =
            $"'s-c1-swap.pdf': FailCheckIds=[{string.Join(", ", swapped.FailCheckIds)}]. " +
            $"InsufficientDataCheckIds=[{string.Join(", ", swapped.InsufficientDataCheckIds)}].";
        swapped.FailCheckIds.ShouldContain("CL-10", swappedDump);
        swapped.InsufficientDataCheckIds.ShouldNotContain(
            "CL-10",
            "CL-10 must be a confident FAIL, not an honest abstention — " + swappedDump);
    }
}
