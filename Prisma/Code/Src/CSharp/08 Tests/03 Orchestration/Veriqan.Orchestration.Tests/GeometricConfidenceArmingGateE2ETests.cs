using System.IO;
using ExxerCube.Prisma.Veriqan.Application.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Tenant;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction;
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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Tests;

/// <summary>
/// Story <b>C1.3</b> — the ship-dark arming gate for the geometric-plausibility confidence
/// (VERIQAN C1 epic; design of record:
/// <c>docs/planning-artifacts/SCOPING-veriqan-c1-geometric-extraction-confidence.md</c>
/// §"Design decision 4 — ship-dark arming gate").
/// </summary>
/// <remarks>
/// <para>
/// <b>What this proves (Part A of C1.3):</b> drives the 5 real demo fixtures used by
/// <see cref="VecChecklistDemoE2ETests"/> through the full <see cref="IVerificationPipeline"/>
/// TWICE per fixture — once with <c>PdfPigStatementFieldExtractor</c>'s
/// <c>emitGeometricConfidence</c> constructor flag OFF, once ON — and asserts the resulting
/// verdict (<see cref="Application.Verdict.VerdictSummary.Signal"/>, <c>FailCheckIds</c>,
/// <c>InsufficientDataCheckIds</c>) plus the raw Tasa/Cat <c>Confidence</c> values are IDENTICAL
/// off vs on. Any difference is the cardinal C1 risk realized: the geometric score under-rating a
/// legitimately-clean pick and mass-converting a working verdict into a false abstention. This is
/// the harness the C1 design of record requires to run BEFORE arming — see
/// <see cref="DemoFixture_VerdictAndTasaCatConfidence_IdenticalFlagOffVsOn"/>.
/// </para>
/// <para>
/// <b>Mandatory negative control (also Part A):</b> the 5-demo diff alone cannot distinguish a
/// working fix from an inert one — none of the 5 demo fixtures carry a geometry defect. The
/// synthetic <c>s-c1-swap-displaced.pdf</c> fixture (built in C1.0a, proven separable by the
/// C1.0b spike and the C1.2 production-wiring calibration suite) is a 6th case that MUST flip:
/// flag-OFF reproduces the C1.0a ground truth (CL-10 confident Fail, the CAT/TASA swap read at
/// confidence 1.0), flag-ON must convert CL-10 into an honest
/// <c>InsufficientDataCheckIds</c> abstain. See
/// <see cref="NegativeControl_MarkerDisplacedSwap_Cl10FlipsToInsufficientDataWhenArmed"/>.
/// <c>s-c1-swap</c> (the central-marker swap) is deliberately NOT used here — it is a documented,
/// geometrically-invisible blind spot for this lever (tracker "CARRY-FORWARD for C1.3") and would
/// not flip even when armed; using it as the negative control would be the wrong proof.
/// </para>
/// <para>
/// <b>DI wiring is copied from <see cref="VecChecklistDemoE2ETests"/> /
/// <see cref="SyntheticDefectVerdictE2ETests"/></b> (same registrations, same reused demo
/// reference bundle for the synthetic fixture — its product token aliases into
/// <c>products.csv</c> as TC-BSSB exactly like <c>s6211-baseline.pdf</c>/<c>s-c1-swap.pdf</c>),
/// with one addition: the <see cref="PdfPigStatementFieldExtractor"/> registration is overridden
/// BEFORE <c>AddVeriqanExtraction()</c> runs (TryAdd semantics: first registration wins) so each
/// run can pin <c>emitGeometricConfidence</c> to a known value without touching production DI.
/// </para>
/// <para>
/// <b>Single-bank limitation:</b> the 5 demo fixtures are all the same institution ("Demo Bank
/// (Iqubica)") and PDF layout family. A clean off/on diff here proves no regression for THIS
/// bank's layout — it does not generalize to other banks' statement geometries.
/// </para>
/// <para>
/// <b>Sharper caveat, verified empirically (C1.3):</b> on all 5 demo fixtures, Tasa and Cat are
/// <c>NotExtracted</c> (confidence 0.0 in both flag states, verified by a throwaway diagnostic
/// test run and then removed) — this real-Banamex layout's TASA/CAT line is not matched by
/// <c>ExtractTasaAndCat</c> at all. So the 5-demo diff proves the geometric-confidence flag does
/// not change extraction SUCCESS/FAILURE or any OTHER field's verdict on real-bank data, but it
/// does **not** exercise the scorer's core claim (does it under-rate a clean Tasa/Cat pick) on any
/// non-synthetic fixture — that proof exists only on the synthetic corpus (C1.0b spike + C1.2
/// production-wiring calibration) and on the <c>s-c1-swap-displaced</c> negative control below.
/// </para>
/// </remarks>
[Trait("Category", "LiveOcr")]
[Collection(MetricsIsolationCollection.Name)]
public sealed class GeometricConfidenceArmingGateE2ETests
{
    // -----------------------------------------------------------------------
    // Repo-root walk (copied verbatim from VecChecklistDemoE2ETests / SyntheticDefectVerdictE2ETests)
    // -----------------------------------------------------------------------

