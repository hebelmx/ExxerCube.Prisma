using System.IO;
using ExxerCube.Prisma.Veriqan.Application.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Extraction;
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
/// Story <b>C1.4</b> — the ship-dark arming-evidence gate for the RESUMEN/NIVEL money-field
/// slice of the geometric-plausibility confidence (VERIQAN C1 epic; design of record:
/// <c>docs/planning-artifacts/SCOPING-veriqan-c1-geometric-extraction-confidence.md</c>
/// §"Design decision 4 — ship-dark arming gate"). Reuses the exact DI/harness pattern
/// <see cref="GeometricConfidenceArmingGateE2ETests"/> built for C1.3 (Tasa/Cat), extended to
/// the 7 <c>ScanResumenColumn</c>-wired fields.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this gate differs from C1.3's — NON-VACUOUS on real data:</b> C1.3 found that Tasa/Cat
/// are <c>NotExtracted</c> on all 5 real demo fixtures, so that gate's "nothing changed" result
/// proved the flag doesn't break anything but never actually exercised the scorer on a real,
/// successfully-extracted pick. The RESUMEN fields are different: CL-21/CL-44 (per the C1 tracker
/// and B2's residual) DO fire against real extracted RESUMEN values on <c>bad-math-cl21.pdf</c> —
/// so this gate's off/on diff is a genuine test of whether the C1.4 rank-adjacency signal
/// under-rates a real, successfully-extracted RESUMEN pick on this bank's actual layout. A flip
/// here is the cardinal C1 false-abstain risk REALIZED on real data, not merely a synthetic-corpus
/// finding — see <see cref="DemoFixture_VerdictAndResumenConfidence_IdenticalFlagOffVsOn"/> and its
/// remarks on what to do if it fails.
/// </para>
/// <para>
/// <b>Mandatory negative control:</b> <c>decoy-resumen-amount.pdf</c> (C1.4, dummievec
/// right-column profile) carries a decoy amount that <c>ScanResumenColumn</c>'s
/// <c>findLeftmost</c> pick grabs instead of the true <c>AdeudoPeriodoAnterior</c> value, proven
/// separable by <c>GeometricPlausibilityResumenCalibrationTests</c> (Extraction.Tests). This test
/// drives it through the FULL pipeline and proves the mis-pick flips CL-21 from a confident
/// (wrong-operand) verdict to an honest <c>InsufficientData</c> abstain once armed.
/// </para>
/// <para>
/// <b>DI wiring</b> is copied verbatim from <see cref="GeometricConfidenceArmingGateE2ETests"/> —
/// same TryAdd-override trick to pin <c>emitGeometricConfidence</c> per run.
/// </para>
/// <para>
/// <b>Single-bank limitation:</b> same caveat as C1.3 — the 5 demo fixtures are one institution's
/// layout family; a clean diff here does not generalize to other banks.
/// </para>
/// </remarks>
[Trait("Category", "LiveOcr")]
[Collection(MetricsIsolationCollection.Name)]
public sealed class GeometricConfidenceResumenArmingGateE2ETests
{
    // -----------------------------------------------------------------------
    // Repo-root walk (copied verbatim from GeometricConfidenceArmingGateE2ETests)
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

    private sealed record VerdictSnapshot(
        VerdictSignal Signal,
        string[] FailCheckIds,
        string[] InsufficientDataCheckIds)
    {
        public string Dump(string label) =>
            $"{label}: Signal={Signal}. FailCheckIds=[{string.Join(", ", FailCheckIds)}]. " +
            $"InsufficientDataCheckIds=[{string.Join(", ", InsufficientDataCheckIds)}].";
    }

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
    /// The 7 RESUMEN money fields wired to the C1.4 scorer, by name (for dump messages) and a
    /// <see cref="PeriodSummary"/> accessor.
    /// </summary>
    private static readonly (string Name, Func<PeriodSummary, ExtractedField<decimal>> Accessor)[] ResumenFields =
    [
        ("AdeudoPeriodoAnterior", ps => ps.AdeudoPeriodoAnterior),
        ("CargosRegularesNoMeses", ps => ps.CargosRegularesNoMeses),
        ("CargosComprasAMesesCapital", ps => ps.CargosComprasAMesesCapital),
        ("MontoIntereses", ps => ps.MontoIntereses),
        ("MontoComisiones", ps => ps.MontoComisiones),
        ("IvaInteresesYComisiones", ps => ps.IvaInteresesYComisiones),
        ("PagosYAbonos", ps => ps.PagosYAbonos),
    ];