    private const string DemoCorpusRelativePath = "Prisma/Fixtures/PRP2/demo";
    private const string SyntheticCorpusRelativePath = "Prisma/Fixtures/PRP2/synthetic";

    private static readonly string? RepoRoot = ComputeRepoRoot();

    private static string? ComputeRepoRoot()
    {
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

    private static string? DemoCorpusDir =>
        RepoRoot is null ? null : Path.Combine(RepoRoot, DemoCorpusRelativePath);

    private static string? SyntheticCorpusDir =>
        RepoRoot is null ? null : Path.Combine(RepoRoot, SyntheticCorpusRelativePath);

    private static readonly StatementContextKey DemoContextKey =
        new("Demo Bank (Iqubica)", PeriodLabel: "Mar-Abr 2026");

    // -----------------------------------------------------------------------
    // Verdict snapshot — the (fixture, verdict, failCheckIds) side of the diff
    // -----------------------------------------------------------------------

    private sealed record VerdictSnapshot(
        VerdictSignal Signal,
        string[] FailCheckIds,
        string[] InsufficientDataCheckIds)
    {
        public string Dump(string label) =>
            $"{label}: Signal={Signal}. FailCheckIds=[{string.Join(", ", FailCheckIds)}]. " +
            $"InsufficientDataCheckIds=[{string.Join(", ", InsufficientDataCheckIds)}].";
    }

    /// <summary>
    /// Builds a fresh DI container per call (matches the existing E2E test pattern), pinning
    /// <c>PdfPigStatementFieldExtractor.emitGeometricConfidence</c> to <paramref name="emitGeometricConfidence"/>
    /// by registering the concrete type BEFORE <c>AddVeriqanExtraction()</c> (TryAdd: first wins).
    /// Runs the fixture through the real <see cref="IVerificationPipeline"/> and returns a sorted,
    /// diff-friendly snapshot of the resulting verdict.
    /// </summary>
    private static async Task<VerdictSnapshot> RunPipelineAsync(
        string corpusDir,
        string bundleRootDir,
        string fixtureName,
        bool emitGeometricConfidence,
        CancellationToken ct)
    {
        var fixturePath = Path.Combine(corpusDir, fixtureName);

        var services = new ServiceCollection();
        services.AddLogging();

        // Pin the ship-dark flag for this run only — registered before AddVeriqanExtraction so
        // its TryAddSingleton<PdfPigStatementFieldExtractor>() is a no-op here.
        services.AddSingleton(sp => new PdfPigStatementFieldExtractor(
            sp.GetRequiredService<ILogger<PdfPigStatementFieldExtractor>>(),
            sp.GetRequiredService<IOptions<PdfExtractionOptions>>(),
            sp.GetRequiredService<IPasswordProvider>(),
            timeProvider: null,
            enableCatalogImageHashing: false,
            emitGeometricConfidence: emitGeometricConfidence));

        services.AddVeriqanIngestion();
        services.AddVeriqanBinding();
        services.AddVeriqanVerdict();

        services.AddVeriqanExtraction();
        services.AddVeriqanValidation();
        services.AddVeriqanVisual();
        services.AddVeriqanReporting();

        services.AddVeriqanReferenceData(opts => opts.RootDirectory = bundleRootDir);

        services.AddVeriqanInMemoryPersistence();
        services.AddSingleton<VeriqanMetrics>();

        services.AddScoped<IVerificationPipeline, VerificationPipeline>();
        services.AddSingleton(TimeProvider.System);

        // Dispose the root container, not just the child scope — it owns the singleton
        // IHeaderProductOcrEngine (native TesseractEngine); see VecChecklistDemoE2ETests remarks.
        await using var sp = services.BuildServiceProvider();

        var pdfBytes = await File.ReadAllBytesAsync(fixturePath, ct);
        var submission = new StatementSubmission(
            Pdf: pdfBytes,
            FileName: fixtureName,
            ContextKey: DemoContextKey);

        Result<VerificationOutcome> result;
        await using (var scope = sp.CreateAsyncScope())
        {
            var pipeline = scope.ServiceProvider.GetRequiredService<IVerificationPipeline>();
            result = await pipeline.ProcessAsync(submission, ct);
        }

        result.IsSuccess.ShouldBeTrue(
            $"Pipeline returned a Result failure for '{fixtureName}' (emitGeometricConfidence={emitGeometricConfidence}): " +
            $"{result.Error ?? "<none>"}.");

        var summary = result.Value!.Summary;
        summary.ShouldNotBeNull($"'{fixtureName}': VerdictSummary must be populated.");

        return new VerdictSnapshot(
            summary.Signal,
            [.. summary.FailCheckIds.OrderBy(id => id, StringComparer.Ordinal)],
            [.. summary.InsufficientDataCheckIds.OrderBy(id => id, StringComparer.Ordinal)]);
    }

    /// <summary>
    /// Runs the extractor directly (bypassing the full pipeline) to read the raw Tasa/Cat
    /// <see cref="Domain.Extraction.ExtractedField{T}.Confidence"/> values — the pipeline's
    /// <see cref="VerificationOutcome"/> does not expose the extracted <c>StatementModel</c>, so
    /// this is the only way to observe the confidence number itself (as opposed to its downstream
    /// verdict effect). Returns <c>Extracted=false</c> when the extractor itself fails (e.g.
    /// <c>scanned.pdf</c>, which has no text layer) — both flag states are expected to fail
    /// identically in that case.
    /// </summary>
    private static async Task<(bool Extracted, double TasaConfidence, double CatConfidence, bool TasaFound, bool CatFound)> TryExtractTasaCatConfidenceAsync(
        string corpusDir,
        string fixtureName,
        bool emitGeometricConfidence,
        CancellationToken ct)
    {
        var pdfPath = Path.Combine(corpusDir, fixtureName);
        var pdfBytes = await File.ReadAllBytesAsync(pdfPath, ct);

        // IPasswordProvider's default (NullPasswordProvider) is internal to the Infrastructure.
        // Extraction assembly, so resolve the extractor through a minimal DI container (same
        // TryAdd-override trick as RunPipelineAsync) instead of constructing it by hand.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(sp => new PdfPigStatementFieldExtractor(
            sp.GetRequiredService<ILogger<PdfPigStatementFieldExtractor>>(),
            sp.GetRequiredService<IOptions<PdfExtractionOptions>>(),
            sp.GetRequiredService<IPasswordProvider>(),
            timeProvider: null,
            enableCatalogImageHashing: false,
            emitGeometricConfidence: emitGeometricConfidence));
        services.AddVeriqanExtraction();