    /// <summary>
    /// Runs the extractor directly to read the raw RESUMEN field <c>Confidence</c>/<c>Status</c>
    /// values — mirrors <c>GeometricConfidenceArmingGateE2ETests.TryExtractTasaCatConfidenceAsync</c>,
    /// generalized to all 7 fields. Returns <c>Extracted=false</c> (empty dictionary) when the
    /// extractor itself fails.
    /// </summary>
    private static async Task<(bool Extracted, Dictionary<string, (double Confidence, ExtractionStatus Status)> Fields)>
        TryExtractResumenConfidenceAsync(
            string corpusDir,
            string fixtureName,
            bool emitGeometricConfidence,
            CancellationToken ct)
    {
        var pdfPath = Path.Combine(corpusDir, fixtureName);
        var pdfBytes = await File.ReadAllBytesAsync(pdfPath, ct);

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
            return (false, []);

        var periodSummary = result.Value!.PeriodSummary;
        if (periodSummary is null)
            return (false, []);

        var fields = new Dictionary<string, (double, ExtractionStatus)>();
        foreach (var (name, accessor) in ResumenFields)
        {
            var field = accessor(periodSummary);
            fields[name] = (field.Confidence, field.Status);
        }

        return (true, fields);
    }

    // -----------------------------------------------------------------------
    // Part A — the 5-demo verdict diff (NON-VACUOUS: RESUMEN fields DO extract on real data)
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
    /// The C1.4 gate: for each of the 5 real demo fixtures, the verdict and every RESUMEN field's
    /// raw confidence must be byte-identical whether <c>emitGeometricConfidence</c> is off or on.
    /// Unlike C1.3's Tasa/Cat gate, RESUMEN fields DO extract on these fixtures (CL-21/CL-44 fire
    /// on <c>bad-math-cl21.pdf</c>) — so a diff here is a REAL false-abstain event on real data,
    /// not a vacuous pass. Per the story's DoD: any such diff must be reported to the owner as the
    /// cardinal C1 risk realized, not silently treated as a pass or "fixed" by loosening the
    /// scorer's constants.
    /// </summary>
    [Theory]
    [MemberData(nameof(DemoFixtureNames))]
    public async Task DemoFixture_VerdictAndResumenConfidence_IdenticalFlagOffVsOn(string fixtureName)
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

        var (offExtracted, offFields) = await TryExtractResumenConfidenceAsync(corpusDir, fixtureName, false, ct);
        var (onExtracted, onFields) = await TryExtractResumenConfidenceAsync(corpusDir, fixtureName, true, ct);

        offExtracted.ShouldBe(
            onExtracted,
            $"'{fixtureName}': extraction success must not depend on the geometric-confidence flag.");

        if (!offExtracted)
            return;

        foreach (var (name, _) in ResumenFields)
        {
            var (offConfidence, offStatus) = offFields[name];
            var (onConfidence, onStatus) = onFields[name];

            offStatus.ShouldBe(
                onStatus,
                $"'{fixtureName}'/{name}: extraction status must not depend on the flag.");
            onConfidence.ShouldBe(
                offConfidence,
                $"'{fixtureName}'/{name}: confidence changed OFF={offConfidence} -> ON={onConfidence} — " +
                "cardinal C1 false-abstain risk on REAL demo data.");

            if (onStatus == ExtractionStatus.Extracted)
            {
                onConfidence.ShouldBeGreaterThanOrEqualTo(
                    0.8,
                    $"'{fixtureName}'/{name}: confidence must clear the 0.8 guard when armed.");
            }
        }
    }

    // -----------------------------------------------------------------------
    // Part B — mandatory negative control
    // -----------------------------------------------------------------------

    /// <summary>
    /// The negative control the C1 design of record requires: a harness that only ever shows
    /// "nothing moved" cannot distinguish a working fix from an inert one. <c>decoy-resumen-amount.pdf</c>
    /// (C1.4) carries a decoy amount that mis-picks <c>AdeudoPeriodoAnterior</c>, proven separable
    /// by <c>GeometricPlausibilityResumenCalibrationTests</c>. This drives it through the FULL
    /// pipeline and proves the mis-pick flips CL-21 (which consumes <c>AdeudoPeriodoAnterior</c> —
    /// see <c>Cl21PagoParaNoGenerarInteresesRule</c>) from a confident (wrong-operand) verdict,
    /// flag off, to an honest <c>InsufficientData</c> abstain, flag on.
    /// </summary>
    [Fact]
    public async Task NegativeControl_DecoyResumenAmount_Cl21FlipsToInsufficientDataWhenArmed()
    {
        var ct = TestContext.Current.CancellationToken;

        var syntheticDir = SyntheticCorpusDir;
        if (syntheticDir is null || RepoRoot is null)
            Assert.Skip("Repository root could not be determined from assembly location — synthetic corpus unreachable.");

        const string fixtureName = "decoy-resumen-amount.pdf";
        var fixturePath = Path.Combine(syntheticDir, fixtureName);
        if (!File.Exists(fixturePath))
            Assert.Skip($"Synthetic fixture '{fixtureName}' not present at '{fixturePath}'.");

        // Reuse the demo reference bundle — the s6211 baseline's TC-BSSB product token aliases
        // into products.csv exactly like s-c1-swap-displaced.pdf (see
        // GeometricConfidenceArmingGateE2ETests / SyntheticDefectVerdictE2ETests).
        var bundleRootDir = Path.Combine(RepoRoot, "Prisma/Fixtures/PRP2/demo/reference-bundle");
        if (!Directory.Exists(bundleRootDir))
            Assert.Skip($"Reference bundle root not found at '{bundleRootDir}'.");

        // ------------------------------------------------------------------
        // Flag OFF — AdeudoPeriodoAnterior mis-picked at confidence 1.0; CL-21 sees the wrong
        // operand and must NOT abstain (it has no reason to — confidence is 1.0).
        // ------------------------------------------------------------------
        var off = await RunPipelineAsync(syntheticDir, bundleRootDir, fixtureName, emitGeometricConfidence: false, ct);
        off.InsufficientDataCheckIds.ShouldNotContain(
            "CL-21",
            "flag-off CL-21 must not abstain — the mis-pick reports confidence 1.0: " + off.Dump("OFF"));

        // ------------------------------------------------------------------
        // Flag ON — the rank-adjacency signal must abstain-gate CL-21's AdeudoPeriodoAnterior operand.
        // ------------------------------------------------------------------
        var on = await RunPipelineAsync(syntheticDir, bundleRootDir, fixtureName, emitGeometricConfidence: true, ct);
        on.InsufficientDataCheckIds.ShouldContain(
            "CL-21",
            "flag-on must convert the decoy-leaked pick into an honest abstain: " + on.Dump("ON"));

        // ------------------------------------------------------------------
        // Corroborate at the confidence level (belt-and-braces with the verdict-level proof).
        // ------------------------------------------------------------------
        var (offExtracted, offFields) = await TryExtractResumenConfidenceAsync(syntheticDir, fixtureName, false, ct);
        offExtracted.ShouldBeTrue($"'{fixtureName}': extraction must succeed (flag off).");
        offFields["AdeudoPeriodoAnterior"].Status.ShouldBe(ExtractionStatus.Extracted,
            $"'{fixtureName}': AdeudoPeriodoAnterior must be Extracted (flag off).");
        offFields["AdeudoPeriodoAnterior"].Confidence.ShouldBe(1.0,
            $"'{fixtureName}' (flag off): AdeudoPeriodoAnterior confidence must be the pre-C1.4 constant 1.0.");

        var (onExtracted, onFields) = await TryExtractResumenConfidenceAsync(syntheticDir, fixtureName, true, ct);
        onExtracted.ShouldBeTrue($"'{fixtureName}': extraction must succeed (flag on).");
        onFields["AdeudoPeriodoAnterior"].Status.ShouldBe(ExtractionStatus.Extracted,
            $"'{fixtureName}': AdeudoPeriodoAnterior must be Extracted (flag on).");
        onFields["AdeudoPeriodoAnterior"].Confidence.ShouldBeLessThan(0.8,
            $"'{fixtureName}' (flag on): AdeudoPeriodoAnterior confidence must fall below the 0.8 guard.");

        // Every OTHER RESUMEN field on this fixture is an undisturbed clean pick — the flag must
        // not have collaterally abstain-gated any of them.
        foreach (var (name, _) in ResumenFields)
        {
            if (name == "AdeudoPeriodoAnterior")
                continue;

            onFields[name].Confidence.ShouldBe(1.0,
                $"'{fixtureName}'/{name}: undisturbed sibling must stay at ceiling when armed.");
        }
    }
}