        await using var sp = services.BuildServiceProvider();
        var extractor = sp.GetRequiredService<PdfPigStatementFieldExtractor>();

        var result = await extractor.ExtractFullAsync(pdfBytes, ct);
        if (!result.IsSuccess)
            return (false, 0.0, 0.0, false, false);

        var periodSummary = result.Value!.PeriodSummary;
        if (periodSummary is null)
            return (false, 0.0, 0.0, false, false);

        return (
            true,
            periodSummary.Tasa.Confidence,
            periodSummary.Cat.Confidence,
            periodSummary.Tasa.Status == Domain.Extraction.ExtractionStatus.Extracted,
            periodSummary.Cat.Status == Domain.Extraction.ExtractionStatus.Extracted);
    }

    // -----------------------------------------------------------------------
    // Part A.1 — the 5-demo verdict diff (the primary safety gate)
    // -----------------------------------------------------------------------

    public static IEnumerable<object[]> DemoFixtureNames =>
    [
        ["good.pdf"],
        ["bad-math-cl21.pdf"],
        ["bad-font-cl35.pdf"],
        ["scanned.pdf"],
        ["compliant-master.pdf"],
    ];

    /// <summary>
    /// The C1.3 gate: for each of the 5 real demo fixtures, the verdict (signal + FailCheckIds +
    /// InsufficientDataCheckIds) and the raw Tasa/Cat confidence must be byte-identical whether
    /// <c>emitGeometricConfidence</c> is off or on. A difference here means the geometric-
    /// plausibility score under-rates a clean pick on this bank's real layout — the cardinal C1
    /// false-abstain risk — and arming must NOT proceed until it is fixed.
    /// </summary>
    [Theory]
    [MemberData(nameof(DemoFixtureNames))]
    public async Task DemoFixture_VerdictAndTasaCatConfidence_IdenticalFlagOffVsOn(string fixtureName)
    {
        var ct = TestContext.Current.CancellationToken;

        var corpusDir = DemoCorpusDir;
        if (corpusDir is null)
            Assert.Skip("Repository root could not be determined from assembly location — demo corpus unreachable.");

        var fixturePath = Path.Combine(corpusDir, fixtureName);
        if (!File.Exists(fixturePath))
            Assert.Skip($"Demo fixture '{fixtureName}' not present at '{fixturePath}'.");

        var bundleRootDir = Path.Combine(corpusDir, "reference-bundle");
        if (!Directory.Exists(bundleRootDir))
            Assert.Skip($"Reference bundle root not found at '{bundleRootDir}'.");

        var off = await RunPipelineAsync(corpusDir, bundleRootDir, fixtureName, emitGeometricConfidence: false, ct);
        var on = await RunPipelineAsync(corpusDir, bundleRootDir, fixtureName, emitGeometricConfidence: true, ct);

        var dump = $"'{fixtureName}':\n  {off.Dump("OFF")}\n  {on.Dump("ON")}";

        on.Signal.ShouldBe(off.Signal, dump);
        on.FailCheckIds.ShouldBe(off.FailCheckIds, dump);
        on.InsufficientDataCheckIds.ShouldBe(off.InsufficientDataCheckIds, dump);

        // Raw confidence must also be identical — proves the diff isn't accidentally masking a
        // real confidence change that happens not to cross any rule's 0.8 threshold today.
        var (offExtracted, offTasa, offCat, offTasaFound, offCatFound) =
            await TryExtractTasaCatConfidenceAsync(corpusDir, fixtureName, false, ct);
        var (onExtracted, onTasa, onCat, onTasaFound, onCatFound) =
            await TryExtractTasaCatConfidenceAsync(corpusDir, fixtureName, true, ct);

        offExtracted.ShouldBe(
            onExtracted,
            $"'{fixtureName}': extraction success must not depend on the geometric-confidence flag.");
        offTasaFound.ShouldBe(
            onTasaFound,
            $"'{fixtureName}': Tasa extraction status (Extracted vs not) must not depend on the flag.");
        offCatFound.ShouldBe(
            onCatFound,
            $"'{fixtureName}': Cat extraction status (Extracted vs not) must not depend on the flag.");

        if (offExtracted)
        {
            // Confidence equality holds unconditionally: when a field isn't Extracted (Missing/
            // InvalidFormat) both flag states report the same constant confidence for that status
            // (see ExtractedField.Missing/InvalidFormat) — the geometric score only ever changes
            // the Found-path confidence.
            onTasa.ShouldBe(
                offTasa,
                $"'{fixtureName}': Tasa confidence changed OFF={offTasa} -> ON={onTasa} — cardinal C1 false-abstain risk.");
            onCat.ShouldBe(
                offCat,
                $"'{fixtureName}': Cat confidence changed OFF={offCat} -> ON={onCat} — cardinal C1 false-abstain risk.");

            // The >= 0.8 guard-floor check only makes sense for fields that were actually found —
            // an un-extracted field legitimately reports a low constant confidence regardless of
            // the flag, and that is not the C1 cardinal risk.
            if (onTasaFound)
            {
                onTasa.ShouldBeGreaterThanOrEqualTo(
                    0.8,
                    $"'{fixtureName}': Tasa confidence must clear the 0.8 guard when armed.");
            }

            if (onCatFound)
            {
                onCat.ShouldBeGreaterThanOrEqualTo(
                    0.8,
                    $"'{fixtureName}': Cat confidence must clear the 0.8 guard when armed.");
            }
        }
    }

    // -----------------------------------------------------------------------
    // Part A.2 — mandatory negative control (proves the harness can detect a real flip)
    // -----------------------------------------------------------------------

    /// <summary>
    /// The negative control the C1 design of record requires: a harness that only ever shows
    /// "nothing moved" cannot distinguish a working fix from an inert one. <c>s-c1-swap-displaced.pdf</c>
    /// (C1.0a) carries a realistic, marker-displaced CAT/TASA swap that the C1.0b spike and the
    /// C1.2 production-wiring calibration suite both proved separable (Tasa/Cat score &lt; 0.8
    /// when armed). This test drives it through the FULL pipeline (not just the extractor) and
    /// proves the swap flips CL-10 from a confident Fail (flag off — reproducing the C1.0a ground
    /// truth) to an honest InsufficientData abstain (flag on).
    /// </summary>
    [Fact]
    public async Task NegativeControl_MarkerDisplacedSwap_Cl10FlipsToInsufficientDataWhenArmed()
    {
        var ct = TestContext.Current.CancellationToken;

        var syntheticDir = SyntheticCorpusDir;
        if (syntheticDir is null || RepoRoot is null)
            Assert.Skip("Repository root could not be determined from assembly location — synthetic corpus unreachable.");

        const string fixtureName = "s-c1-swap-displaced.pdf";
        var fixturePath = Path.Combine(syntheticDir, fixtureName);
        if (!File.Exists(fixturePath))
            Assert.Skip($"Synthetic fixture '{fixtureName}' not present at '{fixturePath}'.");

        // Reuse the demo reference bundle — the synthetic corpus's TC-BSSB product token aliases
        // into products.csv exactly like s6211-baseline.pdf/s-c1-swap.pdf (see SyntheticDefectVerdictE2ETests).
        var bundleRootDir = Path.Combine(RepoRoot, "Prisma/Fixtures/PRP2/demo/reference-bundle");
        if (!Directory.Exists(bundleRootDir))
            Assert.Skip($"Reference bundle root not found at '{bundleRootDir}'.");

        // ------------------------------------------------------------------
        // Flag OFF — must reproduce the C1.0a ground truth: confident-WRONG Fail.
        // ------------------------------------------------------------------
        var off = await RunPipelineAsync(syntheticDir, bundleRootDir, fixtureName, emitGeometricConfidence: false, ct);
        off.FailCheckIds.ShouldContain(
            "CL-10",
            "flag-off must reproduce the C1.0a confident-wrong swap: " + off.Dump("OFF"));
        off.InsufficientDataCheckIds.ShouldNotContain(
            "CL-10",
            "flag-off CL-10 must be a confident Fail, not an abstain: " + off.Dump("OFF"));

        // ------------------------------------------------------------------
        // Flag ON — must convert the confident-wrong Fail into an honest abstain.
        // ------------------------------------------------------------------
        var on = await RunPipelineAsync(syntheticDir, bundleRootDir, fixtureName, emitGeometricConfidence: true, ct);
        on.InsufficientDataCheckIds.ShouldContain(
            "CL-10",
            "flag-on must convert the marker-displaced swap into an honest abstain: " + on.Dump("ON"));
        on.FailCheckIds.ShouldNotContain(
            "CL-10",
            "flag-on CL-10 must NOT remain a confident Fail — that would mean the geometric score didn't fire: " +
            on.Dump("ON"));

        // ------------------------------------------------------------------
        // Corroborate at the confidence level too (belt-and-braces with the verdict-level proof).
        // ------------------------------------------------------------------
        var (offExtracted, offTasa, offCat, offTasaFound, offCatFound) =
            await TryExtractTasaCatConfidenceAsync(syntheticDir, fixtureName, false, ct);
        offExtracted.ShouldBeTrue($"'{fixtureName}': extraction must succeed (flag off).");
        offTasaFound.ShouldBeTrue($"'{fixtureName}': Tasa must be Extracted (flag off).");
        offCatFound.ShouldBeTrue($"'{fixtureName}': Cat must be Extracted (flag off).");
        offTasa.ShouldBe(1.0, $"'{fixtureName}' (flag off): Tasa confidence must be the pre-C1.2 constant 1.0.");
        offCat.ShouldBe(1.0, $"'{fixtureName}' (flag off): Cat confidence must be the pre-C1.2 constant 1.0.");

        var (onExtracted, onTasa, onCat, onTasaFound, onCatFound) =
            await TryExtractTasaCatConfidenceAsync(syntheticDir, fixtureName, true, ct);
        onExtracted.ShouldBeTrue($"'{fixtureName}': extraction must succeed (flag on).");
        onTasaFound.ShouldBeTrue($"'{fixtureName}': Tasa must be Extracted (flag on).");
        onCatFound.ShouldBeTrue($"'{fixtureName}': Cat must be Extracted (flag on).");
        onTasa.ShouldBeLessThan(0.8, $"'{fixtureName}' (flag on): Tasa confidence must fall below the 0.8 guard.");
        onCat.ShouldBeLessThan(0.8, $"'{fixtureName}' (flag on): Cat confidence must fall below the 0.8 guard.");
    }
}
